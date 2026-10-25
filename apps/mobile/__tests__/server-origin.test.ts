import { DEFAULT_SERVER_ORIGIN, normalizeMobileOrigin } from '../src/lib/server-origin';

describe('company mobile server configuration', () => {
  it('defaults to the deployed company server', () => {
    expect(DEFAULT_SERVER_ORIGIN).toBe('https://manager.zney295.id.vn');
    expect(normalizeMobileOrigin(DEFAULT_SERVER_ORIGIN + '/')).toBe(DEFAULT_SERVER_ORIGIN);
  });

  it.each(['http://10.0.2.2:5000', 'https://localhost:7147', 'https://127.0.0.1', 'https://[::1]',
    'https://user:password@server.test', 'https://server.test/api', 'https://server.test/?token=secret',
    'https://server.test/#token', 'invalid'])('rejects an unsafe or local production origin: %s', value => {
    expect(() => normalizeMobileOrigin(value)).toThrow();
  });

  it('permits an explicitly selected emulator only in development', () => {
    expect(normalizeMobileOrigin('http://10.0.2.2:8080', true)).toBe('http://10.0.2.2:8080');
    expect(() => normalizeMobileOrigin('http://public.example.test', true)).toThrow();
  });
});
