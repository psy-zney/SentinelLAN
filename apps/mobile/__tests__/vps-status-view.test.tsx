import React from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import VpsStatusScreen from '../src/app/vps-status';
import * as Auth from '../src/features/auth/auth-context';
import { ApiError, MobileApiClient } from '../src/lib/api/client';
import { vpsStatus } from '../test-support/vps-fixture';

const id = '00000000-0000-4000-8000-000000000001';
const clients: QueryClient[] = [];
let api: MobileApiClient;
let auth: ReturnType<typeof Auth.useAuth>;
let changes: jest.SpiedFunction<typeof AppState.addEventListener>;

async function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  clients.push(client);
  return render(<QueryClientProvider client={client}><VpsStatusScreen /></QueryClientProvider>);
}

async function changeAppState(value: AppStateStatus) {
  await act(() => {
    for (const [event, listener] of changes.mock.calls) if (event === 'change') listener(value);
  });
}

beforeEach(() => {
  api = new MobileApiClient();
  auth = {
    status: 'authenticated',
    user: { id, email: 'admin@example.test', displayName: 'Admin', role: 'Admin', organizationId: id, organizationCode: 'demo' },
    apiClient: api, login: jest.fn(), logout: jest.fn(), logoutAll: jest.fn(), retrySession: jest.fn(), dismissSessionExpired: jest.fn(),
  };
  jest.spyOn(Auth, 'useAuth').mockImplementation(() => auth);
  changes = jest.spyOn(AppState, 'addEventListener');
});

afterEach(() => {
  clients.splice(0).forEach(client => client.clear());
  jest.useRealTimers();
  jest.restoreAllMocks();
});

it('shows real host metrics and expandable services and ports to an Admin', async () => {
  jest.spyOn(api, 'hostStatus').mockResolvedValue(vpsStatus());
  await mount();
  await screen.findByText('VPS công ty');
  expect(screen.getByText('CPU: 12.5%')).toBeTruthy();
  expect(screen.getByText('RAM: 45.0%')).toBeTruthy();
  expect(screen.getByText('Ổ đĩa: 70.0%')).toBeTruthy();
  expect(screen.getByRole('progressbar', { name: 'CPU' }).props.accessibilityValue.now).toBe(12.5);
  expect(screen.getByText('3.6 GiB / 8.0 GiB')).toBeTruthy();
  expect(screen.getByText('Thời gian hoạt động: 1 ngày 1 giờ')).toBeTruthy();
  expect(screen.getByText('1/1 container đang chạy')).toBeTruthy();
  expect(screen.getByText('CPU container: 125.0%')).toBeTruthy();
  expect(screen.getByText('RAM container: 128 MiB / 8 GiB (1.6%)')).toBeTruthy();
  expect(screen.queryByText('nginx.service')).toBeNull();
  await fireEvent.press(screen.getByText('Xem dịch vụ hệ thống (1)'));
  expect(screen.getByText('nginx.service')).toBeTruthy();
  expect(screen.getByText('Đang hoạt động · Tự khởi động')).toBeTruthy();
  await fireEvent.press(screen.getByText('Xem cổng đang lắng nghe (1)'));
  expect(screen.getByText('127.0.0.1:8080/TCP · docker-proxy')).toBeTruthy();
  expect(screen.getByText(/không xác nhận cổng truy cập được từ Internet/)).toBeTruthy();
  await fireEvent.press(screen.getByText('Ẩn cổng đang lắng nghe (1)'));
  await fireEvent.press(screen.getByText('Ẩn dịch vụ hệ thống (1)'));
  expect(screen.queryByText('nginx.service')).toBeNull();
});

it('shows loading without inventing host values', async () => {
  jest.spyOn(api, 'hostStatus').mockReturnValue(new Promise(() => {}));
  await mount();
  expect(screen.getByText('Đang tải trạng thái VPS…')).toBeTruthy();
  expect(screen.queryByRole('progressbar')).toBeNull();
});

it.each(['Employee', 'signed-out'] as const)('does not fetch VPS data for %s', async kind => {
  const load = jest.spyOn(api, 'hostStatus');
  if (kind === 'Employee') auth.user = { ...auth.user!, role: 'Employee' };
  else auth = { ...auth, status: 'unauthenticated', user: null };
  await mount();
  expect(load).not.toHaveBeenCalled();
  expect(screen.queryByText('Làm mới')).toBeNull();
  if (kind === 'Employee') expect(screen.getByText('Chỉ quản trị viên được xem trạng thái VPS.')).toBeTruthy();
});

it.each([
  [false, false, 'Chưa cấu hình giám sát VPS'],
  [true, false, 'Chưa đọc được trạng thái VPS'],
  [true, true, 'Chưa đọc được trạng thái VPS'],
])('distinguishes configured=%s available=%s with no snapshot', async (configured, available, title) => {
  jest.spyOn(api, 'hostStatus').mockResolvedValue({ configured, available, stale: false, snapshot: null, message: 'Thông tin từ máy chủ' });
  await mount();
  await screen.findByText(title);
  expect(screen.getByText('Thông tin từ máy chủ')).toBeTruthy();
  expect(screen.queryByRole('progressbar')).toBeNull();
});

it.each(['server', 'old', 'future'] as const)('marks %s snapshots as stale', async kind => {
  const payload = vpsStatus();
  if (kind === 'server') payload.stale = true;
  else payload.snapshot!.capturedAtUtc = new Date(Date.now() + (kind === 'old' ? -181000 : 61000)).toISOString();
  jest.spyOn(api, 'hostStatus').mockResolvedValue(payload);
  await mount();
  await screen.findByText(/Dữ liệu cũ hoặc chưa xác minh lại được/);
  expect(screen.queryByText('Đã nhận dữ liệu mới.')).toBeNull();
});

it('keeps the last snapshot visibly stale on failed refresh and recovers only after success', async () => {
  const load = jest.spyOn(api, 'hostStatus').mockResolvedValueOnce(vpsStatus())
    .mockRejectedValueOnce(new Error('offline')).mockResolvedValue(vpsStatus());
  await mount(); await screen.findByText('VPS công ty');
  await fireEvent.press(screen.getByText('Làm mới'));
  await screen.findByText(/Không tải được trạng thái VPS/);
  expect(screen.getByText('CPU: 12.5%')).toBeTruthy();
  expect(screen.getByText(/Dữ liệu cũ hoặc chưa xác minh lại được/)).toBeTruthy();
  await fireEvent.press(screen.getByText('Làm mới'));
  await screen.findByText('Đã nhận dữ liệu mới.');
  expect(load).toHaveBeenCalledTimes(3);
  expect(screen.queryByText(/Không tải được trạng thái VPS/)).toBeNull();
});

it.each([401, 403])('conceals prior host details on HTTP %s and stops polling', async status => {
  jest.useFakeTimers();
  const load = jest.spyOn(api, 'hostStatus').mockResolvedValueOnce(vpsStatus()).mockRejectedValue(new ApiError(status));
  await mount(); await screen.findByText('VPS công ty');
  await fireEvent.press(screen.getByText('Làm mới'));
  await screen.findByText(status === 403 ? /không được phép xem VPS/ : /Phiên đăng nhập đã hết hạn/);
  expect(screen.queryByText('vps.example.test')).toBeNull();
  expect(screen.queryByRole('progressbar')).toBeNull();
  await act(() => jest.advanceTimersByTime(120000));
  expect(load).toHaveBeenCalledTimes(2);
});

it('polls only while active, refreshes on return and clears its timers on unmount', async () => {
  jest.useFakeTimers();
  const load = jest.spyOn(api, 'hostStatus').mockImplementation(async () => vpsStatus());
  const view = await mount(); await screen.findByText('VPS công ty');
  await act(() => jest.advanceTimersByTime(60000));
  expect(load).toHaveBeenCalledTimes(2);
  await changeAppState('background');
  await act(() => jest.advanceTimersByTime(120000));
  expect(load).toHaveBeenCalledTimes(2);
  await changeAppState('active');
  await waitFor(() => expect(load).toHaveBeenCalledTimes(3));
  await view.unmount();
  await act(() => jest.advanceTimersByTime(120000));
  expect(load).toHaveBeenCalledTimes(3);
});

it('ages a snapshot locally even if the server stale flag stays false', async () => {
  jest.useFakeTimers();
  jest.spyOn(api, 'hostStatus').mockResolvedValue(vpsStatus());
  await mount(); await screen.findByText('Đã nhận dữ liệu mới.');
  await act(() => jest.advanceTimersByTime(195000));
  expect(screen.getByText(/Dữ liệu cũ hoặc chưa xác minh lại được/)).toBeTruthy();
});

it('cancels in-flight HTTP requests when backgrounded and when unmounted', async () => {
  const load = jest.spyOn(api, 'hostStatus').mockReturnValue(new Promise(() => {}));
  const view = await mount();
  await waitFor(() => expect(load).toHaveBeenCalledTimes(1));
  const first = load.mock.calls[0][0]!;
  expect(first.aborted).toBe(false);
  await changeAppState('background');
  expect(first.aborted).toBe(true);
  await changeAppState('active');
  await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  const second = load.mock.calls[1][0]!;
  await view.unmount();
  expect(second.aborted).toBe(true);
});

it('does not reuse host data across accounts or organizations', async () => {
  const load = jest.spyOn(api, 'hostStatus').mockResolvedValueOnce(vpsStatus()).mockReturnValue(new Promise(() => {}));
  const view = await mount(); await screen.findByText('vps.example.test');
  auth.user = { ...auth.user!, id: 'other-user', organizationId: 'other-org' };
  await view.rerender(<QueryClientProvider client={clients[0]}><VpsStatusScreen /></QueryClientProvider>);
  expect(screen.queryByText('vps.example.test')).toBeNull();
  expect(screen.queryByRole('progressbar')).toBeNull();
  await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
});

it('reports Docker and collector failures without displaying zero utilization', async () => {
  const payload = vpsStatus();
  payload.snapshot!.warnings = ['Dịch vụ đang khởi động lại'];
  payload.snapshot!.runtime.dockerAvailable = false;
  payload.snapshot!.runtime.dockerError = 'Docker chưa chạy';
  jest.spyOn(api, 'hostStatus').mockResolvedValue(payload);
  await mount(); await screen.findByText('Chưa đọc được Docker.');
  expect(screen.getByText('Docker chưa chạy')).toBeTruthy();
  expect(screen.getByText('Dịch vụ đang khởi động lại')).toBeTruthy();
  expect(screen.queryByText('sentinellan-api')).toBeNull();
});

it('distinguishes absent health checks, unavailable container metrics, small byte counts and IPv6', async () => {
  const payload = vpsStatus();
  const snapshot = payload.snapshot!;
  snapshot.uptimeSeconds = 3660; snapshot.cpuPercent = 95;
  snapshot.memoryUsedBytes = 0; snapshot.memoryTotalBytes = 1024 ** 2;
  snapshot.runtime.systemState = 'degraded';
  snapshot.runtime.containers[0] = { ...snapshot.runtime.containers[0], state: 'exited', health: null, project: null, cpuPercent: null, memoryUsage: null, memoryPercent: null };
  snapshot.runtime.services = [{ name: 'custom.service', activeState: 'custom-state', startupState: 'static' }];
  snapshot.listeningPorts = [{ address: '::1', port: 443, protocol: 'tcp', process: null }];
  jest.spyOn(api, 'hostStatus').mockResolvedValue(payload);
  await mount(); await screen.findByText('Chưa có kiểm tra sức khỏe.');
  expect(screen.getByText('CPU container: Chưa có số liệu')).toBeTruthy();
  expect(screen.getByText('RAM container: Chưa có số liệu')).toBeTruthy();
  expect(screen.getByText('0/1 container đang chạy')).toBeTruthy();
  expect(screen.getByText('0 B / 1.0 MiB')).toBeTruthy();
  expect(screen.getByText('Thời gian hoạt động: 1 giờ 1 phút')).toBeTruthy();
  await fireEvent.press(screen.getByText('Xem dịch vụ hệ thống (1)'));
  expect(screen.getByText('custom-state · Theo dịch vụ phụ thuộc')).toBeTruthy();
  await fireEvent.press(screen.getByText('Xem cổng đang lắng nghe (1)'));
  expect(screen.getByText('[::1]:443/TCP · Chưa xác định tiến trình')).toBeTruthy();
});

it('shows unhealthy containers and honest empty service and port lists', async () => {
  const payload = vpsStatus();
  payload.snapshot!.runtime.containers[0].health = 'unhealthy';
  payload.snapshot!.runtime.services = [];
  payload.snapshot!.listeningPorts = [];
  jest.spyOn(api, 'hostStatus').mockResolvedValue(payload);
  await mount(); await screen.findByText('Kiểm tra sức khỏe: Không đạt');
  await fireEvent.press(screen.getByText('Xem dịch vụ hệ thống (0)'));
  await fireEvent.press(screen.getByText('Xem cổng đang lắng nghe (0)'));
  expect(screen.getByText('Không có dữ liệu dịch vụ.')).toBeTruthy();
  expect(screen.getByText('Không có dữ liệu cổng đang lắng nghe.')).toBeTruthy();
});

it('shows an empty Docker inventory without claiming that containers are healthy', async () => {
  const payload = vpsStatus(); payload.snapshot!.runtime.containers = [];
  jest.spyOn(api, 'hostStatus').mockResolvedValue(payload);
  await mount(); await screen.findByText('Không có container trong bản ghi.');
  expect(screen.getByText('0/0 container đang chạy')).toBeTruthy();
  expect(screen.queryByText('Kiểm tra sức khỏe: Tốt')).toBeNull();
});
