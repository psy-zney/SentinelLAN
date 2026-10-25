import { connectionStatus, displayDate, displayDeviceName, isSampleDevice, readDeviceSpecifications } from '../src/lib/device-presentation';

it('replaces only the explicit sample prefix in visible names', () => {
  expect(displayDeviceName('[MẪU] KT-PC-01')).toBe('KT-PC-01');
  expect(displayDeviceName('Máy thiết kế mẫu')).toBe('Máy thiết kế mẫu');
  expect(displayDeviceName('[MẪU]')).toBe('[MẪU]');
  expect(isSampleDevice({ name: 'KT-PC-01', agentVersion: 'sample-only' })).toBe(true);
  expect(isSampleDevice({ name: '[MẪU] KT-PC-01', agentVersion: '1.0' })).toBe(true);
  expect(isSampleDevice({ name: 'Office-PC', agentVersion: '1.0' }, true)).toBe(true);
  expect(isSampleDevice({ name: 'Office-PC', agentVersion: '1.0' })).toBe(false);
});

it('reads sample configuration without exposing JSON metadata as user-facing rows', () => {
  expect(readDeviceSpecifications('{"cpu":"Intel i5","sampleData":true,"dataset":"private-marker"}').data).toEqual({ cpu: 'Intel i5', sampleData: true });
  expect(readDeviceSpecifications(null)).toEqual({ data: null, invalid: false });
  for (const input of ['{bad', '[]', '{"cpu":123}', ' '.repeat(65537)]) expect(readDeviceSpecifications(input).invalid).toBe(true);
});

it('does not infer a live connection from an old timestamp or a missing field', () => {
  expect(connectionStatus({ isRevoked: true, isOnline: true }).label).toBe('Đã thu hồi');
  expect(connectionStatus({ isRevoked: false, isOnline: true }).label).toBe('Đang kết nối');
  expect(connectionStatus({ isRevoked: false, isOnline: false }).label).toBe('Ngoại tuyến');
  expect(connectionStatus({ isRevoked: false }).label).toBe('Chưa rõ kết nối');
  expect(displayDate('bad timestamp')).toBe('Chưa có thông tin');
  expect(displayDate(null)).toBe('Chưa có thông tin');
  expect(displayDate('2026-10-24T09:30:00Z')).not.toContain('2026-10-24T');
  expect(displayDate('2026-10-24T09:30:00Z', false)).not.toContain(':');
});
