const encoder = new TextEncoder();
const decoder = new TextDecoder();

export interface UnencryptedLocation {
    latitude: number;
    longitude: number;
    accuracy?: number;
    timestamp?: string;
}

export interface KeyRotationPayload {
    groupId: string;
    newKeyVersion: number;
    encryptedNewKey: string;
    signature?: string;
}

/**
 * Derives a 256-bit AES-GCM Key from a plain-text pre-shared passphrase/key.
 */
async function getCryptoKey(passphrase: string): Promise<CryptoKey> {
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

/**
* Imports a passphrase as an HMAC-SHA256 key for payload signing and verification.
*/
async function getHmacKey(passphrase: string): Promise<CryptoKey> {
    const paddedKey = passphrase.padEnd(32, '0').slice(0, 32);
    const keyData = encoder.encode(paddedKey);

    return await crypto.subtle.importKey(
        'raw',
        keyData,
        { name: 'HMAC', hash: 'SHA-256' },
        false,
        ['sign', 'verify']
    );
}

/**
 * Encrypts location JSON into a Base64-encoded payload containing IV + Ciphertext.
 */
export async function encryptLocationPayload(location: UnencryptedLocation, passphrase: string): Promise<string> {
    const key = await getCryptoKey(passphrase);

    const iv = crypto.getRandomValues(new Uint8Array(12));
    const data = encoder.encode(JSON.stringify(location));

    const ciphertext = await crypto.subtle.encrypt(
        { name: 'AES-GCM', iv },
        key,
        data
    );

    const payload = {
        iv: btoa(String.fromCharCode(...iv)),
        data: btoa(String.fromCharCode(...new Uint8Array(ciphertext)))
    };

    return btoa(JSON.stringify(payload));
}

/**
 * Decrypts a Base64 payload back into raw location coordinates.
 */
export async function decryptLocationPayload(encryptedPayload: string, passphrase: string): Promise<UnencryptedLocation> {
    const key = await getCryptoKey(passphrase);

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

/**
 * Generates an HMAC-SHA256 signature for a key rotation payload using the active PSK.
 */
export async function signKeyRotationPayload(
    payload: Omit<KeyRotationPayload, 'signature'>,
    currentPassphrase: string
): Promise<string> {
    const hmacKey = await getHmacKey(currentPassphrase);
    const dataToSign = encoder.encode(
        `${payload.groupId}:${payload.newKeyVersion}:${payload.encryptedNewKey}`
    );

    const signatureBuffer = await crypto.subtle.sign('HMAC', hmacKey, dataToSign);
    return btoa(String.fromCharCode(...new Uint8Array(signatureBuffer)));
}

/**
 * Verifies the HMAC signature of a key rotation payload against the active PSK.
 */
export async function verifyKeyRotationPayload(payload: KeyRotationPayload, currentPassphrase: string): Promise<boolean> {
    if (!payload.signature) return false;

    const hmacKey = await getHmacKey(currentPassphrase);
    const dataToVerify = encoder.encode(
        `${payload.groupId}:${payload.newKeyVersion}:${payload.encryptedNewKey}`
    );

    const signatureBuffer = Uint8Array.from(
        atob(payload.signature),
        (c) => c.charCodeAt(0)
    );

    return await crypto.subtle.verify(
        'HMAC',
        hmacKey,
        signatureBuffer,
        dataToVerify
    );
}