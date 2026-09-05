<script setup lang="ts">
import { ref } from 'vue';

import { useLocationStore } from '../services/location';
import { generateHexPassphrase } from '../services/random';

const emit = defineEmits(['close', 'created']);
const locationStore = useLocationStore();

const targetDeviceId = ref('');
const groupName = ref('');
const groupPsk = ref('');

const isSubmitting = ref(false);
const errorMessage = ref('');
const copySuccess = ref(false);

/**
 * Generates a 64-character hex PSK using random.ts
 */
function handleAutoGenerate() {
    groupPsk.value = generateHexPassphrase();
}

/**
 * Copies the current device token to clipboard.
 */
async function copyDeviceId() {
    try {
        await navigator.clipboard.writeText(locationStore.deviceToken);
        copySuccess.value = true;
        setTimeout(() => {
            copySuccess.value = false;
        }, 2000);
    } catch (err) {
        console.error('Failed to copy device ID:', err);
    }
}

/**
 * Handles group creation & direct key association.
 */
async function handleShare() {
    errorMessage.value = '';

    if (!targetDeviceId.value.trim()) {
        errorMessage.value = 'Target Device ID is required.';
        return;
    }

    if (!groupPsk.value.trim()) {
        errorMessage.value = 'Please specify or generate a Group PSK for end-to-end encryption.';
        return;
    }

    isSubmitting.value = true;

    try {
        // Creates group on backend and saves group PSK locally at version 1
        const groupData = await locationStore.shareLocationWithDevice(
            targetDeviceId.value.trim(),
            groupName.value.trim(),
            groupPsk.value.trim()
        );

        emit('created', groupData);
        emit('close');
    } catch (err: any) {
        errorMessage.value = err.message || 'Failed to establish group share.';
    } finally {
        isSubmitting.value = false;
    }
}
</script>

<template>
    <div class="modal-backdrop" @click.self="emit('close')">
        <div class="modal-card">
            <div class="modal-header">
                <h3>Share Location with Device</h3>
                <button class="btn-close" @click="emit('close')">&times;</button>
            </div>

            <div class="modal-body">
                <!-- Display Current Device ID -->
                <div class="my-device-box">
                    <div class="device-label-row">
                        <span class="label">Your Device Token:</span>
                        <span v-if="copySuccess" class="copy-hint">Copied!</span>
                    </div>
                    <div class="code-row">
                        <code>{{ locationStore.deviceToken || 'Not generated' }}</code>
                        <button class="btn-copy" type="button" @click="copyDeviceId">Copy</button>
                    </div>
                </div>

                <hr class="divider" />

                <form @submit.prevent="handleShare">
                    <!-- Target Device Input -->
                    <div class="form-group">
                        <label for="targetDevice">Target Device ID / Token</label>
                        <input id="targetDevice" v-model="targetDeviceId" type="text"
                            placeholder="e.g., dev_xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" required />
                    </div>

                    <!-- Group Name Input -->
                    <div class="form-group">
                        <label for="groupName">Group / Partner Name (Optional)</label>
                        <input id="groupName" v-model="groupName" type="text" placeholder="e.g., Alice's Phone" />
                    </div>

                    <!-- Group PSK Input -->
                    <div class="form-group">
                        <label for="groupPsk">Group Encryption Key (PSK)</label>
                        <div class="input-with-button">
                            <input id="groupPsk" v-model="groupPsk" type="text"
                                placeholder="Shared 64-char key for this group" required />
                            <button class="btn-secondary" type="button" @click="handleAutoGenerate">
                                Auto-Generate
                            </button>
                        </div>
                        <small class="hint">Both devices must share this key to decrypt each other's location.</small>
                    </div>

                    <p v-if="errorMessage" class="alert-error">{{ errorMessage }}</p>

                    <div class="modal-footer">
                        <button class="btn-cancel" type="button" @click="emit('close')">Cancel</button>
                        <button class="btn-primary" type="submit" :disabled="isSubmitting">
                            {{ isSubmitting ? 'Connecting...' : 'Start Sharing' }}
                        </button>
                    </div>
                </form>
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
    background-color: rgba(15, 23, 42, 0.4);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 2000;
}

.modal-card {
    background: #ffffff;
    width: 92%;
    max-width: 460px;
    border-radius: 1rem;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", sans-serif;
}

.modal-header {
    padding: 1.25rem 1.5rem;
    border-bottom: 1px solid #f1f5f9;
    display: flex;
    align-items: center;
    justify-content: space-between;
}

.modal-header h3 {
    margin: 0;
    font-size: 1.1rem;
    font-weight: 700;
    color: #0f172a;
}

.btn-close {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #94a3b8;
    cursor: pointer;
}

.modal-body {
    padding: 1.25rem 1.5rem;
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

.my-device-box {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    padding: 0.75rem 1rem;
    border-radius: 0.625rem;
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
}

.device-label-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.device-label-row .label {
    font-size: 0.75rem;
    font-weight: 600;
    color: #64748b;
}

.copy-hint {
    font-size: 0.7rem;
    color: #16a34a;
    font-weight: 600;
}

.code-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

code {
    font-family: monospace;
    font-size: 0.8rem;
    color: #0f172a;
    background: #e2e8f0;
    padding: 0.25rem 0.5rem;
    border-radius: 0.375rem;
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.btn-copy {
    background: #ffffff;
    border: 1px solid #cbd5e1;
    padding: 0.25rem 0.6rem;
    border-radius: 0.375rem;
    font-size: 0.75rem;
    font-weight: 600;
    cursor: pointer;
}

.divider {
    border: none;
    border-top: 1px solid #f1f5f9;
    margin: 0;
}

.form-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    margin-bottom: 0.85rem;
}

.form-group label {
    font-size: 0.8rem;
    font-weight: 600;
    color: #334155;
}

.form-group input {
    padding: 0.6rem 0.75rem;
    border: 1px solid #cbd5e1;
    border-radius: 0.5rem;
    font-size: 0.85rem;
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
    border-radius: 0.5rem;
    font-size: 0.75rem;
    font-weight: 600;
    cursor: pointer;
    white-space: nowrap;
}

.hint {
    font-size: 0.7rem;
    color: #64748b;
}

.alert-error {
    color: #dc2626;
    font-size: 0.8rem;
    margin: 0 0 0.5rem 0;
}

.modal-footer {
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
    margin-top: 0.5rem;
}

.btn-cancel {
    background: transparent;
    border: 1px solid #cbd5e1;
    padding: 0.5rem 1rem;
    border-radius: 0.5rem;
    font-size: 0.85rem;
    font-weight: 600;
    color: #475569;
    cursor: pointer;
}

.btn-primary {
    background: #2563eb;
    color: #ffffff;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 0.5rem;
    font-size: 0.85rem;
    font-weight: 600;
    cursor: pointer;
}

.btn-primary:disabled {
    background: #93c5fd;
    cursor: not-allowed;
}
</style>