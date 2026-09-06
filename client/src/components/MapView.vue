<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

import { useLocationStore } from '../services/location';

const locationStore = useLocationStore();
const mapContainer = ref<HTMLElement | null>(null);
let map: L.Map | null = null;
const markers = new Map<number, L.Marker>();

const isLocating = ref(false);
const isCentered = ref(false);
let userLocationMarker: L.Marker | null = null;

const defaultIcon = L.icon({
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowSize: [41, 41]
});

// Pulse ring marker for user's active geolocation
const userLocationIcon = L.divIcon({
    className: 'user-location-marker',
    html: '<div class="pulse-ring"></div><div class="user-dot"></div>',
    iconSize: [24, 24],
    iconAnchor: [12, 12]
});

async function initMap() {
    if (!mapContainer.value) return;

    // Disable default top-left zoom controls (+/-)
    map = L.map(mapContainer.value, {
        zoomControl: false
    }).setView([37.7749, -122.4194], 13);

    // Reset filled SF Symbol icon if the user manually drags or zooms the map away
    map.on('movestart', () => {
        if (!isLocating.value) {
            isCentered.value = false;
        }
    });

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

function centerOnUserLocation() {
    if (!map || isLocating.value) return;

    if (!navigator.geolocation) {
        alert('Geolocation is not supported by your browser or device.');
        return;
    }

    isLocating.value = true;

    navigator.geolocation.getCurrentPosition(
        (position) => {
            isLocating.value = false;
            isCentered.value = true;
            const { latitude, longitude } = position.coords;
            const userLatLng: [number, number] = [latitude, longitude];

            if (map) {
                map.flyTo(userLatLng, 16, { animate: true, duration: 1.2 });

                if (userLocationMarker) {
                    userLocationMarker.setLatLng(userLatLng);
                } else {
                    userLocationMarker = L.marker(userLatLng, { icon: userLocationIcon })
                        .bindPopup('<strong>You are here</strong>')
                        .addTo(map);
                }
            }
        },
        (error) => {
            isLocating.value = false;
            isCentered.value = false;
            console.error('Error fetching current location:', error);
            alert('Unable to retrieve your location. Please check location permissions.');
        },
        { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
    );
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

        <!-- Center Location Floating Control Button -->
        <button class="btn-center-location" :class="{ 'is-loading': isLocating, 'is-active': isCentered }"
            @click="centerOnUserLocation" title="Center on my location" :disabled="isLocating">
            <span v-if="isLocating" class="spinner"></span>
            <svg v-else-if="isCentered" class="svg-icon" fill="currentColor" version="1.1"
                xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                viewBox="0 0 20.1513 18.398">
                <g>
                    <rect height="18.398" opacity="0" width="20.1513" x="0" y="0" />
                    <path
                        d="M1.29145 9.82891L8.41059 9.8582C8.55707 9.8582 8.6059 9.90703 8.6059 10.0535L8.62543 17.1141C8.62543 18.5691 10.3735 18.9109 11.0278 17.4949L18.2446 1.97734C18.8989 0.551563 17.7758-0.385937 16.4086 0.248829L0.803167 7.48516C-0.446833 8.06133-0.202692 9.81914 1.29145 9.82891Z"
                        fill-opacity="0.85" />
                </g>
            </svg>
            <svg v-else class="svg-icon" fill="none" stroke="currentColor" stroke-width=".7" version="1.1"
                xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                viewBox="0 0 20.2703 18.4783">
                <g>
                    <rect height="18.4783" opacity="0" width="20.2703" x="0" y="0" />
                    <path
                        d="M0.833401 7.47647C-0.514255 8.10147-0.143161 9.90811 1.34121 9.91787L8.46035 9.94717C8.57754 9.94717 8.60684 9.97647 8.60684 10.0937L8.62637 17.1542C8.63614 18.6972 10.4721 18.9706 11.1264 17.5546L18.3432 2.03701C19.0072 0.591702 17.8744-0.433689 16.4389 0.240139ZM2.53262 8.39444C2.49356 8.39444 2.48379 8.35537 2.53262 8.33584L16.5658 1.91006C16.6342 1.88076 16.6635 1.9003 16.6342 1.97842L10.1693 16.0019C10.1596 16.0409 10.1205 16.0312 10.1205 15.9921L10.1693 9.0878C10.1693 8.65811 9.8666 8.35537 9.42715 8.35537Z"
                        fill-opacity="0.85" />
                </g>
            </svg>
        </button>
    </div>
</template>

<style scoped>
.map-wrapper {
    width: 100vw;
    height: 100vh;
    position: absolute;
    inset: 0;
    margin: 0;
    padding: 0;
    overflow: hidden;
}

.map-container {
    width: 100%;
    height: 100%;
    border: none !important;
    outline: none !important;
}

.btn-center-location {
    position: absolute;
    top: max(16px, env(safe-area-inset-top));
    right: max(16px, env(safe-area-inset-right));
    z-index: 1000;
    width: 44px;
    height: 44px;
    background: rgba(255, 255, 255, 0.9);
    backdrop-filter: blur(10px);
    -webkit-backdrop-filter: blur(10px);
    border: none;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    box-shadow: 0 4px 14px rgba(0, 0, 0, 0.15);
    transition: background-color 0.2s, transform 0.1s, color 0.2s;
    color: #007aff;
    /* Apple System Blue */
}

.btn-center-location:hover {
    background-color: #ffffff;
}

.btn-center-location:active {
    transform: scale(0.92);
}

.svg-icon {
    width: 20px;
    height: 20px;
    fill: currentColor;
}

.spinner {
    width: 16px;
    height: 16px;
    border: 2px solid #cbd5e1;
    border-top-color: #007aff;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}

@keyframes spin {
    to {
        transform: rotate(360deg);
    }
}

/* Remove Leaflet focus outline rings */
:deep(.leaflet-container) {
    border: none !important;
    outline: none !important;
    background: transparent;
}
</style>

<!-- Custom Leaflet Pulsing Location Marker -->
<style>
.user-location-marker {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
}

.user-dot {
    width: 12px;
    height: 12px;
    background-color: #007aff;
    border: 2px solid #ffffff;
    border-radius: 50%;
    box-shadow: 0 0 4px rgba(0, 0, 0, 0.3);
    z-index: 2;
}

.pulse-ring {
    position: absolute;
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background-color: rgba(0, 122, 255, 0.3);
    animation: pulse 2s infinite;
    z-index: 1;
}

@keyframes pulse {
    0% {
        transform: scale(0.5);
        opacity: 1;
    }

    100% {
        transform: scale(1.8);
        opacity: 0;
    }
}
</style>