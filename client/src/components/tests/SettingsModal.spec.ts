import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { describe, beforeEach, it, expect, vi } from 'vitest';

import SettingsModal from '../SettingsModal.vue';
import { useLocationStore } from '../../services/location';

describe('SettingsModal.vue', () => {
    beforeEach(() => {
        setActivePinia(createPinia());
    });

    it('renders device token and signing key as read-only inputs', () => {
        const store = useLocationStore();
        store.setDeviceToken('token-123');
        store.setUserSigningKey('a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2');

        const wrapper = mount(SettingsModal);
        const readonlyInputs = wrapper.findAll('input.readonly-input');

        expect(readonlyInputs).toHaveLength(2);
        expect((readonlyInputs[0].element as HTMLInputElement).value).toBe('token-123');
        expect((readonlyInputs[1].element as HTMLInputElement).value).toBe(
            'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2'
        );
    });

    it('triggers resetDeviceAuth when disconnect button is clicked', async () => {
        const store = useLocationStore();
        store.setDeviceToken('token-123');
        vi.spyOn(window, 'confirm').mockReturnValue(true);

        const wrapper = mount(SettingsModal);
        const disconnectBtn = wrapper.find('button.btn-danger');

        await disconnectBtn.trigger('click');

        expect(store.deviceToken).toBe('');
        expect(localStorage.getItem('deviceToken')).toBeNull();
        expect(wrapper.emitted('close')).toBeTruthy();
    });
});