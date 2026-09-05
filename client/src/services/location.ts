// src/stores/location.ts
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

import {
    encryptLocationPayload,
    decryptLocationPayload,
    signKeyRotationPayload,
    verifyKeyRotationPayload,
    type UnencryptedLocation,
    type KeyRotationPayload
} from './crypto';
import { isValidServerUrl } from './urlValidator';

export interface DecryptedMemberFeed {
    id: number;
    name: string;
    lastUpdated: string;
    location: UnencryptedLocation | null;
    keyVersion?: number;
}

export const useLocationStore = defineStore('location', () => {
    // State
    const deviceToken = ref<string>(localStorage.getItem('deviceToken') || '');
    const pskPassphrase = ref<string>(localStorage.getItem('pskPassphrase') || '');
    const apiBaseUrl = ref<string>(localStorage.getItem('apiBaseUrl') || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5180');
    const currentKeyVersion = ref<number>(Number(localStorage.getItem('keyVersion')) || 1);

    const familyFeed = ref<DecryptedMemberFeed[]>([]);
    const isUpdating = ref<boolean>(false);
    const isConfigured = ref<boolean>(!!localStorage.getItem('apiBaseUrl') && !!deviceToken.value);
    const error = ref<string | null>(null);
    const cartoApiKey = ref<string>('');

    // Getters
    const isAuthenticated = computed(() => deviceToken.value.length > 0);
    const hasConfiguredPsk = computed(() => pskPassphrase.value.length > 0);

    // Actions
    function setDeviceToken(token: string) {
        deviceToken.value = token;
        localStorage.setItem('deviceToken', token);
    }

    function setPskPassphrase(passphrase: string, version: number = 1) {
        pskPassphrase.value = passphrase;
        currentKeyVersion.value = version;
        localStorage.setItem('pskPassphrase', passphrase);
        localStorage.setItem('keyVersion', version.toString());
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
    async function shareLocationWithDevice(targetDeviceId: string, groupName?: string) {
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
     * Encrypts raw GPS coordinates and posts payload to backend.
     */
    async function publishLocation(coords: { latitude: number; longitude: number; accuracy?: number; }) {
        if (!isAuthenticated.value || !hasConfiguredPsk.value) {
            error.value = 'Device token or PSK passphrase missing.';
            return;
        }

        isUpdating.value = true;
        error.value = null;

        try {
            const encryptedPayload = await encryptLocationPayload(coords, pskPassphrase.value);

            const response = await fetch(`${apiBaseUrl.value}/api/v1/location/update`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Device-Token': deviceToken.value
                },
                body: JSON.stringify({
                    encryptedPayload,
                    keyVersion: currentKeyVersion.value
                })
            });

            if (!response.ok) {
                throw new Error(`Failed to update location: ${response.statusText}`);
            }
        } catch (err: any) {
            error.value = err.message || 'Error publishing location.';
        } finally {
            isUpdating.value = false;
        }
    }

    /**
     * Triggers a key rotation signed with the active PSK.
     */
    async function initiateKeyRotation(groupId: string, newPassphrase: string) {
        if (!isAuthenticated.value || !hasConfiguredPsk.value) {
            error.value = 'Cannot rotate keys: Client is unauthenticated or missing PSK.';
            return;
        }

        const nextVersion = currentKeyVersion.value + 1;
        const encryptedNewKey = btoa(newPassphrase);

        const rotationPayload: KeyRotationPayload = {
            groupId,
            newKeyVersion: nextVersion,
            encryptedNewKey
        };

        const signature = await signKeyRotationPayload(rotationPayload, pskPassphrase.value);
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
            throw new Error('Server rejected key rotation request.');
        }

        setPskPassphrase(newPassphrase, nextVersion);
    }

    /**
     * Process pending rotation requests after verification against active PSK.
     */
    async function processPendingKeyRotation(rotationPayload: KeyRotationPayload): Promise<boolean> {
        const isValid = await verifyKeyRotationPayload(rotationPayload, pskPassphrase.value);

        if (!isValid) {
            console.error('Security Violation: Unauthorized key rotation attempt detected!');
            error.value = 'Security Alert: Failed to verify key rotation from server.';
            return false;
        }

        const newPassphrase = atob(rotationPayload.encryptedNewKey);
        setPskPassphrase(newPassphrase, rotationPayload.newKeyVersion);
        return true;
    }

    /**
     * Fetches shared feed and decrypts entry payloads using local PSK.
     */
    async function fetchFeed() {
        if (!isAuthenticated.value || !hasConfiguredPsk.value) {
            error.value = 'Device token or PSK passphrase missing.';
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
                throw new Error('Failed to fetch group feed.');
            }

            const rawFeed = await response.json();

            const decryptedEntries = await Promise.all(
                rawFeed.map(async (member: any) => {
                    let location: UnencryptedLocation | null = null;

                    if (member.latestEntry?.encryptedPayload) {
                        try {
                            location = await decryptLocationPayload(
                                member.latestEntry.encryptedPayload,
                                pskPassphrase.value
                            );
                        } catch (decryptionErr) {
                            console.warn(`Could not decrypt payload for user ${member.id}.`);
                        }
                    }

                    return {
                        id: member.id,
                        name: member.name,
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
                if (group.pendingKeyRotation) {
                    console.warn(`Pending key rotation detected for group ${group.groupId}.`);
                }
            }
        } catch (err: any) {
            console.error('Error checking user status:', err.message);
        }
    }

    return {
        // State
        deviceToken,
        pskPassphrase,
        currentKeyVersion,
        apiBaseUrl,
        familyFeed,
        isUpdating,
        isConfigured,
        error,
        cartoApiKey,

        // Getters
        isAuthenticated,
        hasConfiguredPsk,

        // Actions
        setDeviceToken,
        setPskPassphrase,
        configureServerUrl,
        registerDevice,
        shareLocationWithDevice,
        fetchMapConfig,
        publishLocation,
        initiateKeyRotation,
        processPendingKeyRotation,
        fetchFeed,
        checkUserStatus
    };
});