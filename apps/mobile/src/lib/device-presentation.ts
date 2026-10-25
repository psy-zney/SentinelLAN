import { z } from 'zod';

const samplePrefix = /^\[MẪU\]\s*/i;
const specificationsSchema = z.object({
  cpu: z.string().optional(), ram: z.string().optional(), storage: z.string().optional(),
  sampleData: z.boolean().optional(), sampleInstalledApplications: z.array(z.string()).optional(),
});

export function displayDeviceName(name: string) {
  return name.replace(samplePrefix, '').trim() || name;
}

export function isSampleDevice(device: { name: string; agentVersion: string }, sampleData = false) {
  return sampleData || samplePrefix.test(device.name) || device.agentVersion === 'sample-only';
}

export function readDeviceSpecifications(json: string | null) {
  if (!json) return { data: null, invalid: false };
  try {
    if (json.length > 65536) return { data: null, invalid: true };
    const result = specificationsSchema.safeParse(JSON.parse(json));
    return result.success ? { data: result.data, invalid: false } : { data: null, invalid: true };
  } catch {
    return { data: null, invalid: true };
  }
}

export function displayDate(value?: string | null, includeTime = true) {
  if (!value || !Number.isFinite(Date.parse(value))) return 'Chưa có thông tin';
  return new Date(value).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric', ...(includeTime ? { hour: '2-digit', minute: '2-digit' } as const : {}),
  });
}

export function connectionStatus(device: { isRevoked: boolean; isOnline?: boolean }) {
  if (device.isRevoked) return { label: 'Đã thu hồi', tone: 'danger' as const };
  if (device.isOnline === true) return { label: 'Đang kết nối', tone: 'success' as const };
  if (device.isOnline === false) return { label: 'Ngoại tuyến', tone: 'neutral' as const };
  return { label: 'Chưa rõ kết nối', tone: 'neutral' as const };
}
