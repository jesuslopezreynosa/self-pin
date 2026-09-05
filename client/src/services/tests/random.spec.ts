import { describe, it, expect } from 'vitest';
import {
    generateRandomBytes,
    generateHexPassphrase
} from '../random';

describe('Cryptographic Random Key Generator', () => {
    it('should generate exactly 32 bytes of random data', () => {
        const bytes = generateRandomBytes();
        expect(bytes).toBeInstanceOf(Uint8Array);
        expect(bytes.length).toBe(32);

        console.log(`bytes: ${ bytes }`)
    });

    it('should generate a 64-character hex string', () => {
        const hex = generateHexPassphrase();
        expect(hex).toHaveLength(64);
        expect(hex).toMatch(/^[0-9a-f]{64}$/);

        console.log(`hex: ${ hex }`)
    });

    it('should generate unique values on successive calls', () => {
        const pass1 = generateHexPassphrase();
        const pass2 = generateHexPassphrase();
        expect(pass1).not.toBe(pass2);

        console.log(`random_value_1: ${ pass1 }`)
        console.log(`random_value_2: ${ pass2 }`)
    });
});