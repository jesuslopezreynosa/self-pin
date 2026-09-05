<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

import { useLocationStore } from '../services/location';

const locationStore = useLocationStore();
const mapContainer = ref<HTMLElement | null>(null);
let map: L.Map | null = null;
const markers = new Map<number, L.Marker>();

const defaultIcon = L.icon({
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowSize: [41, 41]
});

async function initMap() {
    if (!mapContainer.value) return;

    map = L.map(mapContainer.value).setView([37.7749, -122.4194], 13);

    // Fetch Carto API key from server if not already in store memory
    if (!locationStore.cartoApiKey) {
        await locationStore.fetchMapConfig();
    }

    // Append key to tile URL if returned by server
    const apiKeyQuery = locationStore.cartoApiKey ? `?api_key=${locationStore.cartoApiKey}` : '';
    const tileUrl = `https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png${apiKeyQuery}`;

    L.tileLayer(tileUrl, {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
        subdomains: 'abcd',
        maxZoom: 20
    }).addTo(map);

    if (import.meta.env.DEV && locationStore.familyFeed.length === 0) {
        injectDevMarkers(map);
    } else {
        updateMarkers();
    }
}

function injectDevMarkers(targetMap: L.Map) {
    const devLocations = [
        { name: 'Dev User 1 (SF)', lat: 37.7749, lng: -122.4194 },
        { name: 'Dev User 2 (Oakland)', lat: 37.8044, lng: -122.2712 },
        { name: 'Dev User 3 (San Jose)', lat: 37.3382, lng: -121.8863 }
    ];

    const bounds: L.LatLngExpression[] = [];

    devLocations.forEach((dev) => {
        const latLng: [number, number] = [dev.lat, dev.lng];
        bounds.push(latLng);

        const popupContent = `
      <div style="font-family: system-ui, sans-serif; padding: 2px;">
        <strong style="font-size: 14px; color: #2563eb;">🛠️ ${dev.name}</strong><br/>
        <small style="color: #64748b;">Development Mock Marker</small>
      </div>
    `;

        L.marker(latLng, { icon: defaultIcon })
            .bindPopup(popupContent)
            .addTo(targetMap);
    });

    targetMap.fitBounds(L.latLngBounds(bounds), { padding: [50, 50] });
}

function updateMarkers() {
    if (!map) return;
    const currentMap = map;
    const bounds: L.LatLngExpression[] = [];

    locationStore.familyFeed.forEach((member) => {
        if (!member.location) return;

        const { latitude, longitude } = member.location;
        const latLng: [number, number] = [latitude, longitude];
        bounds.push(latLng);

        const popupContent = `
      <div style="font-family: system-ui, sans-serif; padding: 2px;">
        <strong style="font-size: 14px; color: #1e293b;">${member.name}</strong><br/>
        <small style="color: #64748b;">Updated: ${new Date(member.lastUpdated).toLocaleTimeString()}</small>
      </div>
    `;

        if (markers.has(member.id)) {
            const marker = markers.get(member.id)!;
            marker.setLatLng(latLng);
            marker.setPopupContent(popupContent);
        } else {
            const marker = L.marker(latLng, { icon: defaultIcon })
                .bindPopup(popupContent)
                .addTo(currentMap);

            markers.set(member.id, marker);
        }
    });

    if (bounds.length > 0) {
        currentMap.fitBounds(L.latLngBounds(bounds), { padding: [50, 50], maxZoom: 16 });
    }
}

watch(() => locationStore.familyFeed, () => {
    updateMarkers();
}, { deep: true });

onMounted(() => {
    initMap();
});

onUnmounted(() => {
    if (map) {
        map.remove();
        map = null;
    }
});
</script>

<template>
    <div class="map-wrapper">
        <div ref="mapContainer" class="map-container"></div>
    </div>
</template>

<style scoped>
.map-wrapper {
    width: 100%;
    height: 100vh;
    position: relative;
}

.map-container {
    width: 100%;
    height: 100%;
}
</style>