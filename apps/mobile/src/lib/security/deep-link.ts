import { isTrustedWebUrl } from './trusted-links';

export interface ActivationLinkParseResult {
  valid: boolean;
  token?: string;
  error?: string;
}

export function parseActivationUrl(rawUrl: string): ActivationLinkParseResult {
  if (!rawUrl || typeof rawUrl !== 'string') {
    return { valid: false, error: 'Empty or invalid URL' };
  }

  try {
    const url = new URL(rawUrl);

    if (url.protocol === 'sentinellan:') {
      const customPathIsValid =
        (url.host.toLowerCase() === 'activate' && (url.pathname === '' || url.pathname === '/')) ||
        (url.host === '' && url.pathname === '/activate');
      if (!customPathIsValid) {
        return { valid: false, error: 'Invalid activation path' };
      }
    } else if (!isTrustedWebUrl(url)) {
      return { valid: false, error: `Untrusted host: ${url.hostname}` };
    } else if (url.pathname !== '/activate') {
      return { valid: false, error: `Invalid activation path: ${url.pathname}` };
    }

    // Extract the token without navigating to the URL.
    const fragmentParams = new URLSearchParams(url.hash.replace(/^#/, ''));
    const token = url.searchParams.get('token') ?? fragmentParams.get('token');
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
