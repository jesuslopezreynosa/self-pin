import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';

import { useLocationStore } from '../location';
import * as cryptoService from '../crypto';

// Global localStorage mock
const localStorageMock = (() => {
    let store: Record<string, string> = {};
    return {
        getItem: (key: string) => store[key] || null,
        setItem: (key: string, value: string) => { store[key] = value.toString(); },
        removeItem: (key: string) => { delete store[key]; },
        clear: () => { store = {}; }
    };
})();

Object.defineProperty(globalThis, 'localStorage', {
    value: localStorageMock
});

describe('Location Pinia Store', () => {
    beforeEach(() => {
        setActivePinia(createPinia());
        localStorage.clear();
        vi.clearAllMocks();
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    describe('Initialization & State', () => {
        it('should initialize with default values when localStorage is empty', () => {
            const store = useLocationStore();

            expect(store.deviceToken).toBe('');
            expect(store.pskPassphrase).toBe('');
            expect(store.currentKeyVersion).toBe(1);
            expect(store.isAuthenticated).toBe(false);
            expect(store.hasConfiguredPsk).toBe(false);
            expect(store.familyFeed).toEqual([]);
        });

        it('should set credentials and persist them to localStorage', () => {
            const store = useLocationStore();

            store.setDeviceToken('device-123');
            store.setPskPassphrase('secret-key-32-chars-long!', 2);

            expect(store.deviceToken).toBe('device-123');
            expect(store.pskPassphrase).toBe('secret-key-32-chars-long!');
            expect(store.currentKeyVersion).toBe(2);
            expect(store.isAuthenticated).toBe(true);
            expect(store.hasConfiguredPsk).toBe(true);

            expect(localStorage.getItem('deviceToken')).toBe('device-123');
            expect(localStorage.getItem('pskPassphrase')).toBe('secret-key-32-chars-long!');
            expect(localStorage.getItem('keyVersion')).toBe('2');
        });
    });

    describe('Publishing Location', () => {
        it('should set an error if client is unauthenticated', async () => {
            const store = useLocationStore();
            const coords = { latitude: 37.7749, longitude: -122.4194 };

            await store.publishLocation(coords);

            expect(store.error).toBe('Device token or PSK passphrase missing.');
        });

        it('should encrypt coords and POST them to the server when authenticated', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setPskPassphrase('secret-key-32-chars-long!');

            vi.spyOn(cryptoService, 'encryptLocationPayload').mockResolvedValue('encrypted-blob');

            const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true
            } as Response);

            const coords = { latitude: 37.7749, longitude: -122.4194 };
            await store.publishLocation(coords);

            expect(cryptoService.encryptLocationPayload).toHaveBeenCalledWith(coords, 'secret-key-32-chars-long!');
            expect(fetchSpy).toHaveBeenCalledWith(
                'http://localhost:5180/api/v1/location/update',
                expect.objectContaining({
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Device-Token': 'device-123'
                    },
                    body: JSON.stringify({
                        encryptedPayload: 'encrypted-blob',
                        keyVersion: 1
                    })
                })
            );
            expect(store.error).toBeNull();
        });
    });

    describe('Fetching and Decrypting Feed', () => {
        it('should fetch feed and decrypt entries successfully', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setPskPassphrase('secret-key-32-chars-long!');

            const mockRawFeed = [
                {
                    id: 1,
                    name: 'Dad',
                    lastUpdated: '2026-09-05T10:00:00Z',
                    latestEntry: {
                        encryptedPayload: 'encrypted-payload-dad',
                        keyVersion: 1
                    }
                }
            ];

            vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true,
                status: 200,
                json: async () => mockRawFeed
            } as Response);

            const decryptedCoords = { latitude: 37.7749, longitude: -122.4194, accuracy: 5 };
            vi.spyOn(cryptoService, 'decryptLocationPayload').mockResolvedValue(decryptedCoords);

            await store.fetchFeed();

            expect(store.familyFeed).toHaveLength(1);
            expect(store.familyFeed[0]).toEqual({
                id: 1,
                name: 'Dad',
                lastUpdated: '2026-09-05T10:00:00Z',
                keyVersion: 1,
                location: decryptedCoords
            });
        });

        it('should handle payload decryption failures gracefully', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setPskPassphrase('secret-key-32-chars-long!');

            vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true,
                status: 200,
                json: async () => [
                    {
                        id: 2,
                        name: 'Mom',
                        lastUpdated: '2026-09-05T10:00:00Z',
                        latestEntry: { encryptedPayload: 'invalid-encrypted-payload' }
                    }
                ]
            } as Response);

            vi.spyOn(cryptoService, 'decryptLocationPayload').mockRejectedValue(new Error('Decryption failed'));

            await store.fetchFeed();

            expect(store.familyFeed).toHaveLength(1);
            expect(store.familyFeed[0].location).toBeNull();
        });
    });

    describe('Key Rotation & Verification', () => {
        it('should sign key rotation payloads and post to server when initiating rotation', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setPskPassphrase('old-passphrase-key!', 1);

            vi.spyOn(cryptoService, 'signKeyRotationPayload').mockResolvedValue('valid-hmac-signature');

            const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true
            } as Response);

            await store.initiateKeyRotation('group-abc', 'new-secret-passphrase!');

            expect(cryptoService.signKeyRotationPayload).toHaveBeenCalled();
            expect(fetchSpy).toHaveBeenCalledWith(
                'http://localhost:5180/api/v1/location/rotate-key',
                expect.objectContaining({
                    method: 'POST',
                    body: JSON.stringify({
                        groupId: 'group-abc',
                        newKeyVersion: 2,
                        encryptedNewKey: btoa('new-secret-passphrase!'),
                        signature: 'valid-hmac-signature'
                    })
                })
            );

            expect(store.pskPassphrase).toBe('new-secret-passphrase!');
            expect(store.currentKeyVersion).toBe(2);
        });

        it('should reject pending key rotation if signature verification fails', async () => {
            const store = useLocationStore();
            store.setPskPassphrase('active-passphrase', 1);

            vi.spyOn(cryptoService, 'verifyKeyRotationPayload').mockResolvedValue(false);

            const maliciousPayload = {
                groupId: 'group-abc',
                newKeyVersion: 2,
                encryptedNewKey: btoa('attacker-injected-key'),
                signature: 'invalid-signature'
            };

            const result = await store.processPendingKeyRotation(maliciousPayload);

            expect(result).toBe(false);
            expect(store.error).toBe('Security Alert: Failed to verify key rotation from server.');
            expect(store.pskPassphrase).toBe('active-passphrase');
            expect(store.currentKeyVersion).toBe(1);
        });

        it('should accept pending key rotation and update local PSK if signature verification succeeds', async () => {
            const store = useLocationStore();
            store.setPskPassphrase('active-passphrase', 1);

            vi.spyOn(cryptoService, 'verifyKeyRotationPayload').mockResolvedValue(true);

            const validPayload = {
                groupId: 'group-abc',
                newKeyVersion: 2,
                encryptedNewKey: btoa('next-gen-passphrase'),
                signature: 'valid-signature'
            };

            const result = await store.processPendingKeyRotation(validPayload);

            expect(result).toBe(true);
            expect(store.pskPassphrase).toBe('next-gen-passphrase');
            expect(store.currentKeyVersion).toBe(2);
        });
    });
});