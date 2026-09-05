/**
 * Validates the server URL, enforcing HTTPS in production environments.
 * Permits HTTP strictly on local development hosts (localhost / 127.0.0.1).
 */
export function isValidServerUrl(url: string): { valid: boolean; reason?: string; } {
    try {
        const parsed = new URL(url);
        const isDev = parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1';

        // 1. Check valid protocol first
        if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
            return { valid: false, reason: 'URL must use HTTP or HTTPS protocol.' };
        }

        // 2. Enforce HTTPS for non-development domains
        if (parsed.protocol !== 'https:' && !isDev) {
            return { valid: false, reason: 'HTTPS is required for remote server connections.' };
        }

        return { valid: true };
    } catch {
        return { valid: false, reason: 'Invalid URL format.' };
    }
}