import { registerPlugin } from '@capacitor/core';
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

import {
    encryptLocationPayload,
    decryptLocationPayload,
    type UnencryptedLocation
} from './crypto';

export interface DecryptedMemberFeed {
    id: number;
    name: string;
    lastUpdated: string;
    location: UnencryptedLocation | null;
}

// Types for background geolocation plugin
interface LocationPayload {
    latitude: number;
    longitude: number;
    accuracy: number;
    altitude?: number;
    simulated?: boolean;
}

const BackgroundGeolocation = registerPlugin<any>('BackgroundGeolocation');

export async function setupBackgroundTracking(deviceToken: string, apiBaseUrl: string) {
    try {
        const watcherId = await BackgroundGeolocation.addWatcher(
            {
                backgroundTitle: "Live Location Active",
                backgroundMessage: "Sharing location updates with your group(s).",
                requestPermissions: true,
                stale: false,
                distanceFilter: 15 // Trigger update after moving 15 meters
            },
            async (location: LocationPayload, error: any) => {
                if (error) {
                    console.error("Location tracking error:", error);
                    return;
                }

                if (location) {
                    await fetch(`${apiBaseUrl}/api/v1/location/update`, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Device-Token': deviceToken
                        },
                        body: JSON.stringify({
                            latitude: location.latitude,
                            longitude: location.longitude,
                            accuracy: location.accuracy,
                            timestamp: new Date().toISOString()
                        })
                    });
                }
            }
        );

        return watcherId;
    } catch (err) {
        console.error("Failed to start background tracking:", err);
    }
}

export async function sendEncryptedLocation(
    location: { latitude: number; longitude: number; accuracy: number; },
    deviceToken: string,
    pskPassphrase: string,
    apiBaseUrl: string
) {
    // 1. Encrypt location client-side
    const encryptedBlob = await encryptLocationPayload(location, pskPassphrase);

    // 2. Post encrypted payload to .NET server
    await fetch(`${apiBaseUrl}/api/v1/location/update`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Device-Token': deviceToken
        },
        body: JSON.stringify({
            encryptedPayload: encryptedBlob,
            keyVersion: 1
        })
    });
}

export const useLocationStore = defineStore('location', () => {
    // State
    const deviceToken = ref<string>(localStorage.getItem('deviceToken') || '');
    const pskPassphrase = ref<string>(localStorage.getItem('pskPassphrase') || '');
    const apiBaseUrl = ref<string>(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5180');

    const familyFeed = ref<DecryptedMemberFeed[]>([]);
    const isUpdating = ref<boolean>(false);
    const error = ref<string | null>(null);

    // Getters
    const isAuthenticated = computed(() => deviceToken.value.length > 0);
    const hasConfiguredPsk = computed(() => pskPassphrase.value.length > 0);

    // Actions
    function setDeviceToken(token: string) {
        deviceToken.value = token;
        localStorage.setItem('deviceToken', token);
    }

    function setPskPassphrase(passphrase: string) {
        pskPassphrase.value = passphrase;
        localStorage.setItem('pskPassphrase', passphrase);
    }

    /**
     * Encrypts raw GPS coordinates and pushes them to the server relay.
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
                    keyVersion: 1
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
     * Fetches the shared location feed from the server and decrypts each payload locally.
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

            // Decrypt each group member's latest entry using the local PSK
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
                            console.warn(`Could not decrypt payload for user ${member.id}. Invalid key?`);
                        }
                    }

                    return {
                        id: member.id,
                        name: member.name,
                        lastUpdated: member.lastUpdated,
                        location
                    } as DecryptedMemberFeed;
                })
            );

            familyFeed.value = decryptedEntries;
        } catch (err: any) {
            error.value = err.message || 'Error fetching location feed.';
        }
    }

    return {
        deviceToken,
        pskPassphrase,
        apiBaseUrl,
        familyFeed,
        isUpdating,
        error,
        isAuthenticated,
        hasConfiguredPsk,
        setDeviceToken,
        setPskPassphrase,
        publishLocation,
        fetchFeed
    };
});