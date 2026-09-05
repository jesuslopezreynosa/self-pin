import { describe, it, expect } from 'vitest';
import { isValidServerUrl } from '../urlValidator';

describe('isValidServerUrl', () => {
    describe('Local development environments', () => {
        it('should allow http for localhost', () => {
            const result = isValidServerUrl('http://localhost:5180');
            expect(result.valid).toBe(true);
            expect(result.reason).toBeUndefined();
        });

        it('should allow http for 127.0.0.1', () => {
            const result = isValidServerUrl('http://127.0.0.1:5180');
            expect(result.valid).toBe(true);
            expect(result.reason).toBeUndefined();
        });

        it('should allow https for localhost', () => {
            const result = isValidServerUrl('https://localhost:5180');
            expect(result.valid).toBe(true);
            expect(result.reason).toBeUndefined();
        });
    });

    describe('Remote server environments', () => {
        it('should allow valid remote HTTPS URLs', () => {
            const result = isValidServerUrl('https://api.selfpin.app');
            expect(result.valid).toBe(true);
            expect(result.reason).toBeUndefined();
        });

        it('should reject remote HTTP URLs requiring HTTPS', () => {
            const result = isValidServerUrl('http://api.selfpin.app');
            expect(result.valid).toBe(false);
            expect(result.reason).toBe('HTTPS is required for remote server connections.');
        });
    });

    describe('Unsupported protocols and malformed URLs', () => {
        it('should reject non-HTTP/HTTPS protocols', () => {
            const result = isValidServerUrl('ws://localhost:5180');
            expect(result.valid).toBe(false);
            expect(result.reason).toBe('URL must use HTTP or HTTPS protocol.');
        });

        it('should reject ftp protocols', () => {
            const result = isValidServerUrl('ftp://example.com');
            expect(result.valid).toBe(false);
            expect(result.reason).toBe('URL must use HTTP or HTTPS protocol.');
        });

        it('should reject malformed string input', () => {
            const result = isValidServerUrl('not-a-valid-url');
            expect(result.valid).toBe(false);
            expect(result.reason).toBe('Invalid URL format.');
        });

        it('should reject empty strings', () => {
            const result = isValidServerUrl('');
            expect(result.valid).toBe(false);
            expect(result.reason).toBe('Invalid URL format.');
        });
    });
});