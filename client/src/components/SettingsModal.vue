<script setup lang="ts">
import { ref } from 'vue';

import { useLocationStore } from '../services/location';
import { generateHexPassphrase } from '../services/random';

const emit = defineEmits(['close']);
const locationStore = useLocationStore();

// Local form fields bound to current store values
const deviceTokenInput = ref(locationStore.deviceToken);
const pskPassphraseInput = ref(locationStore.pskPassphrase);
const apiBaseUrlInput = ref(locationStore.apiBaseUrl);

const showPassphrase = ref(false);
const saveSuccess = ref(false);

/**
 * Generates a new 32-byte (64-char hex) PSK passphrase using the hardware RNG.
 */
function handleGeneratePsk() {
    pskPassphraseInput.value = generateHexPassphrase();
}

/**
 * Saves input values into Pinia reactive state and localStorage.
 */
function handleSave() {
    locationStore.setDeviceToken(deviceTokenInput.value.trim());
    locationStore.setPskPassphrase(pskPassphraseInput.value.trim());

    if (apiBaseUrlInput.value.trim()) {
        locationStore.apiBaseUrl = apiBaseUrlInput.value.trim();
        localStorage.setItem('apiBaseUrl', apiBaseUrlInput.value.trim());
    }

    saveSuccess.value = true;
    setTimeout(() => {
        saveSuccess.value = false;
        emit('close');
    }, 1000);
}

function handleClose() {
    emit('close');
}
</script>

<template>
    <div class="modal-backdrop" @click.self="handleClose">
        <div class="modal-card">
            <div class="modal-header">
                <h3>SelfPin Settings</h3>
                <button class="btn-close" @click="handleClose">&times;</button>
            </div>

            <div class="modal-body">
                <!-- Device Token Input -->
                <div class="form-group">
                    <label for="deviceToken">Device Token</label>
                    <input id="deviceToken" v-model="deviceTokenInput" type="text"
                        placeholder="Paste your X-Device-Token string" />
                    <small>Issued by your server administrator via <code>/admin/users</code>.</small>
                </div>

                <!-- PSK Passphrase Input -->
                <div class="form-group">
                    <label for="pskPassphrase">Family Shared Secret (PSK)</label>
                    <div class="input-with-button">
                        <input id="pskPassphrase" v-model="pskPassphraseInput"
                            :type="showPassphrase ? 'text' : 'password'"
                            placeholder="32-byte / 64-character Hex string" />
                        <button class="btn-secondary" type="button" @click="showPassphrase = !showPassphrase">
                            {{ showPassphrase ? 'Hide' : 'Show' }}
                        </button>
                    </div>
                    <button class="btn-link" type="button" @click="handleGeneratePsk">
                        🎲 Generate Random 32-Byte Key
                    </button>
                </div>

                <!-- API Base URL Input -->
                <div class="form-group">
                    <label for="apiBaseUrl">Server API URL</label>
                    <input id="apiBaseUrl" v-model="apiBaseUrlInput" type="url" placeholder="http://localhost:5180" />
                </div>

                <div v-if="saveSuccess" class="alert-success">
                    ✓ Configuration saved successfully!
                </div>
            </div>

            <div class="modal-footer">
                <button class="btn-cancel" @click="handleClose">Cancel</button>
                <button class="btn-save" @click="handleSave">Save Configuration</button>
            </div>
        </div>
    </div>
</template>

<style scoped>
.modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 2000;
}

.modal-card {
    background: #ffffff;
    width: 90%;
    max-width: 480px;
    border-radius: 0.75rem;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    font-family: system-ui, -apple-system, sans-serif;
}

.modal-header {
    padding: 1rem 1.25rem;
    border-bottom: 1px solid #e2e8f0;
    display: flex;
    align-items: center;
    justify-content: space-between;
}

.modal-header h3 {
    margin: 0;
    font-size: 1.125rem;
    color: #1e293b;
}

.btn-close {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #64748b;
    cursor: pointer;
}

.modal-body {
    padding: 1.25rem;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
}

.form-group {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
}

.form-group label {
    font-size: 0.875rem;
    font-weight: 600;
    color: #334155;
}

.form-group input {
    padding: 0.625rem 0.75rem;
    border: 1px solid #cbd5e1;
    border-radius: 0.375rem;
    font-size: 0.875rem;
}

.form-group small {
    font-size: 0.75rem;
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
    background: #f1f5f9;
    border: 1px solid #cbd5e1;
    padding: 0 0.75rem;
    border-radius: 0.375rem;
    font-size: 0.75rem;
    cursor: pointer;
}

.btn-link {
    background: none;
    border: none;
    color: #2563eb;
    font-size: 0.75rem;
    text-align: left;
    padding: 0;
    margin-top: 0.25rem;
    cursor: pointer;
    text-decoration: underline;
}

.alert-success {
    background: #dcfce7;
    color: #15803d;
    padding: 0.625rem;
    border-radius: 0.375rem;
    font-size: 0.875rem;
    text-align: center;
}

.modal-footer {
    padding: 1rem 1.25rem;
    background: #f8fafc;
    border-top: 1px solid #e2e8f0;
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
}

.btn-cancel {
    background: transparent;
    border: 1px solid #cbd5e1;
    padding: 0.5rem 1rem;
    border-radius: 0.375rem;
    font-size: 0.875rem;
    cursor: pointer;
}

.btn-save {
    background: #2563eb;
    color: #ffffff;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 0.375rem;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
}
</style>