export interface ActivationLinkParseResult {
  valid: boolean;
  token?: string;
  error?: string;
}

const ALLOWED_SCHEMES = ['sentinellan:', 'https:', 'http:'];
const ALLOWED_HOSTS = [
  'sentinellan.local',
  'dashboard.sentinellan.local',
  'localhost',
  '10.0.2.2',
  '127.0.0.1',
];

export function parseActivationUrl(rawUrl: string): ActivationLinkParseResult {
  if (!rawUrl || typeof rawUrl !== 'string') {
    return { valid: false, error: 'Empty or invalid URL' };
  }

  try {
    const url = new URL(rawUrl);

    // 1. Verify scheme
    const scheme = url.protocol.toLowerCase();
    if (!ALLOWED_SCHEMES.includes(scheme)) {
      return { valid: false, error: `Disallowed scheme: ${scheme}` };
    }

    // 2. If HTTP/HTTPS, verify trusted host
    if (scheme === 'https:' || scheme === 'http:') {
      const hostname = url.hostname.toLowerCase();
      const isAllowedHost =
        ALLOWED_HOSTS.includes(hostname) ||
        hostname.endsWith('.sentinellan.local') ||
        hostname.startsWith('192.168.') ||
        hostname.startsWith('10.');

      if (!isAllowedHost) {
        return { valid: false, error: `Untrusted host: ${hostname}` };
      }
    }

    // 3. Verify path is /activate or activate
    const cleanPath = url.pathname.replace(/^\/+/, '').replace(/\/+$/, '');
    if (cleanPath !== 'activate' && url.host !== 'activate') {
      return { valid: false, error: `Invalid activation path: ${url.pathname}` };
    }

    // 4. Extract token
    const token = url.searchParams.get('token');
    if (!token || token.trim().length < 16 || token.trim().length > 512) {
      return { valid: false, error: 'Missing or malformed activation token' };
    }

    // Reject token containing control characters or HTML/script injection
    if (/[<>"'\s]/.test(token)) {
      return { valid: false, error: 'Token contains invalid characters' };
    }

    return { valid: true, token: token.trim() };
  } catch {
    // URL parsing failed
    return { valid: false, error: 'Malformed URL structure' };
  }
}
