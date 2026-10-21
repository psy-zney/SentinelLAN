import { parseEnrollmentCode } from '../src/features/scan/enrollment-code';

const now = Date.parse('2026-10-21T10:00:00Z');
function code(overrides: Record<string, unknown> = {}) {
  return 'SL1.' + btoa(JSON.stringify({ serverUrl: 'https://sentinel.example.test', token: 'a'.repeat(64), expiresAt: '2026-10-21T10:15:00Z', ...overrides })).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}
describe('Enrollment QR preview', () => {
  it('previews a fresh code only for the configured API origin', () => {
    const raw = code();
    expect(parseEnrollmentCode(raw, 'https://sentinel.example.test', now)).toEqual({ connectionCode: raw, serverUrl: 'https://sentinel.example.test', expiresAt: '2026-10-21T10:15:00Z' });
  });
  it.each([
    { serverUrl: 'https://foreign.example.test' }, { serverUrl: 'https://user:pass@sentinel.example.test' },
    { serverUrl: 'https://sentinel.example.test/path' }, { token: 'bad' },
    { expiresAt: '2026-10-21T09:59:00Z' }, { expiresAt: '2026-10-22T10:00:00Z' },
  ])('rejects untrusted, malformed or expired bootstrap data %p', override => {
    expect(() => parseEnrollmentCode(code(override), 'https://sentinel.example.test', now)).toThrow();
  });
});
