export interface QrParseResult {
  valid: boolean;
  code?: string;
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

export function parseScannedQrContent(raw: string): QrParseResult {
  if (!raw || typeof raw !== 'string') {
    return { valid: false, error: 'Empty or invalid QR content' };
  }

  const trimmed = raw.trim();

  // Reject malicious schemes
  const lower = trimmed.toLowerCase();
  if (
    lower.startsWith('javascript:') ||
    lower.startsWith('file:') ||
    lower.startsWith('data:') ||
    lower.startsWith('intent:') ||
    lower.startsWith('vbscript:')
  ) {
    return { valid: false, error: 'Malicious or unsupported URI scheme' };
  }

  // 1. Check if it's a URL
  if (lower.startsWith('http://') || lower.startsWith('https://') || lower.startsWith('sentinellan://')) {
    try {
      const url = new URL(trimmed);
      const scheme = url.protocol.toLowerCase();

      if (!ALLOWED_SCHEMES.includes(scheme)) {
        return { valid: false, error: `Disallowed scheme: ${scheme}` };
      }

      if (scheme === 'https:' || scheme === 'http:') {
        const hostname = url.hostname.toLowerCase();
        const isAllowed =
          ALLOWED_HOSTS.includes(hostname) ||
          hostname.endsWith('.sentinellan.local') ||
          hostname.startsWith('192.168.') ||
          hostname.startsWith('10.');

        if (!isAllowed) {
          return { valid: false, error: `Foreign or untrusted origin: ${hostname}` };
        }
      }

      // Check path: /qr/<code-part> or sentinellan://qr/<code-part>
      const segments = url.pathname.split('/').filter(Boolean);
      let extractedCode: string | null = null;

      if (segments.length >= 2 && segments[0].toLowerCase() === 'qr') {
        extractedCode = segments[1];
      } else if (url.host.toLowerCase() === 'qr' && segments.length >= 1) {
        extractedCode = segments[0];
      }

      if (extractedCode && isValidOpaqueCode(extractedCode)) {
        return { valid: true, code: extractedCode };
      }

      return { valid: false, error: 'URL does not point to a valid SentinelLAN /qr endpoint' };
    } catch {
      return { valid: false, error: 'Malformed URL structure in QR code' };
    }
  }

  // 2. Direct opaque code string check (e.g., QR-..., or 16-128 alphanumeric characters)
  if (isValidOpaqueCode(trimmed)) {
    return { valid: true, code: trimmed };
  }

  return { valid: false, error: 'QR content does not match valid SentinelLAN asset format' };
}

function isValidOpaqueCode(code: string): boolean {
  if (!code || code.length < 6 || code.length > 128) {
    return false;
  }
  // Alphanumeric with hyphen and underscore only
  return /^[A-Za-z0-9_-]+$/.test(code);
}
