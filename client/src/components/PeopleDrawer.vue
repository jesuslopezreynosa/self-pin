<script setup lang="ts">
import { ref } from 'vue';

import { useLocationStore } from '../services/location';

const emit = defineEmits(['openSettings', 'openShare']);
const locationStore = useLocationStore();

const activeTab = ref<'people' | 'me'>('people');
const isExpanded = ref(true);

function formatTime(isoString: string) {
    if (!isoString) return 'Unknown';
    return new Date(isoString).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}
</script>

<template>
    <div class="findmy-drawer" :class="{ 'is-collapsed': !isExpanded }">
        <!-- Drag / Collapse Handle -->
        <div class="drawer-handle" @click="isExpanded = !isExpanded">
            <div class="handle-bar"></div>
        </div>

        <!-- Tab 1: People & Groups View -->
        <div v-if="activeTab === 'people'" class="tab-content">
            <div class="drawer-header">
                <h2>People</h2>
                <button class="btn-add sf-icon" title="Share Location" @click="$emit('openShare')">
                    <svg version="1.1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                        viewBox="0 0 16.4746 16.123" width="14" height="14" fill="currentColor">
                        <g>
                            <rect height="16.123" opacity="0" width="16.4746" x="0" y="0" />
                            <path
                                d="M8.93555 15.2441L8.93555 0.869141C8.93555 0.400391 8.53516 0 8.05664 0C7.57812 0 7.1875 0.400391 7.1875 0.869141L7.1875 15.2441C7.1875 15.7129 7.57812 16.1133 8.05664 16.1133C8.53516 16.1133 8.93555 15.7129 8.93555 15.2441ZM0.869141 8.92578L15.2441 8.92578C15.7129 8.92578 16.1133 8.53516 16.1133 8.05664C16.1133 7.57812 15.7129 7.17773 15.2441 7.17773L0.869141 7.17773C0.400391 7.17773 0 7.57812 0 8.05664C0 8.53516 0.400391 8.92578 0.869141 8.92578Z"
                                fill="currentColor" fill-opacity="0.85" />
                        </g>
                    </svg>
                </button>
            </div>

            <div class="member-list">
                <div v-for="member in locationStore.familyFeed" :key="member.id" class="member-card">
                    <div class="avatar-ring">
                        <div class="avatar">{{ member.name.charAt(0).toUpperCase() }}</div>
                    </div>
                    <div class="member-info">
                        <div class="member-name">{{ member.name }}</div>
                        <div class="member-status">
                            <template v-if="member.location">
                                {{ member.location.latitude.toFixed(3) }}, {{ member.location.longitude.toFixed(3) }} •
                                {{ formatTime(member.lastUpdated) }}
                            </template>
                            <template v-else>
                                Location unavailable
                            </template>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Tab 2: Me / Settings View -->
        <div v-else-if="activeTab === 'me'" class="tab-content">
            <div class="drawer-header">
                <h2>Me</h2>
            </div>

            <div class="me-section">
                <div class="me-card">
                    <div class="me-info">
                        <span class="label">Sharing ID:</span>
                        <code class="token-display">{{ locationStore.deviceToken || 'Not Configured' }}</code>
                    </div>
                    <div class="me-info">
                        <span class="label">Server:</span>
                        <span class="value">{{ locationStore.apiBaseUrl }}</span>
                    </div>
                </div>

                <button class="btn-manage-settings" @click="emit('openSettings')">
                    <svg style=".svg-icon { width: 20px;height: 20px; }" fill="none" stroke="currentColor"
                        stroke-width=".7" version="1.1" xmlns="http://www.w3.org/2000/svg"
                        xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 20.7715 20.4199">
                        <g>
                            <rect height="20.4199" opacity="0" width="20.7715" x="0" y="0" />
                            <path
                                d="M9.30664 20.4102L11.1035 20.4102C11.6113 20.4102 11.9727 20.1074 12.0898 19.6094L12.5977 17.4609C12.9785 17.334 13.3496 17.1875 13.6719 17.0312L15.5566 18.1836C15.9766 18.4473 16.4551 18.4082 16.8066 18.0566L18.0664 16.8066C18.418 16.4551 18.4668 15.9473 18.1836 15.5273L17.0312 13.6621C17.1973 13.3203 17.3438 12.9688 17.4512 12.6172L19.6191 12.0996C20.1172 11.9824 20.4102 11.6211 20.4102 11.1133L20.4102 9.3457C20.4102 8.84766 20.1172 8.48633 19.6191 8.36914L17.4707 7.85156C17.3438 7.45117 17.1875 7.08984 17.0508 6.78711L18.2031 4.89258C18.4668 4.46289 18.4473 4.00391 18.0859 3.64258L16.8066 2.38281C16.4453 2.05078 16.0059 1.97266 15.5859 2.23633L13.6719 3.41797C13.3594 3.25195 12.998 3.10547 12.5977 2.97852L12.0898 0.800781C11.9727 0.302734 11.6113 0 11.1035 0L9.30664 0C8.79883 0 8.4375 0.302734 8.31055 0.800781L7.80273 2.95898C7.42188 3.08594 7.05078 3.23242 6.71875 3.4082L4.82422 2.23633C4.4043 1.97266 3.94531 2.03125 3.59375 2.38281L2.32422 3.64258C1.96289 4.00391 1.93359 4.46289 2.20703 4.89258L3.34961 6.78711C3.22266 7.08984 3.06641 7.45117 2.93945 7.85156L0.791016 8.36914C0.292969 8.48633 0 8.84766 0 9.3457L0 11.1133C0 11.6211 0.292969 11.9824 0.791016 12.0996L2.95898 12.6172C3.06641 12.9688 3.21289 13.3203 3.36914 13.6621L2.22656 15.5273C1.93359 15.9473 1.99219 16.4551 2.34375 16.8066L3.59375 18.0566C3.94531 18.4082 4.43359 18.4473 4.85352 18.1836L6.72852 17.0312C7.06055 17.1875 7.42188 17.334 7.80273 17.4609L8.31055 19.6094C8.4375 20.1074 8.79883 20.4102 9.30664 20.4102ZM10.2051 13.6523C8.30078 13.6523 6.75781 12.1094 6.75781 10.2051C6.75781 8.30078 8.30078 6.75781 10.2051 6.75781C12.1094 6.75781 13.6523 8.30078 13.6523 10.2051C13.6523 12.1094 12.1094 13.6523 10.2051 13.6523Z"
                                fill="white" fill-opacity="0.85" />
                        </g>
                    </svg> Manage App Settings
                </button>
            </div>
        </div>

        <!-- Bottom Navigation Tabs -->
        <nav class="bottom-tab-bar">
            <!-- Bottom Tabs: People (person.2.fill - U+1003A4) -->
            <button class="tab-button" :class="{ active: activeTab === 'people' }"
                @click="activeTab = 'people'; isExpanded = true">
                <svg class="tab-icon svg-icon" fill="currentColor" version="1.1" xmlns="http://www.w3.org/2000/svg"
                    xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 28.916 20.2051">
                    <g>
                        <rect height="20.2051" opacity="0" width="28.916" x="0" y="0" />
                        <path
                            d="M12.36 12.748C10.7377 14.1583 9.82422 15.9607 9.82422 17.5781C9.82422 18.0415 9.91799 18.4885 10.1417 18.877L2.68555 18.877C1.45508 18.877 0.966797 18.3887 0.966797 17.5C0.966797 14.8047 3.7207 11.5527 8.125 11.5527C9.79425 11.5527 11.2264 12.0199 12.36 12.748ZM11.5332 6.29883C11.5332 8.41797 9.95117 10.0586 8.13477 10.0586C6.30859 10.0586 4.72656 8.41797 4.72656 6.31836C4.72656 4.22852 6.31836 2.64648 8.13477 2.64648C9.94141 2.64648 11.5332 4.18945 11.5332 6.29883Z"
                            fill-opacity="0.85" />
                        <path
                            d="M19.375 9.83398C21.4746 9.83398 23.2812 7.95898 23.2812 5.51758C23.2812 3.10547 21.4648 1.31836 19.375 1.31836C17.2852 1.31836 15.4688 3.14453 15.4688 5.53711C15.4688 7.95898 17.2754 9.83398 19.375 9.83398ZM13.2324 18.877L25.5078 18.877C27.041 18.877 27.5879 18.4375 27.5879 17.5781C27.5879 15.0586 24.4336 11.582 19.3652 11.582C14.3066 11.582 11.1523 15.0586 11.1523 17.5781C11.1523 18.4375 11.6992 18.877 13.2324 18.877Z"
                            fill-opacity="0.85" />
                    </g>
                </svg>
                <span class="tab-label">People</span>
            </button>

            <!-- Bottom Tabs: Me (person.crop.circle.fill - U+1004A8) -->
            <button class="tab-button" :class="{ active: activeTab === 'me' }"
                @click="activeTab = 'me'; isExpanded = true">
                <svg class="tab-icon svg-icon" fill="currentColor" version="1.1" xmlns="http://www.w3.org/2000/svg"
                    xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 30.3516 22.8027">
                    <g>
                        <rect height="22.8027" opacity="0" width="30.3516" x="0" y="0" />
                        <path
                            d="M24.9609 11.3965C24.9609 16.8848 20.498 21.3574 15 21.3574C13.6121 21.3574 12.2892 21.0714 11.0881 20.5534C11.4167 20.1722 11.6984 19.7503 11.9213 19.2952C12.8956 19.6729 13.9461 19.873 14.9902 19.873C17.2363 19.873 19.4922 18.9551 20.9961 17.3633C19.9316 15.6836 17.6172 14.7266 14.9902 14.7266C14.0802 14.7266 13.2117 14.842 12.4228 15.0636C11.77 12.2903 9.26016 10.2051 6.29883 10.2051C5.88888 10.2051 5.48735 10.2453 5.0989 10.3246C5.63308 5.32951 9.87347 1.43555 15 1.43555C20.498 1.43555 24.9609 5.89844 24.9609 11.3965ZM11.6406 9.38477C11.6406 11.4844 13.1152 13.0469 14.9902 13.0664C16.875 13.0859 18.3398 11.4844 18.3398 9.38477C18.3398 7.41211 16.8652 5.77148 14.9902 5.77148C13.125 5.77148 11.6309 7.41211 11.6406 9.38477Z"
                            fill-opacity="0.85" />
                        <path
                            d="M11.2598 16.5039C11.2598 19.2188 8.98438 21.4648 6.29883 21.4648C3.58398 21.4648 1.33789 19.2383 1.33789 16.5039C1.33789 13.7891 3.58398 11.543 6.29883 11.543C9.02344 11.543 11.2598 13.7793 11.2598 16.5039ZM2.82227 16.5039C2.82227 16.9141 3.16406 17.2656 3.58398 17.2656C4.00391 17.2656 4.3457 16.9141 4.3457 16.5039C4.3457 16.0938 4.00391 15.7324 3.58398 15.7324C3.16406 15.7324 2.82227 16.0938 2.82227 16.5039ZM5.53711 16.5039C5.53711 16.9141 5.88867 17.2656 6.30859 17.2656C6.71875 17.2656 7.07031 16.9141 7.07031 16.5039C7.07031 16.0938 6.71875 15.7324 6.30859 15.7324C5.88867 15.7324 5.53711 16.0938 5.53711 16.5039ZM8.25195 16.5039C8.25195 16.9141 8.59375 17.2656 9.01367 17.2656C9.42383 17.2656 9.76562 16.9141 9.77539 16.5039C9.77539 16.0938 9.43359 15.7324 9.01367 15.7324C8.59375 15.7324 8.25195 16.0938 8.25195 16.5039Z"
                            fill-opacity="0.85" />
                    </g>
                </svg>
                <span class="tab-label">Me</span>
            </button>
        </nav>
    </div>
</template>

<style scoped>
/* 1. Reset font family across all elements inside the drawer */
.findmy-drawer,
.findmy-drawer *,
button,
h2,
span,
code,
div {
    font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "SF Pro Display", "Helvetica Neue", Helvetica, Arial, sans-serif !important;
    box-sizing: border-box;
}

.findmy-drawer {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    max-width: 440px;
    margin: 0 auto;
    background: rgba(255, 255, 255, 0.88);
    backdrop-filter: blur(25px);
    -webkit-backdrop-filter: blur(25px);
    border-top-left-radius: 1.5rem;
    border-top-right-radius: 1.5rem;
    box-shadow: 0 -8px 30px rgba(0, 0, 0, 0.12);
    z-index: 1000;
    transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1);

    /* Safe Area Padding for iOS Gesture Bar */
    padding-top: 0;
    padding-left: 1.25rem;
    padding-right: 1.25rem;
    padding-bottom: max(0.75rem, env(safe-area-inset-bottom));
}

.findmy-drawer.is-collapsed {
    transform: translateY(calc(100% - 70px));
}

.drawer-handle {
    width: 100%;
    height: 20px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
}

.handle-bar {
    width: 36px;
    height: 5px;
    background: #cbd5e1;
    border-radius: 3px;
}

.tab-content {
    min-height: 220px;
}

.drawer-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.75rem;
}

.drawer-header h2 {
    font-size: 1.5rem;
    font-weight: 700;
    color: #0f172a;
    margin: 0;
    letter-spacing: -0.02em;
}

.btn-add {
    background: #f1f5f9;
    border: none;
    border-radius: 50%;
    width: 32px;
    height: 32px;
    font-size: 1.2rem;
    font-weight: 600;
    color: #0f172a;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
}

.member-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    max-height: 220px;
    overflow-y: auto;
}

.member-card {
    display: flex;
    align-items: center;
    gap: 0.85rem;
    padding: 0.5rem 0;
    border-bottom: 1px solid rgba(226, 232, 240, 0.6);
}

.avatar-ring {
    width: 42px;
    height: 42px;
    border-radius: 50%;
    background: linear-gradient(135deg, #3b82f6, #1d4ed8);
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: 700;
}

.member-info {
    text-align: left;
}

.member-name {
    font-weight: 600;
    color: #0f172a;
    font-size: 0.95rem;
}

.member-status {
    font-size: 0.75rem;
    color: #64748b;
}

.me-section {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    margin-top: 0.5rem;
}

.me-card {
    background: rgba(241, 245, 249, 0.7);
    border-radius: 0.75rem;
    padding: 0.85rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    text-align: left;
}

.me-info {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 0.85rem;
}

.me-info .label {
    color: #64748b;
    font-weight: 500;
}

.me-info .value {
    color: #0f172a;
    font-weight: 600;
}

.token-display {
    font-size: 0.75rem;
    background: #e2e8f0;
    padding: 2px 6px;
    border-radius: 4px;
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.btn-manage-settings {
    width: 100%;
    padding: 0.75rem;
    background: #2563eb;
    color: white;
    border: none;
    border-radius: 0.625rem;
    font-weight: 600;
    font-size: 0.9rem;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.4rem;
}

/* Bottom Tab Navigation Styles */
.bottom-tab-bar {
    display: flex;
    justify-content: space-around;
    border-top: 1px solid rgba(226, 232, 240, 0.8);
    padding-top: 0.5rem;
    margin-top: 0.75rem;
}

.tab-button {
    background: none;
    border: none;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.2rem;
    cursor: pointer;
    color: #94a3b8;
    transition: color 0.2s;
    flex: 1;
}

.tab-button.active {
    color: #2563eb;
}

.tab-icon {
    font-size: 1.25rem;
}

.tab-label {
    font-size: 0.7rem;
    font-weight: 600;
}

.svg-icon {
    width: 35px;
    height: 35px;
    fill: currentColor;
}
</style>