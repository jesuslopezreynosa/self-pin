import { describe, it, expect } from 'vitest';
import {
    encryptLocationPayload,
    decryptLocationPayload,
    type UnencryptedLocation
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
        expect(encryptedBase64).not.toContain(`${ testLocation.latitude }`); // Raw values are hidden

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
});