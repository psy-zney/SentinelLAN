export type EnrollmentPreview = { connectionCode: string; serverUrl: string; expiresAt: string };

export function parseEnrollmentCode(raw: string, trustedOrigin: string, now = Date.now()): EnrollmentPreview {
  if (raw.length > 4096 || !/^SL1\.[A-Za-z0-9_-]+$/.test(raw)) throw new Error('Mã đăng ký không hợp lệ.');
  try {
    const encoded = raw.slice(4).replace(/-/g, '+').replace(/_/g, '/');
    const value: unknown = JSON.parse(atob(encoded.padEnd(Math.ceil(encoded.length / 4) * 4, '=')));
    if (!value || typeof value !== 'object') throw new Error();
    const data = value as Record<string, unknown>;
    if (typeof data.serverUrl !== 'string' || typeof data.token !== 'string' || !/^[a-fA-F0-9]{64}$/.test(data.token) || typeof data.expiresAt !== 'string') throw new Error();
    const server = new URL(data.serverUrl); const expiry = Date.parse(data.expiresAt);
    if (server.origin !== new URL(trustedOrigin).origin || (server.protocol !== 'https:' && !['localhost', '127.0.0.1', '[::1]'].includes(server.hostname)) || server.username || server.password || server.search || server.hash || server.pathname !== '/')
      throw new Error('Mã thuộc máy chủ khác hoặc địa chỉ chưa được tin cậy.');
    if (!Number.isFinite(expiry) || expiry <= now || expiry > now + 16 * 60 * 1000) throw new Error('Mã đã hết hạn hoặc thời hạn không hợp lệ. Hãy xin mã mới từ IT.');
    return { connectionCode: raw, serverUrl: server.origin, expiresAt: data.expiresAt };
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('Mã ')) throw error;
    throw new Error('Mã đăng ký không hợp lệ.');
  }
}
