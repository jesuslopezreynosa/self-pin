import { registerPlugin } from '@capacitor/core';
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

import {
    encryptLocationPayload,
    // decryptLocationPayload,
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
    const deviceToken = ref<string>(localStorage.getItem('deviceToken') || '');
    const pskPassphrase = ref<string>(localStorage.getItem('pskPassphrase') || '');
    const apiBaseUrl = ref<string>(localStorage.getItem('apiBaseUrl') || 'http://localhost:5180');
    const familyFeed = ref<any[]>([]);
    const cartoApiKey = ref<string>('');

    const isAuthenticated = computed(() => !!deviceToken.value.trim());
    const hasConfiguredPsk = computed(() => !!pskPassphrase.value.trim());

    /**
     * Fetches the protected map configuration and CARTO API key from the backend.
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
                cartoApiKey.value = config.cartoApiKey || '';
            } else {
                console.warn(`Map config server returned ${response.status}. Falling back to default tiles.`);
                cartoApiKey.value = ''; // Reset key on failure
            }
        } catch (err: any) {
            console.warn('Network error fetching map config. Falling back to default tiles.', err.message);
            cartoApiKey.value = '';
        }
    }

    function setDeviceToken(token: string) {
        deviceToken.value = token;
        localStorage.setItem('deviceToken', token);
    }

    function setPskPassphrase(psk: string) {
        pskPassphrase.value = psk;
        localStorage.setItem('pskPassphrase', psk);
    }

    return {
        deviceToken,
        pskPassphrase,
        apiBaseUrl,
        familyFeed,
        cartoApiKey,
        isAuthenticated,
        hasConfiguredPsk,
        fetchMapConfig,
        setDeviceToken,
        setPskPassphrase
    };
});