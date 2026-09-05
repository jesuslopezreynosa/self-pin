<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';

import MapView from './components/MapView.vue';
import OnboardingModal from './components/OnboardingModal.vue';
import PeopleDrawer from './components/PeopleDrawer.vue';
import SettingsModal from './components/SettingsModal.vue';
import { useLocationStore } from './services/location.ts';
import {
    startBackgroundLocationWatcher,
    stopBackgroundLocationWatcher
} from './services/locationWorker';

const locationStore = useLocationStore();
const isSettingsOpen = ref(false);
let pollInterval: number | null = null;

onMounted(async () => {
    if (locationStore.isAuthenticated && locationStore.hasGroupKeys) {
        // Initial data sync
        await locationStore.checkUserStatus();
        await locationStore.fetchFeed();

        // Start background location updates across active groups
        await startBackgroundLocationWatcher();

        // Periodic feed and rotation check
        pollInterval = window.setInterval(async () => {
            await locationStore.checkUserStatus();
            await locationStore.fetchFeed();
        }, 10000);
    }
});

onUnmounted(async () => {
    if (pollInterval) clearInterval(pollInterval);
    await stopBackgroundLocationWatcher();
});
</script>

<template>
    <main>
        <!-- Onboarding Flow -->
        <OnboardingModal v-if="!locationStore.isAuthenticated || !locationStore.isConfigured" />

        <!-- Active Interface with Map and Sliding Drawer -->
        <template v-else>
            <MapView />
            <PeopleDrawer @openSettings="isSettingsOpen = true" />
        </template>

        <!-- Settings Modal -->
        <SettingsModal v-if="isSettingsOpen" @close="isSettingsOpen = false" />
    </main>
</template>