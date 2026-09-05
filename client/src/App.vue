<script setup lang="ts">
import { onMounted } from 'vue';
import { useLocationStore, setupBackgroundTracking } from './services/location';

const locationStore = useLocationStore();

onMounted(async () => {
    // Option A: Initialize store state if tokens exist in local storage or environment
    if (!locationStore.deviceToken) {
        locationStore.setDeviceToken('FAMILY_MEMBER_GUID_SECRET');
    }

    // Set up background geolocation using state from the Pinia store
    if (locationStore.isAuthenticated) {
        await setupBackgroundTracking(
            locationStore.deviceToken,
            locationStore.apiBaseUrl
        );

        // Fetch initial group feed
        await locationStore.fetchFeed();
    }
});
</script>

<template>
    <main>
        <h1>SelfPin Active</h1>
        <p v-if="locationStore.isAuthenticated">
            Background location sharing is initialized for token: {{ locationStore.deviceToken }}
        </p>
        <p v-else>
            Please configure your device token and PSK in settings.
        </p>
    </main>
</template>