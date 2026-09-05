const encoder = new TextEncoder();
const decoder = new TextDecoder();

/**
 * Derives a 256-bit AES-GCM Key from a plain-text pre-shared passphrase.
 */
async function getCryptoKey(passphrase: string): Promise<CryptoKey> {
    // Pad or trim passphrase to strictly 32 bytes (256 bits)
    const paddedKey = passphrase.padEnd(32, '0').slice(0, 32);
    const keyData = encoder.encode(paddedKey);

    return await crypto.subtle.importKey(
        'raw',
        keyData,
        { name: 'AES-GCM' },
        false,
        ['encrypt', 'decrypt']
    );
}

export interface UnencryptedLocation {
    latitude: number;
    longitude: number;
    accuracy?: number;
    timestamp?: string;
}

/**
 * Encrypts location JSON into a Base64-encoded payload containing IV + Ciphertext.
 */
export async function encryptLocationPayload(
    location: UnencryptedLocation,
    passphrase: string
): Promise<string> {
    const key = await getCryptoKey(passphrase);

    // Generate a random 12-byte Initialization Vector (IV/Nonce) for AES-GCM
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const data = encoder.encode(JSON.stringify(location));

    const ciphertext = await crypto.subtle.encrypt(
        { name: 'AES-GCM', iv },
        key,
        data
    );

    // Package IV and Ciphertext as Base64 strings inside a JSON object
    const payload = {
        iv: btoa(String.fromCharCode(...iv)),
        data: btoa(String.fromCharCode(...new Uint8Array(ciphertext)))
    };

    return btoa(JSON.stringify(payload));
}

/**
 * Decrypts a Base64 payload back into raw location coordinates.
 */
export async function decryptLocationPayload(
    encryptedPayload: string,
    passphrase: string
): Promise<UnencryptedLocation> {
    const key = await getCryptoKey(passphrase);

    // Parse Base64 container
    const { iv, data } = JSON.parse(atob(encryptedPayload));

    const ivBuffer = Uint8Array.from(atob(iv), (c) => c.charCodeAt(0));
    const dataBuffer = Uint8Array.from(atob(data), (c) => c.charCodeAt(0));

    const decryptedBuffer = await crypto.subtle.decrypt(
        { name: 'AES-GCM', iv: ivBuffer },
        key,
        dataBuffer
    );

    return JSON.parse(decoder.decode(decryptedBuffer)) as UnencryptedLocation;
}