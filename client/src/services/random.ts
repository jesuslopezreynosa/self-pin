/**
 * Generates a 32-byte (256-bit) cryptographically secure random byte array.
 */
export function generateRandomBytes(): Uint8Array {
    const bytes = new Uint8Array(32);
    crypto.getRandomValues(bytes);
    return bytes;
}

/**
 * Converts 32 random bytes into a 64-character Hex string for UI storage/display.
 */
export function generateHexPassphrase(): string {
    return Array.from(generateRandomBytes())
        .map((b) => b.toString(16).padStart(2, '0'))
        .join('');
}