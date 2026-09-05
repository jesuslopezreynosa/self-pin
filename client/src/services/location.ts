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
    const apiBaseUrl = ref<string>(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5180');
    const currentKeyVersion = ref<number>(Number(localStorage.getItem('keyVersion')) || 1);

    const familyFeed = ref<DecryptedMemberFeed[]>([]);
    const isUpdating = ref<boolean>(false);
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
     * Fetches the map configuration from backend.
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
            console.warn('Could not retrieve map configuration from server.', err.message);
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
        const encryptedNewKey = btoa(newPassphrase); // Replace with your group key-exchange cipher payload

        const rotationPayload: KeyRotationPayload = {
            groupId,
            newKeyVersion: nextVersion,
            encryptedNewKey
        };

        // HMAC Sign rotation request with current active PSK
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

        // Update local state with newly adopted key
        setPskPassphrase(newPassphrase, nextVersion);
    }

    /**
     * Process pending rotation requests after verification against active PSK.
     */
    async function processPendingKeyRotation(rotationPayload: KeyRotationPayload): Promise<boolean> {
        const isValid = await verifyKeyRotationPayload(rotationPayload, pskPassphrase.value);

        if (!isValid) {
            console.error('Security Violation: Unauthorized key rotation attempt detected! Signature verification failed.');
            error.value = 'Security Alert: Failed to verify key rotation from server.';
            return false;
        }

        // Decrypt or decode the new key material and update store state
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
                            console.warn(`Could not decrypt payload for user ${member.id}. Stale or mismatching key?`);
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

            // Find any group marked with PendingKeyRotation = true
            for (const group of data.groups) {
                if (group.pendingKeyRotation) {
                    console.warn(`Pending key rotation detected for group ${group.groupId}. Executing rotation sync...`);
                    // Trigger client-side key fetch or auto-rotation handler
                }
            }
        } catch (err: any) {
            console.error('Error checking user status:', err.message);
        }
    }

    return {
        deviceToken,
        pskPassphrase,
        currentKeyVersion,
        apiBaseUrl,
        familyFeed,
        isUpdating,
        error,
        cartoApiKey,
        isAuthenticated,
        hasConfiguredPsk,
        setDeviceToken,
        setPskPassphrase,
        fetchMapConfig,
        publishLocation,
        initiateKeyRotation,
        processPendingKeyRotation,
        fetchFeed,
        checkUserStatus
    };
});