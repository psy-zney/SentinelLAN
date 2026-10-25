import React from 'react';
import { AppState } from 'react-native';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Router from 'expo-router';
import * as Auth from '../src/features/auth/auth-context';
import DeviceDetail from '../src/app/device-detail';
import { OperatorView } from '../src/features/it-operator/operator-view';
import { ApiError, MobileApiClient } from '../src/lib/api/client';
import { operatorDeviceFixture, deviceTelemetryFixture, sampleDeviceId } from '../test-support/operator-device-fixture';

const clients: QueryClient[] = [];
const push = jest.fn();
let api: MobileApiClient;
let auth: ReturnType<typeof Auth.useAuth>;
let detail: jest.SpiedFunction<MobileApiClient['operatorDeviceDetail']>;
let telemetry: jest.SpiedFunction<MobileApiClient['operatorDeviceTelemetry']>;

async function mount(ui = <DeviceDetail />) {
  const query = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  clients.push(query);
  return render(<QueryClientProvider client={query}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  api = new MobileApiClient(); push.mockReset();
  auth = {
    status: 'authenticated', user: { id: sampleDeviceId, email: 'admin@example.test', displayName: 'Admin', role: 'Admin', organizationId: sampleDeviceId, organizationCode: 'demo' },
    apiClient: api, login: jest.fn(), logout: jest.fn(), logoutAll: jest.fn(), retrySession: jest.fn(), dismissSessionExpired: jest.fn(),
  };
  jest.spyOn(Auth, 'useAuth').mockImplementation(() => auth);
  jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push, replace: push });
  jest.mocked(Router.useLocalSearchParams).mockReturnValue({ deviceId: sampleDeviceId });
  detail = jest.spyOn(api, 'operatorDeviceDetail').mockResolvedValue(operatorDeviceFixture());
  telemetry = jest.spyOn(api, 'operatorDeviceTelemetry').mockResolvedValue([deviceTelemetryFixture()]);
});
afterEach(() => { clients.splice(0).forEach(client => client.clear()); jest.useRealTimers(); jest.restoreAllMocks(); });

it('opens a sample device from a clearly labelled, accessible list row', async () => {
  jest.spyOn(api, 'operatorDashboard').mockResolvedValue({ totalDevices: 1, onlineDevices: 0, offlineDevices: 1, openAlerts: 0 });
  jest.spyOn(api, 'operatorDevices').mockResolvedValue([operatorDeviceFixture()]);
  await mount(<OperatorView />);
  await screen.findByText('KT-PC-01');
  expect(screen.queryByText('[MẪU] KT-PC-01')).toBeNull();
  expect(screen.queryByText(/sample-only/)).toBeNull();
  expect(screen.getByText('Dữ liệu mẫu')).toBeTruthy();
  await fireEvent.press(screen.getByRole('button', { name: 'Xem chi tiết máy KT-PC-01' }));
  expect(push).toHaveBeenCalledWith({ pathname: '/device-detail', params: { deviceId: sampleDeviceId } });
});

it('searches the server, filters connection state and gives an actionable empty result', async () => {
  jest.spyOn(api, 'operatorDashboard').mockResolvedValue({ totalDevices: 0, onlineDevices: 0, offlineDevices: 0, openAlerts: 0 });
  const list = jest.spyOn(api, 'operatorDevices').mockResolvedValue([]);
  await mount(<OperatorView />);
  await fireEvent.changeText(screen.getByLabelText('Tìm máy'), 'KT & Windows');
  await waitFor(() => expect(list).toHaveBeenCalledWith(0, 100, { search: 'KT & Windows', status: 'all' }, expect.anything()));
  await fireEvent.press(screen.getByRole('button', { name: 'Lọc: Ngoại tuyến' }));
  await waitFor(() => expect(list).toHaveBeenCalledWith(0, 100, { search: 'KT & Windows', status: 'offline' }, expect.anything()));
  expect(screen.getByRole('button', { name: 'Lọc: Ngoại tuyến' }).props.accessibilityState.selected).toBe(true);
  expect(await screen.findByText(/Không tìm thấy máy phù hợp/)).toBeTruthy();
});

it('shows readable sample hardware, assigned staff, location, assets and explicitly fictional software', async () => {
  await mount(); await screen.findByText('KT-PC-01');
  for (const text of ['Dữ liệu mẫu', 'Máy minh họa', 'Dell', 'OptiPlex 7010', 'Máy tính để bàn', 'Nguyễn Minh Anh', 'Kế toán', 'Tầng 2', 'Intel Core i5-13400', '16 GB DDR4', '512 GB NVMe SSD', 'Số liệu minh họa', 'Phần mềm minh họa', 'Firefox ESR']) expect(screen.getByText(text)).toBeTruthy();
  expect(screen.getByText(/không phải kiểm kê phần mềm đã cài/)).toBeTruthy();
  expect(screen.getByRole('progressbar', { name: 'CPU' }).props.accessibilityValue.now).toBe(24);
  expect(screen.queryByText(/"sampleData"/)).toBeNull();
  expect(screen.queryByText('[MẪU] Nguyễn Minh Anh')).toBeNull();
  expect(detail).toHaveBeenCalledWith(sampleDeviceId, expect.anything());
});

it('shows real machines without sample labels or fabricated software inventory', async () => {
  detail.mockResolvedValue({ ...operatorDeviceFixture(), name: 'OFFICE-PC', agentVersion: '1.0', isOnline: true, assetType: 'Laptop', specificationsJson: JSON.stringify({ cpu: 'AMD Ryzen 5', sampleInstalledApplications: ['Not an inventory'] }) });
  await mount(); await screen.findByText('OFFICE-PC');
  expect(screen.getByText('Đang kết nối')).toBeTruthy();
  expect(screen.getByText('Máy tính xách tay')).toBeTruthy();
  expect(screen.getByText('Mức sử dụng tài nguyên')).toBeTruthy();
  expect(screen.queryByText('Dữ liệu mẫu')).toBeNull();
  expect(screen.queryByText('Phần mềm minh họa')).toBeNull();
  expect(screen.queryByText('Not an inventory')).toBeNull();
});

it.each(['Employee', 'signed-out'] as const)('blocks %s before fetching machine information', async kind => {
  if (kind === 'Employee') auth.user = { ...auth.user!, role: 'Employee' };
  else auth = { ...auth, status: 'unauthenticated', user: null };
  await mount();
  expect(detail).not.toHaveBeenCalled(); expect(telemetry).not.toHaveBeenCalled();
  expect(screen.queryByText('Làm mới thông tin máy')).toBeNull();
});

it.each([{}, { deviceId: '../other' }, { deviceId: [sampleDeviceId, 'other'] }])('rejects invalid route parameters %p', async params => {
  jest.mocked(Router.useLocalSearchParams).mockReturnValue(params);
  await mount();
  expect(detail).not.toHaveBeenCalled(); expect(telemetry).not.toHaveBeenCalled();
  await fireEvent.press(screen.getByText('Quay lại danh sách máy'));
  expect(push).toHaveBeenCalledWith('/(tabs)');
});

it.each([403, 404])('does not display profile or telemetry after HTTP %s', async status => {
  detail.mockRejectedValue(new ApiError(status));
  await mount(); await screen.findByText(/Không tìm thấy máy hoặc tài khoản không có quyền xem/);
  expect(screen.queryByText('OptiPlex 7010')).toBeNull();
  expect(screen.queryByRole('progressbar')).toBeNull();
});

it('keeps a readable profile when telemetry fails and retries without making up zero values', async () => {
  telemetry.mockRejectedValueOnce(new Error('offline')).mockResolvedValue([deviceTelemetryFixture()]);
  await mount(); await screen.findByText('OptiPlex 7010');
  expect(screen.getByText(/Chưa cập nhật được số liệu/)).toBeTruthy();
  expect(screen.queryByRole('progressbar')).toBeNull();
  await fireEvent.press(screen.getByText('Làm mới thông tin máy'));
  await waitFor(() => expect(screen.getByRole('progressbar', { name: 'CPU' })).toBeTruthy());
  expect(telemetry).toHaveBeenCalledTimes(2);
});

it('conceals previously loaded details if telemetry access is revoked', async () => {
  await mount(); await screen.findByText('OptiPlex 7010');
  telemetry.mockRejectedValue(new ApiError(403));
  await fireEvent.press(screen.getByText('Làm mới thông tin máy'));
  await screen.findByText(/Không tìm thấy máy hoặc tài khoản không có quyền xem/);
  expect(screen.queryByText('Nguyễn Minh Anh')).toBeNull();
  expect(screen.queryByRole('progressbar')).toBeNull();
});

it('handles missing configuration and an empty telemetry history honestly', async () => {
  detail.mockResolvedValue({ ...operatorDeviceFixture(), manufacturer: null, assignedUserName: null, specificationsJson: null, purchaseCost: null, vendorName: null, assetType: null, locationRoom: null });
  telemetry.mockResolvedValue([]);
  await mount(); await screen.findByText('Chưa có số liệu từ Agent.');
  expect(screen.getAllByText('Chưa có thông tin').length).toBeGreaterThan(2);
  expect(screen.getByText('Chưa phân công')).toBeTruthy();
  expect(screen.queryByRole('progressbar')).toBeNull();
});

it('reports malformed configuration without showing raw JSON and warns about old real telemetry', async () => {
  detail.mockResolvedValue({ ...operatorDeviceFixture(), name: 'REAL-PC', agentVersion: '1.0', specificationsJson: '{broken json', lastSeenAt: new Date().toISOString() });
  telemetry.mockResolvedValue([{ ...deviceTelemetryFixture(), collectedAt: new Date(Date.now() - 600000).toISOString() }]);
  await mount(); await screen.findByText(/Chưa đọc được thông số chi tiết/);
  expect(screen.queryByText('{broken json')).toBeNull();
  expect(screen.getByText(/Số liệu cũ hoặc chưa xác minh lại được/)).toBeTruthy();
});

it('allows retry after a failed profile request and never fabricates a machine name while loading', async () => {
  detail.mockRejectedValueOnce(new Error('offline')).mockResolvedValue(operatorDeviceFixture());
  await mount(); await screen.findByText(/Chưa tải được thông tin máy/);
  expect(screen.queryByText('KT-PC-01')).toBeNull();
  await fireEvent.press(screen.getByText('Làm mới thông tin máy'));
  await screen.findByText('KT-PC-01');
});

it('isolates details across accounts and organizations', async () => {
  const view = await mount(); await screen.findByText('Nguyễn Minh Anh');
  detail.mockReturnValue(new Promise(() => {}));
  telemetry.mockReturnValue(new Promise(() => {}));
  auth.user = { ...auth.user!, id: 'other-admin', organizationId: 'other-company' };
  await view.rerender(<QueryClientProvider client={clients[0]}><DeviceDetail /></QueryClientProvider>);
  expect(screen.queryByText('Nguyễn Minh Anh')).toBeNull();
  expect(screen.queryByText('OptiPlex 7010')).toBeNull();
  await waitFor(() => expect(detail).toHaveBeenCalledTimes(2));
});

it('cancels both requests when the app goes to the background', async () => {
  detail.mockReturnValue(new Promise(() => {})); telemetry.mockReturnValue(new Promise(() => {}));
  const changes = jest.spyOn(AppState, 'addEventListener');
  await mount(); await waitFor(() => expect(detail).toHaveBeenCalledTimes(1));
  const profileSignal = detail.mock.calls[0][1]!;
  const telemetrySignal = telemetry.mock.calls[0][1]!;
  await act(() => { for (const [event, listener] of changes.mock.calls) if (event === 'change') listener('background'); });
  expect(profileSignal.aborted).toBe(true); expect(telemetrySignal.aborted).toBe(true);
});
