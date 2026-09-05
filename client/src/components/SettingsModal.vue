<script setup lang="ts">
import { ref } from 'vue';

import { useLocationStore } from '../services/location';

const emit = defineEmits(['close']);
const locationStore = useLocationStore();

const apiBaseUrlInput = ref(locationStore.apiBaseUrl);
const userSigningKey = ref(locationStore.userSigningKey);
const showSigningKey = ref(false);
const showCopyNotification = ref(false);

function copyKeyToClipboard() {
    if (!userSigningKey.value) return;
    navigator.clipboard.writeText(userSigningKey.value);
    showCopyNotification.value = true;
    setTimeout(() => {
        showCopyNotification.value = false;
    }, 2000);
}

function saveSettings() {
    try {
        if (apiBaseUrlInput.value) {
            locationStore.configureServerUrl(apiBaseUrlInput.value);
        }
        emit('close');
    } catch (err: any) {
        alert(err.message || 'Failed to save settings.');
    }
}

function resetDeviceAuth() {
    if (confirm('Are you sure you want to disconnect this device? This will clear your device token and bring up onboarding.')) {
        locationStore.setDeviceToken('');
        localStorage.removeItem('deviceToken');
        locationStore.isConfigured = false;
        emit('close');
    }
}
</script>

<template>
    <div class="modal-overlay" @click.self="emit('close')">
        <div class="modal-card">
            <div class="modal-header">
                <h3>App & Security Settings</h3>
                <button class="close-btn" @click="emit('close')">&times;</button>
            </div>

            <div class="modal-body">
                <!-- User & Device Identity Section -->
                <section class="setting-section">
                    <h4>User & Device Identity</h4>

                    <div class="form-group">
                        <label>Device ID</label>
                        <input type="text" :value="locationStore.deviceToken || 'No active device token issued'"
                            readonly class="readonly-input" />
                        <small>Issued directly by the server administrator.</small>
                    </div>

                    <div class="form-group">
                        <label>Signing Key</label>
                        <div class="input-with-button">
                            <input :type="showSigningKey ? 'text' : 'password'" :value="userSigningKey || 'No key set'"
                                readonly class="readonly-input" />
                            <button type="button" @click="showSigningKey = !showSigningKey" class="btn-secondary">
                                {{ showSigningKey ? 'Hide' : 'Show' }}
                            </button>
                            <button type="button" @click="copyKeyToClipboard" class="btn-secondary"
                                :disabled="!userSigningKey">
                                Copy
                            </button>
                        </div>
                        <p v-if="showCopyNotification" class="toast-text">Copied to clipboard!</p>
                        <small class="warning-text">
                            <strong>Important:</strong> Treat this key like a password. Save it in a secure location to
                            restore your account access on a new device.
                        </small>
                    </div>
                </section>

                <hr />

                <!-- Transparent Group Key Management List -->
                <section class="setting-section">
                    <h4>Joined Groups & Encryption Status</h4>
                    <p class="section-desc">
                        End-to-end group encryption keys are managed transparently. Keys update automatically when group
                        members join or leave.
                    </p>

                    <div v-if="Object.keys(locationStore.groupKeys).length === 0" class="empty-state">
                        No active group keys found. Group memberships will sync automatically upon joining.
                    </div>

                    <div v-else class="group-list">
                        <div v-for="(config, groupId) in locationStore.groupKeys" :key="groupId" class="group-item">
                            <div class="group-info">
                                <span class="group-id">{{ groupId }}</span>
                                <span class="key-version">Key Version: v{{ config.keyVersion }}</span>
                            </div>
                            <div class="group-status">
                                <span class="status-badge active">Encrypted (AES-GCM)</span>
                            </div>
                        </div>
                    </div>
                </section>

                <hr />

                <!-- Server API Settings -->
                <section class="setting-section">
                    <h4>Server Connection</h4>
                    <div class="form-group">
                        <label>Server API URL</label>
                        <input type="url" v-model="apiBaseUrlInput" placeholder="http://localhost:5180" />
                    </div>
                </section>
            </div>

            <div class="modal-footer">
                <button type="button" class="btn-danger" @click="resetDeviceAuth">Reset / Disconnect Device</button>
                <div class="footer-actions">
                    <button type="button" class="btn-cancel" @click="emit('close')">Cancel</button>
                    <button type="button" class="btn-primary" @click="saveSettings">Save Configuration</button>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.modal-overlay,
.modal-card,
input,
button,
label {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    box-sizing: border-box;
}

.modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(15, 23, 42, 0.6);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 3000;
}

.modal-card {
    background: #ffffff;
    width: 90%;
    max-width: 520px;
    max-height: 90vh;
    border-radius: 1rem;
    display: flex;
    flex-direction: column;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
}

.modal-header {
    padding: 1.25rem 1.5rem;
    border-bottom: 1px solid #e2e8f0;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.modal-header h3 {
    margin: 0;
    font-size: 1.15rem;
    font-weight: 600;
    color: #0f172a;
}

.close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #64748b;
    cursor: pointer;
}

.modal-body {
    padding: 1.5rem;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

.setting-section h4 {
    margin: 0 0 0.5rem 0;
    font-size: 0.95rem;
    font-weight: 600;
    color: #1e293b;
}

.section-desc {
    font-size: 0.8rem;
    color: #64748b;
    margin-bottom: 0.75rem;
    line-height: 1.4;
}

.form-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    margin-bottom: 0.75rem;
}

.form-group label {
    font-size: 0.8rem;
    font-weight: 600;
    color: #334155;
}

.form-group input {
    padding: 0.6rem;
    border: 1px solid #cbd5e1;
    border-radius: 0.5rem;
    font-size: 0.875rem;
}

.readonly-input {
    background-color: #f8fafc;
    color: #64748b;
}

.input-with-button {
    display: flex;
    gap: 0.5rem;
}

.input-with-button input {
    flex: 1;
}

.btn-secondary {
    padding: 0 0.75rem;
    background: #e2e8f0;
    border: none;
    border-radius: 0.5rem;
    font-size: 0.8rem;
    font-weight: 600;
    color: #334155;
    cursor: pointer;
}

.btn-secondary:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}

.warning-text {
    color: #b45309;
    font-size: 0.75rem;
    line-height: 1.3;
    margin-top: 0.25rem;
}

.toast-text {
    color: #16a34a;
    font-size: 0.75rem;
    margin: 0;
}

.empty-state {
    font-size: 0.8rem;
    color: #94a3b8;
    font-style: italic;
    background: #f8fafc;
    padding: 0.75rem;
    border-radius: 0.5rem;
    text-align: center;
}

.group-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.group-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.75rem;
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 0.5rem;
}

.group-info {
    display: flex;
    flex-direction: column;
}

.group-id {
    font-weight: 600;
    font-size: 0.85rem;
    color: #0f172a;
}

.key-version {
    font-size: 0.75rem;
    color: #64748b;
}

.status-badge {
    font-size: 0.7rem;
    padding: 0.25rem 0.5rem;
    border-radius: 1rem;
    font-weight: 600;
}

.status-badge.active {
    background: #dcfce7;
    color: #15803d;
}

hr {
    border: none;
    border-top: 1px solid #e2e8f0;
    margin: 0.25rem 0;
}

.modal-footer {
    padding: 1rem 1.5rem;
    border-top: 1px solid #e2e8f0;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.footer-actions {
    display: flex;
    gap: 0.75rem;
}

.btn-danger {
    padding: 0.6rem 0.85rem;
    background: #fef2f2;
    border: 1px solid #fecaca;
    border-radius: 0.5rem;
    color: #dc2626;
    font-weight: 600;
    font-size: 0.8rem;
    cursor: pointer;
}

.btn-danger:hover {
    background: #fee2e2;
}

.btn-cancel {
    padding: 0.6rem 1rem;
    background: #f1f5f9;
    border: none;
    border-radius: 0.5rem;
    color: #475569;
    font-weight: 600;
    cursor: pointer;
}

.btn-primary {
    padding: 0.6rem 1rem;
    background: #2563eb;
    border: none;
    border-radius: 0.5rem;
    color: white;
    font-weight: 600;
    cursor: pointer;
}
</style>