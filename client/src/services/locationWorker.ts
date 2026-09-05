import { registerPlugin } from '@capacitor/core';

import { useLocationStore } from './location';

export interface LocationPayload {
    latitude: number;
    longitude: number;
    accuracy?: number;
    altitude?: number;
    simulated?: boolean;
}

// Access the Capacitor Background Geolocation native plugin
const BackgroundGeolocation = registerPlugin<any>('BackgroundGeolocation');

let watcherId: string | null = null;

/**
 * Starts background location tracking using @capacitor-community/background-geolocation.
 * Automatically posts updates across all configured groups in Pinia store.
 */
export async function startBackgroundLocationWatcher(): Promise<string | null> {
    const locationStore = useLocationStore();

    if (!locationStore.isAuthenticated) {
        console.warn('Cannot start background location watcher: Device is not authenticated.');
        return null;
    }

    if (!locationStore.hasGroupKeys) {
        console.warn('Cannot start background location watcher: No group encryption keys configured.');
        return null;
    }

    // Avoid creating duplicate listeners if already running
    if (watcherId !== null) {
        return watcherId;
    }

    try {
        watcherId = await BackgroundGeolocation.addWatcher(
            {
                backgroundTitle: 'Live Location Sharing Active',
                backgroundMessage: 'Sharing encrypted location updates with your groups.',
                requestPermissions: true,
                stale: false,
                distanceFilter: 15 // Trigger update after moving 15 meters
            },
            async (location: LocationPayload, error: any) => {
                if (error) {
                    console.error('Background Geolocation Error:', error);
                    return;
                }

                if (location) {
                    // Encrypt and post update across all active groups in groupKeys
                    await locationStore.publishLocation({
                        latitude: location.latitude,
                        longitude: location.longitude,
                        accuracy: location.accuracy
                    });
                }
            }
        );

        console.log(`Background location watcher initialized with ID: ${watcherId}`);
        return watcherId;
    } catch (err: any) {
        console.error('Failed to register native background geolocation watcher:', err);
        return null;
    }
}

/**
 * Stops active background tracking.
 */
export async function stopBackgroundLocationWatcher(): Promise<void> {
    if (watcherId !== null) {
        try {
            await BackgroundGeolocation.removeWatcher({ id: watcherId });
            console.log(`Stopped background location watcher: ${watcherId}`);
            watcherId = null;
        } catch (err: any) {
            console.error('Error stopping background location watcher:', err);
        }
    }
}