<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';

import MapView from './components/MapView.vue';
import SettingsModal from './components/SettingsModal.vue';
import { useLocationStore } from './services/location';

const locationStore = useLocationStore();
const isSettingsOpen = ref(false);
let pollInterval: number | null = null;

function enableDemoMode() {
    locationStore.setDeviceToken('demo-device-token-12345');
    locationStore.setPskPassphrase('family-secret-key-32-chars-long!');

    locationStore.familyFeed = [
        {
            id: 1,
            name: 'Dad',
            lastUpdated: new Date().toISOString(),
            location: { latitude: 37.7749, longitude: -122.4194, accuracy: 10 }
        },
        {
            id: 2,
            name: 'Mom',
            lastUpdated: new Date(Date.now() - 5 * 60000).toISOString(),
            location: { latitude: 37.7833, longitude: -122.4167, accuracy: 8 }
        }
    ];
}

onMounted(async () => {
    if (locationStore.isAuthenticated && locationStore.hasConfiguredPsk) {
        await locationStore.checkUserStatus();
        await locationStore.fetchFeed();

        // Poll feed and status check periodically
        pollInterval = window.setInterval(async () => {
            await locationStore.checkUserStatus();
            await locationStore.fetchFeed();
        }, 10000);
    }
});

onUnmounted(() => {
    if (pollInterval) clearInterval(pollInterval);
});
</script>

<template>
    <main>
        <!-- Setup Notice overlay when unconfigured -->
        <div v-if="!locationStore.isAuthenticated || !locationStore.hasConfiguredPsk" class="setup-notice">
            <h2>SelfPin Setup Required</h2>
            <p>Please configure your Device Token and PSK Passphrase in settings.</p>

            <div class="actions">
                <button class="btn-primary" @click="isSettingsOpen = true">
                    ⚙️ Open Settings
                </button>
                <button class="btn-secondary" @click="enableDemoMode">
                    🧪 Load Map Demo Mode
                </button>
            </div>
        </div>

        <!-- Active Map View -->
        <template v-else>
            <MapView />

            <div class="floating-controls">
                <button class="btn-settings" @click="isSettingsOpen = true" title="Settings">
                    ⚙️
                </button>
            </div>
        </template>

        <!-- Settings Modal -->
        <SettingsModal v-if="isSettingsOpen" @close="isSettingsOpen = false" />
    </main>
</template>

<style>
body,
html,
#app,
main {
    margin: 0;
    padding: 0;
    width: 100%;
    height: 100%;
    overflow: hidden;
}

.setup-notice {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100vh;
    padding: 2rem;
    font-family: system-ui, -apple-system, sans-serif;
    text-align: center;
    background-color: #f8fafc;
}

.actions {
    display: flex;
    gap: 0.75rem;
    margin-top: 1.5rem;
}

.btn-primary {
    background-color: #2563eb;
    color: #ffffff;
    border: none;
    padding: 0.75rem 1.25rem;
    font-size: 1rem;
    font-weight: 600;
    border-radius: 0.5rem;
    cursor: pointer;
}

.btn-secondary {
    background-color: #e2e8f0;
    color: #334155;
    border: none;
    padding: 0.75rem 1.25rem;
    font-size: 1rem;
    font-weight: 600;
    border-radius: 0.5rem;
    cursor: pointer;
}

.floating-controls {
    position: absolute;
    top: 1rem;
    right: 1rem;
    z-index: 1000;
}

.btn-settings {
    background: #ffffff;
    border: 1px solid #cbd5e1;
    width: 2.75rem;
    height: 2.75rem;
    border-radius: 50%;
    font-size: 1.25rem;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.15);
}
</style>