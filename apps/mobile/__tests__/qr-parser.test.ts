import { parseScannedQrContent } from '../src/features/scan/qr-parser';

describe('QR Code Scanned Content Parser', () => {
  it('parses canonical HTTPS URL on trusted host', () => {
    const raw = 'https://sentinellan.local/qr/QR-A1B2C3D4';
    const result = parseScannedQrContent(raw);

    expect(result.valid).toBe(true);
    expect(result.code).toBe('QR-A1B2C3D4');
  });

  it('parses custom scheme canonical URL', () => {
    const raw = 'sentinellan://qr/QR-DEVICE-9988';
    const result = parseScannedQrContent(raw);

    expect(result.valid).toBe(true);
    expect(result.code).toBe('QR-DEVICE-9988');
  });

  it('parses direct opaque code string with valid prefix', () => {
    const raw = 'QR-F8E7D6C5B4A3';
    const result = parseScannedQrContent(raw);

    expect(result.valid).toBe(true);
    expect(result.code).toBe('QR-F8E7D6C5B4A3');
  });

  it('rejects foreign origin in QR URL', () => {
    const raw = 'https://phishing-site.test/qr/QR-A1B2C3D4';
    const result = parseScannedQrContent(raw);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Foreign or untrusted origin');
  });

  it('rejects malicious schemes', () => {
    expect(parseScannedQrContent('javascript:doBadThings()').valid).toBe(false);
    expect(parseScannedQrContent('file:///android_asset/something').valid).toBe(false);
    expect(parseScannedQrContent('data:text/html,<h1>bad</h1>').valid).toBe(false);
  });

  it('rejects invalid code format or special characters', () => {
    expect(parseScannedQrContent('QR;DROP TABLE Users;--').valid).toBe(false);
    expect(parseScannedQrContent('short').valid).toBe(false);
  });
});
