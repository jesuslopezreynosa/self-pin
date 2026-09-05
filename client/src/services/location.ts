import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

import {
    encryptLocationPayload,
    decryptLocationPayload,
    signKeyRotationPayload,
    verifyKeyRotationPayload,
    type UnencryptedLocation,
    type KeyRotationPayload
} from '../services/crypto';

import { isValidServerUrl } from '../services/urlValidator';

export interface GroupKeyConfig {
    psk: string;
    keyVersion: number;
}

export interface DecryptedMemberFeed {
    id: number;
    name: string;
    groupId: string;
    lastUpdated: string;
    location: UnencryptedLocation | null;
    keyVersion?: number;
}

export const useLocationStore = defineStore('location', () => {
    // User & Authentication State
    const deviceToken = ref<string>(localStorage.getItem('deviceToken') || '');
    const userSigningKey = ref<string>(localStorage.getItem('userSigningKey') || '');
    const apiBaseUrl = ref<string>(localStorage.getItem('apiBaseUrl') || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5180');

    // Group-Specific Encryption Keys State: Record<groupId, { psk, keyVersion }>
    const groupKeys = ref<Record<string, GroupKeyConfig>>(
        JSON.parse(localStorage.getItem('groupKeys') || '{}')
    );

    // UI & Location State
    const familyFeed = ref<DecryptedMemberFeed[]>([]);
    const isUpdating = ref<boolean>(false);
    const isConfigured = ref<boolean>(!!localStorage.getItem('apiBaseUrl') && !!deviceToken.value);
    const error = ref<string | null>(null);
    const cartoApiKey = ref<string>('');

    // Getters
    const isAuthenticated = computed(() => deviceToken.value.length > 0);
    const hasGroupKeys = computed(() => Object.keys(groupKeys.value).length > 0);

    // Key Management Helpers
    function setDeviceToken(token: string) {
        deviceToken.value = token;
        localStorage.setItem('deviceToken', token);
    }

    function setUserSigningKey(key: string) {
        userSigningKey.value = key;
        localStorage.setItem('userSigningKey', key);
    }

    function setGroupKey(groupId: string, psk: string, keyVersion: number = 1) {
        groupKeys.value[groupId] = { psk, keyVersion };
        localStorage.setItem('groupKeys', JSON.stringify(groupKeys.value));
    }

    function removeGroupKey(groupId: string) {
        delete groupKeys.value[groupId];
        localStorage.setItem('groupKeys', JSON.stringify(groupKeys.value));
    }

    /**
     * Configures the server URL and enforces HTTPS outside localhost.
     */
    function configureServerUrl(serverUrl: string) {
        const validation = isValidServerUrl(serverUrl);
        if (!validation.valid) {
            throw new Error(validation.reason);
        }

        apiBaseUrl.value = serverUrl.replace(/\/+$/, '');
        localStorage.setItem('apiBaseUrl', apiBaseUrl.value);
    }

    /**
     * Registers this client on the backend to receive a server-generated deviceToken.
     */
    async function registerDevice(userName: string) {
        if (!apiBaseUrl.value) {
            throw new Error('Server URL is not configured.');
        }

        error.value = null;

        try {
            const response = await fetch(`${apiBaseUrl.value}/admin/users`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: userName })
            });

            if (!response.ok) {
                throw new Error('Failed to register device on server.');
            }

            const data = await response.json();
            setDeviceToken(data.deviceToken);
            if (data.userSigningKey) {
                setUserSigningKey(data.userSigningKey);
            }
            isConfigured.value = true;
            return data;
        } catch (err: any) {
            error.value = err.message || 'Device registration failed.';
            throw err;
        }
    }

    /**
     * Creates a location sharing group with another device via their Server Device Token.
     */
    async function shareLocationWithDevice(targetDeviceId: string, groupName?: string, initialPsk?: string) {
        if (!isAuthenticated.value) {
            throw new Error('Device is not authenticated.');
        }

        error.value = null;

        try {
            const response = await fetch(`${apiBaseUrl.value}/api/v1/location/groups/share`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Device-Token': deviceToken.value
                },
                body: JSON.stringify({
                    targetDeviceId,
                    groupName: groupName || 'Direct Share'
                })
            });

            if (!response.ok) {
                const errData = await response.json().catch(() => ({}));
                throw new Error(errData.error || 'Failed to share location with device.');
            }

            const groupData = await response.json();

            // If an initial PSK was specified for this new group, store it locally
            if (groupData.groupId && initialPsk) {
                setGroupKey(groupData.groupId, initialPsk, 1);
            }

            await fetchFeed();
            return groupData;
        } catch (err: any) {
            error.value = err.message || 'Error creating group share.';
            throw err;
        }
    }

    /**
     * Fetches map configuration from backend.
     */
    async function fetchMapConfig() {
        if (!isAuthenticated.value) return;

        try {
            const response = await fetch(`${apiBaseUrl.value}/api/v1/location/map-config`, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json',
                    'X-Device-Token': deviceToken.value
                }
            });

            if (response.ok) {
                const config = await response.json();
                cartoApiKey.value = config.cartoApiKey;
            }
        } catch (err: any) {
            console.warn('Could not retrieve map configuration.', err.message);
        }
    }

    /**
     * Encrypts and publishes location updates for each group the user belongs to using that group's active PSK.
     */
    async function publishLocation(coords: { latitude: number; longitude: number; accuracy?: number; }) {
        if (!isAuthenticated.value || !hasGroupKeys.value) {
            error.value = 'Device token or group encryption keys missing.';
            return;
        }

        isUpdating.value = true;
        error.value = null;

        try {
            // Publish encrypted payload to each configured group independently
            for (const [groupId, groupConfig] of Object.entries(groupKeys.value)) {
                const encryptedPayload = await encryptLocationPayload(coords, groupConfig.psk);

                const response = await fetch(`${apiBaseUrl.value}/api/v1/location/update`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Device-Token': deviceToken.value
                    },
                    body: JSON.stringify({
                        groupId,
                        encryptedPayload,
                        keyVersion: groupConfig.keyVersion
                    })
                });

                if (!response.ok) {
                    console.warn(`Failed to update location for group ${groupId}: ${response.statusText}`);
                }
            }
        } catch (err: any) {
            error.value = err.message || 'Error publishing location.';
        } finally {
            isUpdating.value = false;
        }
    }

    /**
     * Initiates a key rotation for a specific group signed with that group's active PSK.
     */
    async function initiateKeyRotation(groupId: string, newPassphrase: string) {
        const groupConfig = groupKeys.value[groupId];
        if (!isAuthenticated.value || !groupConfig) {
            error.value = `Cannot rotate keys: Group ${groupId} is not configured on this client.`;
            return;
        }

        const nextVersion = groupConfig.keyVersion + 1;
        const encryptedNewKey = btoa(newPassphrase);

        const rotationPayload: KeyRotationPayload = {
            groupId,
            newKeyVersion: nextVersion,
            encryptedNewKey
        };

        // HMAC signature signed with the current active PSK of this specific group
        const signature = await signKeyRotationPayload(rotationPayload, groupConfig.psk);
        rotationPayload.signature = signature;

        const response = await fetch(`${apiBaseUrl.value}/api/v1/location/rotate-key`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Device-Token': deviceToken.value
            },
            body: JSON.stringify(rotationPayload)
        });

        if (!response.ok) {
            throw new Error(`Server rejected key rotation request for group ${groupId}.`);
        }

        setGroupKey(groupId, newPassphrase, nextVersion);
    }

    /**
     * Processes pending rotation requests for a specific group after verifying HMAC signature.
     */
    async function processPendingKeyRotation(groupId: string, rotationPayload: KeyRotationPayload): Promise<boolean> {
        const groupConfig = groupKeys.value[groupId];
        if (!groupConfig) {
            console.error(`Cannot process key rotation: Missing PSK configuration for group ${groupId}.`);
            return false;
        }

        const isValid = await verifyKeyRotationPayload(rotationPayload, groupConfig.psk);

        if (!isValid) {
            console.error(`Security Violation: Unauthorized key rotation attempt for group ${groupId}!`);
            error.value = `Security Alert: Failed to verify key rotation for group ${groupId}.`;
            return false;
        }

        const newPassphrase = atob(rotationPayload.encryptedNewKey);
        setGroupKey(groupId, newPassphrase, rotationPayload.newKeyVersion);
        return true;
    }

    /**
     * Fetches shared feed and decrypts entry payloads using group-specific PSKs.
     */
    async function fetchFeed() {
        if (!isAuthenticated.value) {
            error.value = 'Device token missing.';
            return;
        }

        error.value = null;

        try {
            const response = await fetch(`${apiBaseUrl.value}/api/v1/location/feed`, {
                headers: {
                    'Accept': 'application/json',
                    'X-Device-Token': deviceToken.value
                }
            });

            if (response.status === 401) {
                throw new Error('Unauthorized device token.');
            }

            if (!response.ok) {
                throw new Error('Failed to fetch location feed.');
            }

            const rawFeed = await response.json();

            const decryptedEntries = await Promise.all(
                rawFeed.map(async (member: any) => {
                    let location: UnencryptedLocation | null = null;
                    const groupConfig = groupKeys.value[member.groupId];

                    if (member.latestEntry?.encryptedPayload && groupConfig) {
                        try {
                            location = await decryptLocationPayload(
                                member.latestEntry.encryptedPayload,
                                groupConfig.psk
                            );
                        } catch (decryptionErr) {
                            console.warn(`Could not decrypt payload for user ${member.id} in group ${member.groupId}. Key mismatch or pending rotation?`);
                        }
                    }

                    return {
                        id: member.id,
                        name: member.name,
                        groupId: member.groupId,
                        lastUpdated: member.lastUpdated,
                        keyVersion: member.latestEntry?.keyVersion,
                        location
                    } as DecryptedMemberFeed;
                })
            );

            familyFeed.value = decryptedEntries;
        } catch (err: any) {
            error.value = err.message || 'Error fetching location feed.';
        }
    }

    /**
     * Checks user status to detect pending key rotations across joined groups.
     */
    async function checkUserStatus() {
        if (!isAuthenticated.value) return;

        try {
            const response = await fetch(`${apiBaseUrl.value}/api/v1/location/status`, {
                headers: {
                    'Accept': 'application/json',
                    'X-Device-Token': deviceToken.value
                }
            });

            if (!response.ok) return;

            const data = await response.json();

            for (const group of data.groups) {
                if (group.pendingKeyRotation && group.rotationPayload) {
                    console.warn(`Pending key rotation detected for group ${group.groupId}. Processing...`);
                    await processPendingKeyRotation(group.groupId, group.rotationPayload);
                }
            }
        } catch (err: any) {
            console.error('Error checking user status:', err.message);
        }
    }

    function resetDeviceAuth() {
        deviceToken.value = '';
        localStorage.removeItem('deviceToken');
        isConfigured.value = false;
    }

    return {
        // State
        deviceToken,
        userSigningKey,
        groupKeys,
        apiBaseUrl,
        familyFeed,
        isUpdating,
        isConfigured,
        error,
        cartoApiKey,

        // Getters
        isAuthenticated,
        hasGroupKeys,

        // Actions
        setDeviceToken,
        setUserSigningKey,
        setGroupKey,
        removeGroupKey,
        configureServerUrl,
        registerDevice,
        shareLocationWithDevice,
        fetchMapConfig,
        publishLocation,
        initiateKeyRotation,
        processPendingKeyRotation,
        fetchFeed,
        checkUserStatus,
        resetDeviceAuth
    };
});