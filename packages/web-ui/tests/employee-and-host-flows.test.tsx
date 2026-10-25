// @vitest-environment jsdom
import React from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { auth, fakeApi, renderProviders, resetApi } from './helpers';
import { SupportCenterView } from '../src/components/support-center-view';
import { ApiError } from '../src/lib/api-client';
import { VpsStatusView } from '../src/components/vps-status-view';
import { MyDeviceView } from '../src/components/my-device-view';
import { VpsServiceDiagram } from '../src/components/vps-service-diagram';

vi.mock('@/lib/api-client', async () => {
  const { fakeApi } = await import('./helpers');
  return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number, message = 'api') { super(message); } } };
});
vi.mock('@/components/app-shell', async () => { const { auth } = await import('./helpers'); return { useCurrentSession: () => auth.session }; });
vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: () => {} }));
vi.mock('next/image', () => ({ default: ({ unoptimized, ...props }: React.ImgHTMLAttributes<HTMLImageElement> & { unoptimized?: boolean }) => { void unoptimized; return React.createElement('img', { alt: '', ...props }); } }));

const date = '2026-10-23T08:00:00Z';
const request = { id: 'request-1', deviceId: 'device-1', deviceName: 'Office PC', userId: 'staff', userName: 'Staff', kind: 'Incident', category: 'Network', title: 'Office network issue', description: 'Cannot reach printer', canWork: false, status: 'Open', assignedTechnicianId: null, assignedTechnicianName: null, catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: null, createdAt: date, updatedAt: date, approvalExpiresAt: null };

beforeEach(() => {
  resetApi(); auth.session = { ...auth.session, role: 'Employee' };
  fakeApi.supportRequests.mockResolvedValue([request]); fakeApi.supportRequest.mockResolvedValue(request);
  fakeApi.supportMessages.mockResolvedValue([{ id: 'message-1', authorName: 'IT Support', body: 'Please check the cable', createdAt: date }]);
  fakeApi.supportAttachments.mockResolvedValue([]);
  fakeApi.createSupportRequest.mockResolvedValue(request);
  Object.defineProperty(window, 'matchMedia', { configurable: true, value: vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn(), addListener: vi.fn(), removeListener: vi.fn() })) });
  HTMLElement.prototype.scrollIntoView = vi.fn();
  URL.createObjectURL = vi.fn(() => 'blob:evidence'); URL.revokeObjectURL = vi.fn();
});

it('retries a disconnected request list and changes the server filter', async () => {
  fakeApi.supportRequests.mockRejectedValueOnce(new Error('offline'));
  renderProviders(<SupportCenterView />);
  await screen.findByText(/Chưa kết nối được với IT/);
  fireEvent.click(screen.getByRole('button', { name: 'Thử kết nối lại' }));
  await screen.findByText('Office network issue');
  fireEvent.change(screen.getByLabelText('Hiển thị'), { target: { value: 'all' } });
  await waitFor(() => expect(fakeApi.supportRequests).toHaveBeenLastCalledWith(0, 100, false));
  expect(screen.queryByText('Duyệt yêu cầu')).toBeNull();
});

it('preserves the same request key after a failed submission, then opens the conversation', async () => {
  fakeApi.createSupportRequest.mockRejectedValueOnce(new ApiError(409));
  renderProviders(<SupportCenterView />);
  await screen.findByText('Office network issue');
  fireEvent.click(screen.getByRole('button', { name: /Tôi gặp sự cố/ }));
  fireEvent.change(screen.getByLabelText('Bạn gặp vấn đề gì?'), { target: { value: 'Printer' } });
  fireEvent.change(screen.getByLabelText('Bạn có tiếp tục làm việc được không?'), { target: { value: 'false' } });
  fireEvent.change(screen.getByLabelText('Mô tả ngắn (không bắt buộc)'), { target: { value: ' Broken printer ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Gửi yêu cầu' }));
  await screen.findByText(/Yêu cầu đã thay đổi hoặc mã đã dùng/);
  const first = fakeApi.createSupportRequest.mock.calls[0][0];
  expect(first).toMatchObject({ kind: 'Incident', category: 'Printer', title: 'Broken printer', canWork: false, confirmed: true });
  fireEvent.click(screen.getByRole('button', { name: 'Gửi yêu cầu' }));
  await screen.findByText('Please check the cable');
  expect(fakeApi.createSupportRequest.mock.calls[1][0].idempotencyKey).toBe(first.idempotencyKey);
  expect(fakeApi.supportTeam).not.toHaveBeenCalled();
});

it.each([['Tôi nghi máy bị nhiễm virus', 'Panic'], ['Xin tạm dừng 15 phút', 'PauseAgent'], ['Xin gỡ SentinelLAN', 'UninstallAgent']])('requires explicit confirmation for %s', async (button, kind) => {
  renderProviders(<SupportCenterView />);
  await screen.findByText('Office network issue');
  fireEvent.click(screen.getByRole('button', { name: button, hidden: true }));
  const submit = screen.getByRole('button', { name: 'Gửi yêu cầu' });
  expect((submit as HTMLButtonElement).disabled).toBe(true);
  fireEvent.submit(submit.closest('form')!);
  expect(fakeApi.createSupportRequest).not.toHaveBeenCalled();
  fireEvent.click(screen.getByLabelText('Tôi hiểu thao tác và xác nhận gửi yêu cầu cho máy được giao cho tôi.'));
  fireEvent.click(submit);
  await waitFor(() => expect(fakeApi.createSupportRequest).toHaveBeenCalledWith(expect.objectContaining({ kind, confirmed: true })));
});

it('retries a message using the same key and clears the body only after success', async () => {
  fakeApi.sendSupportMessage.mockRejectedValueOnce(new ApiError(403));
  renderProviders(<SupportCenterView initialRequestId={request.id} />);
  await screen.findByText('Please check the cable');
  fireEvent.change(screen.getByLabelText('Tin nhắn'), { target: { value: ' Still broken ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Gửi tin nhắn' }));
  await screen.findByText('Bạn chưa được phép thực hiện thao tác này.');
  const key = fakeApi.sendSupportMessage.mock.calls[0][2];
  expect((screen.getByLabelText('Tin nhắn') as HTMLTextAreaElement).value).toBe(' Still broken ');
  fireEvent.click(screen.getByRole('button', { name: 'Gửi tin nhắn' }));
  await waitFor(() => expect((screen.getByLabelText('Tin nhắn') as HTMLTextAreaElement).value).toBe(''));
  expect(fakeApi.sendSupportMessage).toHaveBeenLastCalledWith(request.id, 'Still broken', key);
});

it('requires image preview consent, discards a preview, then uploads only the employee-selected image', async () => {
  renderProviders(<SupportCenterView initialRequestId={request.id} />);
  await screen.findByText('Please check the cable');
  const upload = screen.getByLabelText('Chọn ảnh');
  fireEvent.change(upload, { target: { files: [new File(['data'], 'private.txt', { type: 'text/plain' })] } });
  expect(screen.getByRole('alert')).toBeTruthy();
  expect(fakeApi.uploadSupportAttachment).not.toHaveBeenCalled();
  const image = new File(['image'], 'office.png', { type: 'image/png' });
  fireEvent.change(upload, { target: { files: [image] } });
  expect(screen.getByAltText('Ảnh xem trước: office.png')).toBeTruthy();
  expect((screen.getByRole('button', { name: 'Gửi ảnh' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Bỏ ảnh' }));
  expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:evidence');
  fireEvent.change(upload, { target: { files: [image] } });
  fireEvent.click(screen.getByLabelText('Tôi đã xem lại ảnh và đồng ý gửi cho IT.'));
  fireEvent.click(screen.getByRole('button', { name: 'Gửi ảnh' }));
  await waitFor(() => expect(fakeApi.uploadSupportAttachment).toHaveBeenCalledWith(request.id, expect.objectContaining({ fileName: 'office.png', contentType: 'image/png', base64: expect.any(String) })));
  await waitFor(() => expect(screen.queryByAltText('Ảnh xem trước: office.png')).toBeNull());
});

it('redeems a maintenance code only after device confirmation and clears the code after success', async () => {
  fakeApi.supportRequest.mockResolvedValue({ ...request, kind: 'PauseAgent', status: 'Approved' });
  renderProviders(<SupportCenterView initialRequestId={request.id} />);
  await screen.findByLabelText('Mã xác nhận IT cấp');
  fireEvent.change(screen.getByLabelText('Mã xác nhận IT cấp'), { target: { value: '12345678' } });
  expect((screen.getByRole('button', { name: 'Xác nhận bằng mã' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText(/Tôi xác nhận tạm dừng/));
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận bằng mã' }));
  await waitFor(() => expect(fakeApi.redeemMaintenanceCode).toHaveBeenCalledWith(request.id, '12345678', true));
  await waitFor(() => expect((screen.getByLabelText('Mã xác nhận IT cấp') as HTMLInputElement).value).toBe(''));
});

it('marks an outage, acknowledges a notice, reads a notification and follows help to a printer report', async () => {
  fakeApi.announcements.mockResolvedValue([{ id: 'notice-1', title: 'Network maintenance', body: 'Office network unavailable', isOutage: true, requiresAcknowledgement: true, startsAt: date, endsAt: date, acknowledged: false, affected: false }]);
  fakeApi.employeeNotifications.mockResolvedValue([{ id: 'notification-1', title: 'IT replied', body: 'View support request', requestId: request.id, readAt: null, createdAt: date }]);
  fakeApi.helpArticles.mockResolvedValue([{ id: 'help-1', category: 'Printer', title: 'Check printer cable', steps: ['Check power', 'Check cable'] }]);
  renderProviders(<SupportCenterView />);
  await screen.findByText('Office network issue');
  fireEvent.click(screen.getByRole('button', { name: 'Tôi cũng bị ảnh hưởng' }));
  await waitFor(() => expect(fakeApi.markAnnouncementAffected).toHaveBeenCalledWith('notice-1'));
  fireEvent.click(screen.getByRole('button', { name: 'Thông báo công ty' }));
  fireEvent.click(screen.getByRole('button', { name: 'Tôi đã đọc' }));
  await waitFor(() => expect(fakeApi.acknowledgeAnnouncement).toHaveBeenCalledWith('notice-1'));
  fireEvent.click(screen.getByRole('button', { name: 'Thông báo (1)' }));
  fireEvent.click(screen.getByRole('button', { name: 'Đánh dấu đã đọc' }));
  await waitFor(() => expect(fakeApi.readEmployeeNotification).toHaveBeenCalledWith('notification-1'));
  fireEvent.click(screen.getByRole('button', { name: 'Xem yêu cầu' }));
  await screen.findByText('Please check the cable');
  fireEvent.click(screen.getByRole('button', { name: 'Hướng dẫn nhanh' }));
  fireEvent.click(screen.getByRole('button', { name: 'Vẫn chưa được — nhờ IT', hidden: true }));
  expect((screen.getByLabelText('Bạn gặp vấn đề gì?') as HTMLSelectElement).value).toBe('Printer');
});

it('host authorization failure removes a previously visible snapshot instead of showing it as fresh', async () => {
  fakeApi.hostStatus.mockResolvedValueOnce({ configured: true, available: true, stale: false, message: '', snapshot: hostSnapshot() }).mockRejectedValueOnce(new ApiError(403));
  renderProviders(<VpsStatusView />);
  await screen.findByRole('heading', { name: 'Office Host · 192.0.2.1' });
  fireEvent.click(screen.getByRole('button', { name: /Làm mới|Cập nhật|Tải lại/ }));
  await screen.findByText('Bạn cần quyền quản trị hệ thống để xem tình trạng máy chủ.');
  expect(screen.queryByRole('heading', { name: 'Office Host · 192.0.2.1' })).toBeNull();
});

function hostSnapshot() {
  return {
    name: 'Office Host', host: '192.0.2.1', capturedAtUtc: new Date().toISOString(), osInfo: 'Linux', uptimeSeconds: 90000,
    cpuPercent: 85, ramPercent: 70, diskPercent: 30, memoryTotalBytes: 1024 ** 3, memoryUsedBytes: 512 * 1024 ** 2, diskTotalBytes: 10 * 1024 ** 3, diskUsedBytes: 3 * 1024 ** 3, warnings: ['Collector is read-only'],
    runtime: { systemState: 'running', dockerAvailable: true, dockerError: null, services: [{ name: 'nginx', activeState: 'active', startupState: 'enabled' }, { name: 'beatsync', activeState: 'inactive', startupState: 'disabled' }], containers: [
      { id: 'container-1', project: 'monopoly', name: 'monopoly-server-1', image: 'game:test', state: 'running', status: 'Up', health: 'healthy', cpuPercent: 25, memoryUsage: '100MiB', memoryPercent: 10, ports: [{ hostIp: '127.0.0.1', hostPort: 9004, containerPort: 3001, protocol: 'tcp' }, { hostIp: null, hostPort: null, containerPort: 5000, protocol: 'tcp' }], listeningPorts: [{ address: '0.0.0.0', port: 3001, protocol: 'tcp', process: 'node' }] },
      { id: 'container-2', project: 'other-project', name: 'other-service', image: 'service:test', state: 'exited', status: 'Exited', health: null, cpuPercent: null, memoryUsage: null, memoryPercent: null, ports: [], listeningPorts: [] },
    ], storageBreakdown: [{ name: 'Repository', path: '/srv/repo', category: 'repo', sizeBytes: 1024 ** 3, reclaimable: null }, { name: 'Docker storage', path: '/var/lib/docker', category: 'docker', sizeBytes: 2 * 1024 ** 3, reclaimable: '1GiB' }] },
    listeningPorts: [{ address: '127.0.0.1', port: 9004, protocol: 'tcp', process: 'docker-proxy' }, { address: '127.0.0.1', port: 9000, protocol: 'tcp', process: 'nginx' }, { address: '0.0.0.0', port: 50000, protocol: 'udp', process: null }],
  };
}

it('host details distinguish Docker publication, EXPOSE metadata and listening sockets while filtering ports', async () => {
  fakeApi.hostStatus.mockResolvedValue({ configured: true, available: true, stale: false, message: '', snapshot: hostSnapshot() });
  renderProviders(<VpsStatusView />);
  await screen.findByRole('heading', { name: 'Office Host · 192.0.2.1' });
  fireEvent.click(screen.getByRole('button', { name: 'Docker' }));
  fireEvent.click(screen.getByRole('button', { name: /monopoly-server-1.*running/ }));
  expect(screen.getAllByText('127.0.0.1:9004 → 3001/tcp').length).toBeGreaterThan(0);
  expect(screen.getAllByText('5000/tcp').length).toBeGreaterThan(0);
  const portsToggle = screen.getByRole('button', { name: /Cổng đang nghe trên VPS/ });
  fireEvent.click(portsToggle);
  fireEvent.change(screen.getByLabelText('Tìm cổng'), { target: { value: '9004' } });
  fireEvent.click(screen.getByRole('button', { name: 'Chi tiết cổng 127.0.0.1:9004/tcp' }));
  expect(screen.getAllByText('127.0.0.1:9004/tcp').length).toBeGreaterThan(0);
  fireEvent.change(screen.getByLabelText('Tìm cổng'), { target: { value: 'not-present' } });
  expect(screen.getAllByText('Không có cổng phù hợp bộ lọc.').length).toBeGreaterThan(0);
  fireEvent.click(screen.getByRole('button', { name: 'UDP (1)' }));
  fireEvent.change(screen.getByLabelText('Tìm cổng'), { target: { value: '' } });
  fireEvent.click(screen.getByRole('button', { name: 'Mở rộng tất cả' }));
  expect(screen.getByRole('button', { name: 'Chi tiết cổng 0.0.0.0:50000/udp' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Thu gọn tất cả' }));
});

it('assigned device report preserves its request key on failure and refreshes incident history on success', async () => {
  fakeApi.myDevice.mockResolvedValue({ name: 'Assigned PC', osVersion: 'Windows', agentVersion: '1.0', isOnline: false, transparencyManifest: { dataRetentionDays: 30, collectedTelemetry: ['CPU and memory'], privacyBoundaries: ['No screen capture'] } });
  fakeApi.reportMyDeviceIncident.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({});
  vi.spyOn(window, 'alert').mockImplementation(() => {});
  renderProviders(<MyDeviceView />);
  await screen.findByRole('heading', { name: 'Assigned PC' });
  expect(screen.getByText('No screen capture')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Nhờ IT hỗ trợ' }));
  const dialog = screen.getByRole('dialog');
  fireEvent.change(within(dialog).getByPlaceholderText('VD: Máy khởi động chậm, quạt kêu to, bàn phím hỏng...'), { target: { value: ' Printer failed ' } });
  fireEvent.change(screen.getByLabelText('Bạn còn làm việc được không?'), { target: { value: 'High' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Gửi báo cáo' }));
  await waitFor(() => expect(window.alert).toHaveBeenCalled());
  const key = fakeApi.reportMyDeviceIncident.mock.calls[0][0].idempotencyKey;
  fireEvent.click(within(dialog).getByRole('button', { name: 'Gửi báo cáo' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(fakeApi.reportMyDeviceIncident).toHaveBeenLastCalledWith({ title: 'Printer failed', description: undefined, severity: 'High', idempotencyKey: key });
  expect(fakeApi.myDeviceIncidents.mock.calls.length).toBeGreaterThanOrEqual(2);
});

it('admin publishes a verified catalog package only with confirmation and can retry a server rejection', async () => {
  auth.session.role = 'Admin';
  fakeApi.saveCatalogApp.mockRejectedValueOnce(new ApiError(409));
  renderProviders(<SupportCenterView />);
  await screen.findByText('Office network issue');
  fireEvent.click(screen.getByRole('button', { name: 'Phần mềm' }));
  fireEvent.click(screen.getByRole('button', { name: 'Thêm phần mềm được phép' }));
  const fields = { 'Tên phần mềm': 'Approved Office', 'Ứng dụng giúp nhân viên làm gì?': 'Read documents', 'Phiên bản': '2.0', 'URL HTTPS của gói MSI': 'https://downloads.example/office.msi', 'SHA-256 của gói cài': 'a'.repeat(64), 'Thumbprint chứng thư nhà phát hành': 'b'.repeat(40), 'Lý do công bố / sửa đổi': 'Verified release', 'MSI ProductCode (UUID, tùy chọn)': '00000000-0000-4000-8000-000000000001', 'MSI UpgradeCode (UUID, tùy chọn)': '00000000-0000-4000-8000-000000000002' };
  for (const [label, value] of Object.entries(fields)) fireEvent.change(screen.getByLabelText(label), { target: { value } });
  fireEvent.click(screen.getByLabelText('Đang cung cấp'));
  fireEvent.click(screen.getByLabelText('Yêu cầu Admin duyệt trước khi cài'));
  const save = screen.getByRole('button', { name: 'Lưu phần mềm' });
  expect((save as HTMLButtonElement).disabled).toBe(true);
  fireEvent.submit(save.closest('form')!);
  expect(fakeApi.saveCatalogApp).not.toHaveBeenCalled();
  fireEvent.click(screen.getByLabelText('Tôi xác minh gói cài, nhà phát hành và xác nhận công bố.'));
  fireEvent.click(save);
  await screen.findByText(/Yêu cầu đã thay đổi hoặc mã đã dùng/);
  fireEvent.click(save);
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Lưu phần mềm' })).toBeNull());
  expect(fakeApi.saveCatalogApp).toHaveBeenLastCalledWith(expect.objectContaining({ name: 'Approved Office', confirmed: true, isActive: false, requiresApproval: false, reason: 'Verified release' }), undefined);
});

it('admin sends a scoped maintenance announcement after dates, reason and confirmation are entered', async () => {
  auth.session.role = 'Admin';
  renderProviders(<SupportCenterView />);
  await screen.findByText('Office network issue');
  fireEvent.click(screen.getByRole('button', { name: 'Thông báo công ty' }));
  fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo / lịch bảo trì' }));
  for (const [label, value] of Object.entries({ 'Tiêu đề': 'Maintenance window', 'Nội dung': 'Network interruption expected', 'Bắt đầu': '2026-10-24T08:00', 'Kết thúc': '2026-10-24T09:00', 'Lý do gửi': 'Approved maintenance' })) fireEvent.change(screen.getByLabelText(label), { target: { value } });
  fireEvent.click(screen.getByLabelText('Sự cố chung — cho phép nhân viên báo bị ảnh hưởng'));
  fireEvent.click(screen.getByLabelText('Yêu cầu xác nhận đã đọc'));
  expect((screen.getByRole('button', { name: 'Gửi thông báo' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('Tôi xác nhận gửi thông báo trong tổ chức.'));
  fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' }));
  await waitFor(() => expect(fakeApi.createAnnouncement).toHaveBeenCalledWith({ title: 'Maintenance window', body: 'Network interruption expected', startsAt: new Date('2026-10-24T08:00').toISOString(), endsAt: new Date('2026-10-24T09:00').toISOString(), reason: 'Approved maintenance', confirmed: true, isOutage: true, requiresAcknowledgement: true }));
});

it('admin updates ownership and approves a maintenance request with a temporary OTP that can be hidden', async () => {
  auth.session.role = 'Admin';
  fakeApi.supportRequest.mockResolvedValue({ ...request, kind: 'PauseAgent' });
  fakeApi.supportTeam.mockResolvedValue([{ id: 'technician-1', displayName: 'Office IT' }]);
  fakeApi.decideSupportRequest.mockResolvedValue({ request, otp: '12345678', expiresAt: date });
  renderProviders(<SupportCenterView initialRequestId={request.id} />);
  await screen.findByText('Please check the cable');
  expect((screen.getByRole('button', { name: 'Duyệt yêu cầu' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText('Người phụ trách'), { target: { value: 'technician-1' } });
  fireEvent.change(screen.getByLabelText('Lý do / nội dung xử lý'), { target: { value: 'Authorized maintenance' } });
  fireEvent.click(screen.getByLabelText('Tôi xác nhận thao tác cho yêu cầu và máy này.'));
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật xử lý' }));
  await waitFor(() => expect(fakeApi.updateSupportRequest).toHaveBeenCalledWith(request.id, expect.objectContaining({ assignedTechnicianId: 'technician-1', reason: 'Authorized maintenance', confirmed: true })));
  fireEvent.click(screen.getByRole('button', { name: 'Duyệt yêu cầu' }));
  await screen.findByText('Mã một lần: 12345678');
  expect(fakeApi.decideSupportRequest).toHaveBeenLastCalledWith(request.id, { approved: true, reason: 'Authorized maintenance', confirmed: true });
  fireEvent.click(screen.getByRole('button', { name: 'Ẩn mã' }));
  expect(screen.queryByText('Mã một lần: 12345678')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Từ chối' }));
  await waitFor(() => expect(fakeApi.decideSupportRequest).toHaveBeenLastCalledWith(request.id, { approved: false, reason: 'Authorized maintenance', confirmed: true }));
});

it('gateway drilldown identifies the container behind a project without claiming an Internet firewall check', () => {
  renderProviders(<VpsServiceDiagram snapshot={hostSnapshot()} vi />);
  fireEvent.click(screen.getByRole('button', { name: 'Xem container monopoly-server-1' }));
  expect(screen.getByRole('heading', { name: 'monopoly-server-1' })).toBeTruthy();
  expect(screen.getByText(/quyền truy cập từ Internet còn phụ thuộc firewall và reverse proxy/)).toBeTruthy();
  expect(screen.getByText('game:test')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Thu gọn' }));
  expect(screen.queryByRole('heading', { name: 'monopoly-server-1' })).toBeNull();
});

it('assigned-device absence and server errors offer different recovery states', async () => {
  fakeApi.myDevice.mockRejectedValueOnce(new ApiError(404)).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({ name: 'Assigned PC', osVersion: 'Windows', agentVersion: '1.0', isOnline: true, transparencyManifest: null });
  renderProviders(<MyDeviceView />);
  await screen.findByRole('heading', { name: 'Chưa có máy tính được giao cho bạn' });
  fireEvent.click(screen.getByRole('button', { name: /Làm mới|Cập nhật|Tải lại/ }));
  await screen.findByText('Chưa thể tải thiết bị được gán. Hãy liên hệ với Quản trị viên IT của bạn.');
  fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
  await screen.findByRole('heading', { name: 'Assigned PC' });
});

it('host refresh failure retains the previous snapshot only with an explicit stale warning', async () => {
  fakeApi.hostStatus.mockResolvedValueOnce({ configured: true, available: true, stale: false, message: '', snapshot: hostSnapshot() }).mockRejectedValueOnce(new Error('offline'));
  renderProviders(<VpsStatusView />);
  await screen.findByRole('heading', { name: 'Office Host · 192.0.2.1' });
  fireEvent.click(screen.getByRole('button', { name: /Làm mới|Cập nhật|Tải lại/ }));
  await screen.findByText('Dữ liệu cũ — chưa xác nhận trạng thái hiện tại');
  expect(screen.getByRole('heading', { name: 'Office Host · 192.0.2.1' })).toBeTruthy();
});
