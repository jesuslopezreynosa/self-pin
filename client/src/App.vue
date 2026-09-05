<script setup lang="ts">
import HelloWorld from './components/HelloWorld.vue'
</script>

<template>
  <HelloWorld />
</template>

<!-- <script setup lang="ts">
import { onMounted } from 'vue';
import { setupBackgroundTracking } from './services/location';

onMounted(async () => {
  // Replace with dynamic storage or device key from local configuration
  const DEVICE_TOKEN = "FAMILY_MEMBER_GUID_SECRET"; 
  const API_BASE_URL = "https://your-public-domain.com";

  await setupBackgroundTracking(DEVICE_TOKEN, API_BASE_URL);
});
</script>

<template>
  <main>
    <h1>SelfPin Active</h1>
    <p>Background location sharing is initialized.</p>
  </main>
</template> -->