<script setup lang="ts">
import { ref } from 'vue';

import { useLocationStore } from '../services/location';

const emit = defineEmits(['close', 'created']);
const locationStore = useLocationStore();

const targetSharingId = ref('');
const connectionName = ref('');

const isSubmitting = ref(false);
const errorMessage = ref('');
const copySuccess = ref(false);

/**
 * Copies the current user's personal sharing ID to the clipboard.
 */
async function copySharingId() {
    try {
        await navigator.clipboard.writeText(locationStore.deviceToken);
        copySuccess.value = true;
        setTimeout(() => {
            copySuccess.value = false;
        }, 2000);
    } catch (err) {
        console.error('Failed to copy sharing ID:', err);
    }
}

/**
 * Connects with a friend or family member using their sharing ID.
 */
async function handleConnect() {
    errorMessage.value = '';

    if (!targetSharingId.value.trim()) {
        errorMessage.value = 'Please enter a friend\'s sharing ID.';
        return;
    }

    isSubmitting.value = true;

    try {
        const groupData = await locationStore.shareLocationWithDevice(
            targetSharingId.value.trim(),
            connectionName.value.trim()
        );

        emit('created', groupData);
        emit('close');
    } catch (err: any) {
        errorMessage.value = err.message || 'Failed to connect with member.';
    } finally {
        isSubmitting.value = false;
    }
}
</script>

<template>
    <div class="modal-overlay" @click.self="emit('close')">
        <div class="modal-card">
            <div class="modal-header">
                <h3>Share Location</h3>
                <button class="close-btn" type="button" @click="emit('close')">&times;</button>
            </div>

            <form @submit.prevent="handleConnect" class="modal-body">
                <!-- Display Current User's ID -->
                <div class="form-group readonly-group">
                    <div class="label-row">
                        <label>Your Sharing ID:</label>
                        <span v-if="copySuccess" class="copy-hint">Copied!</span>
                    </div>
                    <div class="input-with-button">
                        <input type="text" :value="locationStore.deviceToken || 'Not generated'" readonly
                            class="readonly-input" />
                        <button type="button" @click="copySharingId" class="btn-secondary">Copy</button>
                    </div>
                </div>

                <hr class="divider" />

                <!-- Target Member ID -->
                <div class="form-group">
                    <label for="targetId">Friend's Sharing ID</label>
                    <input id="targetId" v-model="targetSharingId" type="text" placeholder="Paste their ID here"
                        required />
                </div>

                <!-- Optional Display Name -->
                <div class="form-group">
                    <label for="connectionName">Name or Label (Optional)</label>
                    <input id="connectionName" v-model="connectionName" type="text"
                        placeholder="e.g., Mom or Family Circle" />
                </div>

                <p v-if="errorMessage" class="alert-error">{{ errorMessage }}</p>

                <div class="modal-footer">
                    <button type="button" class="btn-cancel" @click="emit('close')">Cancel</button>
                    <button type="submit" class="btn-primary" :disabled="isSubmitting">
                        {{ isSubmitting ? 'Connecting...' : 'Start Sharing' }}
                    </button>
                </div>
            </form>
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
    padding-top: max(1rem, env(safe-area-inset-top));
    padding-bottom: max(1rem, env(safe-area-inset-bottom));
    padding-left: max(1rem, env(safe-area-inset-left));
    padding-right: max(1rem, env(safe-area-inset-right));
}

.modal-card {
    background: #ffffff;
    width: 100%;
    max-width: 460px;
    max-height: calc(100vh - env(safe-area-inset-top) - env(safe-area-inset-bottom) - 2rem);
    border-radius: 1.25rem;
    display: flex;
    flex-direction: column;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
    overflow: hidden;
}

.modal-header {
    padding: 1.25rem 1.25rem 1rem 1.25rem;
    border-bottom: 1px solid #e2e8f0;
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-shrink: 0;
}

.modal-header h3 {
    margin: 0;
    font-size: 1.1rem;
    font-weight: 600;
    color: #0f172a;
}

.close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #64748b;
    cursor: pointer;
    padding: 0;
    line-height: 1;
}

.modal-body {
    padding: 1.25rem;
    overflow-y: auto;
    -webkit-overflow-scrolling: touch;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    flex: 1;
}

.form-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
}

.label-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.form-group label {
    font-size: 0.8rem;
    font-weight: 600;
    color: #334155;
}

.copy-hint {
    font-size: 0.75rem;
    color: #16a34a;
    font-weight: 600;
}

.form-group input {
    padding: 0.65rem;
    border: 1px solid #cbd5e1;
    border-radius: 0.5rem;
    font-size: 16px !important;
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
    min-width: 0;
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
    white-space: nowrap;
}

.divider {
    border: none;
    border-top: 1px solid #f1f5f9;
    margin: 0.25rem 0;
}

.alert-error {
    color: #dc2626;
    font-size: 0.8rem;
    margin: 0;
}

.modal-footer {
    padding: 1rem 1.25rem;
    border-top: 1px solid #e2e8f0;
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
    background: #ffffff;
    flex-shrink: 0;
}

.btn-cancel,
.btn-primary {
    padding: 0.7rem 1rem;
    border-radius: 0.5rem;
    font-weight: 600;
    font-size: 0.85rem;
    cursor: pointer;
    white-space: nowrap;
}

.btn-cancel {
    background: #f1f5f9;
    border: none;
    color: #475569;
}

.btn-primary {
    background: #2563eb;
    border: none;
    color: white;
}

.btn-primary:disabled {
    background: #93c5fd;
    cursor: not-allowed;
}
</style>