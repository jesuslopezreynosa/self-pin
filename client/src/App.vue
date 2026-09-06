<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';

import MapView from './components/MapView.vue';
import OnboardingModal from './components/OnboardingModal.vue';
import PeopleDrawer from './components/PeopleDrawer.vue';
import SettingsModal from './components/SettingsModal.vue';
import ShareModal from './components/ShareModal.vue';
import { useLocationStore } from './services/location';

const locationStore = useLocationStore();
const isSettingsOpen = ref(false);
const isShareOpen = ref(false);

let pollInterval: number | null = null;

onMounted(async () => {
    if (locationStore.isAuthenticated && locationStore.hasGroupKeys) {
        await locationStore.checkUserStatus();
        await locationStore.fetchFeed();

        pollInterval = window.setInterval(async () => {
            await locationStore.checkUserStatus();
            await locationStore.fetchFeed();
        }, 10000);
    }
});

onUnmounted(() => {
    if (pollInterval) clearInterval(pollInterval);
});

// Refresh list after creating a new group share
async function handleShareCreated() {
    await locationStore.fetchFeed();
}
</script>

<template>
    <main>
        <!-- Onboarding Flow -->
        <OnboardingModal v-if="!locationStore.isAuthenticated || !locationStore.isConfigured" />

        <!-- Active Interface with Map and Sliding Drawer -->
        <template v-else>
            <MapView />
            <PeopleDrawer @openSettings="isSettingsOpen = true" @openShare="isShareOpen = true" />
        </template>

        <!-- Modals -->
        <SettingsModal v-if="isSettingsOpen" @close="isSettingsOpen = false" />
        <ShareModal v-if="isShareOpen" @close="isShareOpen = false" @created="handleShareCreated" />
    </main>
</template>