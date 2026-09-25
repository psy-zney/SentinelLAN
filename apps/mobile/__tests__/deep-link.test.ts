import { parseActivationUrl } from '../src/lib/security/deep-link';

describe('Deep Link Parser for Account Activation', () => {
  it('parses valid custom scheme deep links', () => {
    const raw = 'sentinellan://activate?token=abcdef1234567890abcdef1234567890';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(true);
    expect(result.token).toBe('abcdef1234567890abcdef1234567890');
    expect(result.error).toBeUndefined();
  });

  it('parses valid verified HTTPS app links on trusted host', () => {
    const raw = 'https://sentinellan.local/activate?token=secure-token-sample-123456789';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(true);
    expect(result.token).toBe('secure-token-sample-123456789');
  });

  it('parses activation tokens from URL fragments so the token is not sent in the HTTP request', () => {
    const raw = 'https://sentinellan.local/activate#token=secure-token-sample-123456789';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(true);
    expect(result.token).toBe('secure-token-sample-123456789');
  });

  it('rejects an unconfigured LAN host', () => {
    const raw = 'https://192.168.1.100/activate?token=valid-lan-token-1234567890';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(false);
  });

  it('rejects HTTP, lookalike hosts, credentials in URLs and a foreign custom-scheme host', () => {
    const token = 'valid-token-1234567890';
    expect(parseActivationUrl(`http://sentinellan.local/activate?token=${token}`).valid).toBe(false);
    expect(parseActivationUrl(`https://sentinellan.local.evil.test/activate?token=${token}`).valid).toBe(false);
    expect(parseActivationUrl(`https://user@sentinellan.local/activate?token=${token}`).valid).toBe(false);
    expect(parseActivationUrl(`sentinellan://evil/activate?token=${token}`).valid).toBe(false);
  });

  it('rejects foreign or untrusted host', () => {
    const raw = 'https://malicious-attacker.com/activate?token=valid-token-1234567890';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Untrusted host');
    expect(result.token).toBeUndefined();
  });

  it('rejects disallowed schemes like javascript or file', () => {
    expect(parseActivationUrl('javascript:alert(1)').valid).toBe(false);
    expect(parseActivationUrl('file:///sdcard/token.txt').valid).toBe(false);
    expect(parseActivationUrl('ftp://sentinellan.local/activate?token=123').valid).toBe(false);
  });

  it('rejects links missing activation path', () => {
    const raw = 'https://sentinellan.local/dashboard?token=1234567890abcdef1234';
    const result = parseActivationUrl(raw);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Invalid activation path');
  });

  it('rejects tokens that are too short or contain dangerous characters', () => {
    const shortToken = 'https://sentinellan.local/activate?token=short';
    expect(parseActivationUrl(shortToken).valid).toBe(false);

    const injectionToken = 'https://sentinellan.local/activate?token=<script>alert(1)</script>';
    expect(parseActivationUrl(injectionToken).valid).toBe(false);
  });
});
