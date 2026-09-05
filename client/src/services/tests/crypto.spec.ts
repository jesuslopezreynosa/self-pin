import { describe, it, expect } from 'vitest';
import {
    encryptLocationPayload,
    decryptLocationPayload,
    type UnencryptedLocation,
    signKeyRotationPayload,
    verifyKeyRotationPayload,
    type KeyRotationPayload
} from '../crypto';

describe('Crypto Service', () => {
    const secretPassphrase = 'groups-secret-key-32-chars-long!';
    const testLocation: UnencryptedLocation = {
        latitude: 37.7749,
        longitude: -122.4194,
        accuracy: 5.0,
    };

    it('should encrypt and decrypt a location payload correctly', async () => {
        // 1. Encrypt
        const encryptedBase64 = await encryptLocationPayload(testLocation, secretPassphrase);
        expect(encryptedBase64).toBeTypeOf('string');
        expect(encryptedBase64).not.toContain(`${testLocation.latitude}`); // Raw values are hidden

        console.log(encryptedBase64);

        // 2. Decrypt
        const decrypted = await decryptLocationPayload(encryptedBase64, secretPassphrase);
        expect(decrypted.latitude).toBe(testLocation.latitude);
        expect(decrypted.longitude).toBe(testLocation.longitude);
        expect(decrypted.accuracy).toBe(testLocation.accuracy);

        console.log(decrypted);
    });

    it('should fail decryption when given an incorrect passphrase', async () => {
        const encryptedBase64 = await encryptLocationPayload(testLocation, secretPassphrase);
        const wrongPassphrase = 'wrong-family-passphrase!';

        await expect(
            decryptLocationPayload(encryptedBase64, wrongPassphrase)
        ).rejects.toThrow();
    });

    it('should produce unique ciphertexts for identical inputs (unique IVs)', async () => {
        const encrypted1 = await encryptLocationPayload(testLocation, secretPassphrase);
        const encrypted2 = await encryptLocationPayload(testLocation, secretPassphrase);

        console.log(encrypted1);
        console.log(encrypted2);

        // Each run uses a fresh 12-byte IV, so ciphertexts must differ
        expect(encrypted1).not.toBe(encrypted2);
    });

    describe('HMAC Key Rotation Signatures', () => {
        const rotationData: Omit<KeyRotationPayload, 'signature'> = {
            groupId: '123e4567-e89b-12d3-a456-426614174000',
            newKeyVersion: 2,
            encryptedNewKey: 'encrypted-key-material-blob'
        };

        it('should successfully sign and verify a valid key rotation payload', async () => {
            const signature = await signKeyRotationPayload(rotationData, secretPassphrase);
            expect(signature).toBeTypeOf('string');

            const fullPayload: KeyRotationPayload = {
                ...rotationData,
                signature
            };

            const isValid = await verifyKeyRotationPayload(fullPayload, secretPassphrase);
            expect(isValid).toBe(true);
        });

        it('should fail verification if the signature was generated with a different passphrase', async () => {
            const signature = await signKeyRotationPayload(rotationData, 'attacker-secret-key!');
            const fullPayload: KeyRotationPayload = {
                ...rotationData,
                signature
            };

            const isValid = await verifyKeyRotationPayload(fullPayload, secretPassphrase);
            expect(isValid).toBe(false);
        });

        it('should fail verification if the payload contents were tampered with', async () => {
            const signature = await signKeyRotationPayload(rotationData, secretPassphrase);

            const tamperedPayload: KeyRotationPayload = {
                ...rotationData,
                encryptedNewKey: 'malicious-injected-key-material',
                signature
            };

            const isValid = await verifyKeyRotationPayload(tamperedPayload, secretPassphrase);
            expect(isValid).toBe(false);
        });

        it('should reject verification if signature is altered', async () => {
            const signature = await signKeyRotationPayload(rotationData, secretPassphrase);
            
            const alteredPayload: KeyRotationPayload = {
              ...rotationData,
              newKeyVersion: 3, // Tampered data
              signature
            };
      
            const isValid = await verifyKeyRotationPayload(alteredPayload, secretPassphrase);
            expect(isValid).toBe(false);
          });

        it('should return false when payload lacks a signature', async () => {
            const unsignedPayload: KeyRotationPayload = {
                ...rotationData
            };

            const isValid = await verifyKeyRotationPayload(unsignedPayload, secretPassphrase);
            expect(isValid).toBe(false);
        });
    });
});