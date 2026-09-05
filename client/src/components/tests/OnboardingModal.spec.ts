import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { describe, beforeEach, it, expect } from 'vitest';

import OnboardingModal from '../OnboardingModal.vue';
import { useLocationStore } from '../../services/location';

describe('OnboardingModal.vue Validation', () => {
    beforeEach(() => {
        setActivePinia(createPinia());
        localStorage.clear();
    });

    it('displays an error if the signing key is not a 32-byte (64 hex characters) string', async () => {
        const wrapper = mount(OnboardingModal);

        // Fill in valid server URL and device token
        await wrapper.find('input#serverUrl').setValue('http://localhost:5180');
        await wrapper.find('input#deviceToken').setValue('valid-device-token-123');

        // Set an invalid/too-short signing key (e.g. 10 chars instead of 64)
        await wrapper.find('input#signingKey').setValue('shortkey123');

        // Submit the form
        await wrapper.find('form').trigger('submit.prevent');

        // Assert error message appears and store is NOT configured
        const errorText = wrapper.find('p.error').text();
        expect(errorText).toBe('The Signing Key must be a valid 32-byte hex string (64 characters).');

        const store = useLocationStore();
        expect(store.isConfigured).toBe(false);
    });

    it('successfully completes onboarding when a valid 64-character key is provided', async () => {
        const wrapper = mount(OnboardingModal);
        const valid32ByteHex = 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2';

        await wrapper.find('input#serverUrl').setValue('http://localhost:5180');
        await wrapper.find('input#deviceToken').setValue('valid-device-token-123');
        await wrapper.find('input#signingKey').setValue(valid32ByteHex);

        await wrapper.find('form').trigger('submit.prevent');

        expect(wrapper.find('p.error').exists()).toBe(false);

        const store = useLocationStore();
        expect(store.userSigningKey).toBe(valid32ByteHex);
        expect(store.deviceToken).toBe('valid-device-token-123');
        expect(store.isConfigured).toBe(true);
    });
});