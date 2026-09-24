export function extractSentinelLanQrCode(scannedText: string, allowedOrigin: string): string | null {
  const trimmed = scannedText.trim();
  const codePattern = /^[a-zA-Z0-9_-]{16,128}$/;

  if (/^[a-z][a-z0-9+.-]*:/i.test(trimmed)) {
    try {
      const url = new URL(trimmed);
      if (url.protocol !== "https:" && url.protocol !== "http:") return null;
      if (url.origin !== allowedOrigin) return null;
      const match = url.pathname.match(/^\/qr\/([a-zA-Z0-9_-]{16,128})\/?$/i);
      return match?.[1] ?? null;
    } catch {
      return null;
    }
  }

  return codePattern.test(trimmed) ? trimmed : null;
}
