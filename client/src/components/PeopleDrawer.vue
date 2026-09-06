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
                <button class="btn-add sf-icon" title="Share Location" @click="$emit('openShare')">􀅼</button>
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
                    <span class="sf-icon">􀍟</span> Manage App Settings
                </button>
            </div>
        </div>

        <!-- Bottom Navigation Tabs -->
        <nav class="bottom-tab-bar">
            <button class="tab-button" :class="{ active: activeTab === 'people' }"
                @click="activeTab = 'people'; isExpanded = true">
                <span class="tab-icon sf-icon">􀝋</span>
                <span class="tab-label">People</span>
            </button>

            <button class="tab-button" :class="{ active: activeTab === 'me' }"
                @click="activeTab = 'me'; isExpanded = true">
                <span class="tab-icon sf-icon">􃂈</span>
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

/* SF Symbol Helper Class */
.sf-icon {
    font-family: -apple-system, SF Pro Text, SF Pro Icons, "SF Pro", system-ui, sans-serif !important;
    font-weight: 500;
    line-height: 1;
    -webkit-font-smoothing: antialiased;
}
</style>