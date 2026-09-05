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
            expect(store.userSigningKey).toBe('');
            expect(store.groupKeys).toEqual({});
            expect(store.isAuthenticated).toBe(false);
            expect(store.hasGroupKeys).toBe(false);
            expect(store.familyFeed).toEqual([]);
        });

        it('should set credentials and group keys and persist them to localStorage', () => {
            const store = useLocationStore();

            store.setDeviceToken('device-123');
            store.setUserSigningKey('user-identity-signing-key');
            store.setGroupKey('group-abc', 'secret-psk-32-chars-long!', 1);

            expect(store.deviceToken).toBe('device-123');
            expect(store.userSigningKey).toBe('user-identity-signing-key');
            expect(store.groupKeys['group-abc']).toEqual({ psk: 'secret-psk-32-chars-long!', keyVersion: 1 });
            expect(store.isAuthenticated).toBe(true);
            expect(store.hasGroupKeys).toBe(true);

            expect(localStorage.getItem('deviceToken')).toBe('device-123');
            expect(localStorage.getItem('userSigningKey')).toBe('user-identity-signing-key');
            expect(JSON.parse(localStorage.getItem('groupKeys') || '{}')).toEqual({
                'group-abc': { psk: 'secret-psk-32-chars-long!', keyVersion: 1 }
            });
        });
    });

    describe('Publishing Location', () => {
        it('should set an error if client is unauthenticated or missing group keys', async () => {
            const store = useLocationStore();
            const coords = { latitude: 37.7749, longitude: -122.4194 };

            await store.publishLocation(coords);

            expect(store.error).toBe('Device token or group encryption keys missing.');
        });

        it('should loop through all configured groups, encrypting and POSTing to each independently', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');

            // Configured for 2 distinct groups
            store.setGroupKey('group-abc', 'psk-for-group-abc', 1);
            store.setGroupKey('group-xyz', 'psk-for-group-xyz', 2);

            vi.spyOn(cryptoService, 'encryptLocationPayload')
                .mockResolvedValueOnce('blob-abc')
                .mockResolvedValueOnce('blob-xyz');

            const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true
            } as Response);

            const coords = { latitude: 37.7749, longitude: -122.4194 };
            await store.publishLocation(coords);

            // Assert encryption was invoked twice with respective PSKs
            expect(cryptoService.encryptLocationPayload).toHaveBeenCalledTimes(2);
            expect(cryptoService.encryptLocationPayload).toHaveBeenNthCalledWith(1, coords, 'psk-for-group-abc');
            expect(cryptoService.encryptLocationPayload).toHaveBeenNthCalledWith(2, coords, 'psk-for-group-xyz');

            // Assert fetch was invoked twice with correct payloads
            expect(fetchSpy).toHaveBeenCalledTimes(2);
            expect(fetchSpy).toHaveBeenNthCalledWith(
                1,
                'http://localhost:5180/api/v1/location/update',
                expect.objectContaining({
                    body: JSON.stringify({
                        groupId: 'group-abc',
                        encryptedPayload: 'blob-abc',
                        keyVersion: 1
                    })
                })
            );
            expect(fetchSpy).toHaveBeenNthCalledWith(
                2,
                'http://localhost:5180/api/v1/location/update',
                expect.objectContaining({
                    body: JSON.stringify({
                        groupId: 'group-xyz',
                        encryptedPayload: 'blob-xyz',
                        keyVersion: 2
                    })
                })
            );

            expect(store.error).toBeNull();
        });
    });

    describe('Fetching and Decrypting Feed', () => {
        it('should fetch feed and decrypt entries successfully using group PSK', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setGroupKey('group-abc', 'secret-psk-group-abc', 1);

            const mockRawFeed = [
                {
                    id: 1,
                    name: 'Dad',
                    groupId: 'group-abc',
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
                groupId: 'group-abc',
                lastUpdated: '2026-09-05T10:00:00Z',
                keyVersion: 1,
                location: decryptedCoords
            });
        });

        it('should handle payload decryption failures gracefully', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setGroupKey('group-abc', 'secret-psk-group-abc', 1);

            vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true,
                status: 200,
                json: async () => [
                    {
                        id: 2,
                        name: 'Mom',
                        groupId: 'group-abc',
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
        it('should sign key rotation payloads using group PSK and post to server', async () => {
            const store = useLocationStore();
            store.setDeviceToken('device-123');
            store.setGroupKey('group-abc', 'old-psk-group-abc', 1);

            vi.spyOn(cryptoService, 'signKeyRotationPayload').mockResolvedValue('valid-hmac-signature');

            const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
                ok: true
            } as Response);

            await store.initiateKeyRotation('group-abc', 'new-secret-passphrase!');

            expect(cryptoService.signKeyRotationPayload).toHaveBeenCalledWith(
                expect.objectContaining({
                    groupId: 'group-abc',
                    newKeyVersion: 2,
                    encryptedNewKey: btoa('new-secret-passphrase!')
                }),
                'old-psk-group-abc'
            );

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

            expect(store.groupKeys['group-abc']).toEqual({
                psk: 'new-secret-passphrase!',
                keyVersion: 2
            });
        });

        it('should reject pending key rotation if signature verification fails', async () => {
            const store = useLocationStore();
            store.setGroupKey('group-abc', 'active-passphrase', 1);

            vi.spyOn(cryptoService, 'verifyKeyRotationPayload').mockResolvedValue(false);

            const maliciousPayload = {
                groupId: 'group-abc',
                newKeyVersion: 2,
                encryptedNewKey: btoa('attacker-injected-key'),
                signature: 'invalid-signature'
            };

            const result = await store.processPendingKeyRotation('group-abc', maliciousPayload);

            expect(result).toBe(false);
            expect(store.error).toBe('Security Alert: Failed to verify key rotation for group group-abc.');
            expect(store.groupKeys['group-abc']).toEqual({
                psk: 'active-passphrase',
                keyVersion: 1
            });
        });

        it('should accept pending key rotation and update local PSK if signature verification succeeds', async () => {
            const store = useLocationStore();
            store.setGroupKey('group-abc', 'active-passphrase', 1);

            vi.spyOn(cryptoService, 'verifyKeyRotationPayload').mockResolvedValue(true);

            const validPayload = {
                groupId: 'group-abc',
                newKeyVersion: 2,
                encryptedNewKey: btoa('next-gen-passphrase'),
                signature: 'valid-signature'
            };

            const result = await store.processPendingKeyRotation('group-abc', validPayload);

            expect(result).toBe(true);
            expect(store.groupKeys['group-abc']).toEqual({
                psk: 'next-gen-passphrase',
                keyVersion: 2
            });
        });
    });
});