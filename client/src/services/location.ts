import { registerPlugin } from '@capacitor/core';

import { encryptLocationPayload } from './crypto';

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
                backgroundMessage: "Sharing location updates with your family server.",
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