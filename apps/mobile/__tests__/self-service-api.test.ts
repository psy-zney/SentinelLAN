import { MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';
import { CreateSupportRequestSchema, RedeemMaintenanceSchema } from '../src/lib/validation/self-service';
import { notificationRequestId } from '../src/features/self-service/push';
import { parseSelectedImage } from '../src/features/self-service/image-attachment';

const id = '00000000-0000-4000-8000-000000000001';
const supportRequest = { id, deviceId: id, deviceName: 'PC', userId: id, userName: 'An', kind: 'Incident', category: 'Printer', title: 'Không in được', description: '', canWork: false, status: 'Open', assignedTechnicianId: null, assignedTechnicianName: null, catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: null, createdAt: '2026-09-26T00:00:00Z', updatedAt: '2026-09-26T00:00:00Z', approvalExpiresAt: null };
describe('Employee self-service API boundary', () => {
  const api = new MobileApiClient('https://sentinellan.local');
  afterEach(() => { setInMemoryAccessToken(null); jest.restoreAllMocks(); });
  it('sends employee work impact and confirmation with authenticated API traffic', async () => {
    setInMemoryAccessToken('access');
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => supportRequest });
    const input = { kind: 'Incident' as const, category: 'Printer' as const, title: 'Không in được', canWork: false, confirmed: true as const, idempotencyKey: id };
    expect(await api.createSupportRequest(input)).toEqual(supportRequest);
    const [url, options] = (global.fetch as jest.Mock).mock.calls[0];
    expect(url).toBe('https://sentinellan.local/api/v1/self-service/requests');
    expect(options.headers.get('Authorization')).toBe('Bearer access');
    expect(JSON.parse(options.body)).toEqual(input);
  });
  it('preserves server authorization failures and does not fabricate a result', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 403, json: async () => ({ detail: 'Not your assigned device' }) });
    await expect(api.supportRequest(id)).rejects.toMatchObject({ status: 403 });
  });
  it('rejects malformed responses and unconfirmed sensitive actions', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ({ id }) });
    await expect(api.supportRequest(id)).rejects.toThrow();
    await expect(api.redeemMaintenance(id, '12345678', false)).rejects.toThrow();
    await expect(api.updateOwnSupportRequest(id, 'Closed', 'Used again', false)).rejects.toThrow();
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });
  it('validates UUID request navigation and never accepts an arbitrary push URL', () => {
    expect(notificationRequestId({ requestId: id, url: 'https://attacker.invalid' })).toBe(id);
    expect(notificationRequestId({ url: 'sentinellan://request-detail?id=bad' })).toBeNull();
    expect(notificationRequestId({ requestId: '../admin' })).toBeNull();
  });
  it('rejects unsupported images and oversize uploads before sending', () => {
    const asset = { uri: 'file:///error.jpg', width: 100, height: 100, base64: '/9j/AAAA' };
    expect(parseSelectedImage(asset).contentType).toBe('image/jpeg');
    expect(() => parseSelectedImage({ ...asset, base64: 'PHN2Zz4=' })).toThrow('JPEG');
    expect(() => parseSelectedImage({ ...asset, fileSize: 2 * 1024 * 1024 + 1 })).toThrow('2 MiB');
  });
  it('requires confirmation and eight decimal digits for maintenance', () => {
    expect(RedeemMaintenanceSchema.safeParse({ code: '12345678', confirmed: true }).success).toBe(true);
    expect(RedeemMaintenanceSchema.safeParse({ code: '123456', confirmed: true }).success).toBe(false);
    expect(CreateSupportRequestSchema.safeParse({ kind: 'Incident', category: 'Other', title: 'help', canWork: true, confirmed: false, idempotencyKey: id }).success).toBe(false);
  });
});
