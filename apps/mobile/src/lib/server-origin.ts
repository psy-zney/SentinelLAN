export const DEFAULT_SERVER_ORIGIN = 'https://manager.zney295.id.vn';

export function normalizeMobileOrigin(value: string, allowDevelopmentLoopback = false): string {
  let url: URL;
  try { url = new URL(value); } catch { throw new Error('Configure a valid company server address.'); }
  const loopback = url.hostname === 'localhost' || url.hostname.endsWith('.localhost') ||
    /^127\./.test(url.hostname) || url.hostname === '[::1]' || url.hostname === '0.0.0.0' || url.hostname === '10.0.2.2';
  if (url.username || url.password || url.pathname !== '/' || url.search || url.hash ||
      (url.protocol !== 'https:' && !(allowDevelopmentLoopback && loopback && url.protocol === 'http:')) ||
      (loopback && !allowDevelopmentLoopback)) {
    throw new Error('Use the company HTTPS origin; local addresses require explicit development configuration.');
  }
  return url.origin;
}
