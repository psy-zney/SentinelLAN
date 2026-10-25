// @vitest-environment jsdom
import React from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { auth, fakeApi, renderProviders, resetApi } from './helpers';
import { SupportCenterView } from '../src/components/support-center-view';
import { ApiError } from '../src/lib/api-client';

vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: vi.fn() }));

vi.mock('@/lib/api-client', async () => {
  const { fakeApi } = await import('./helpers');
  return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number, message = 'api unavailable') { super(message); } } };
});
vi.mock('@/components/app-shell', async () => { const { auth } = await import('./helpers'); return { useCurrentSession: () => auth.session }; });
vi.mock('next/image', () => ({ default: ({ unoptimized, ...props }: React.ImgHTMLAttributes<HTMLImageElement> & { unoptimized?: boolean }) => { void unoptimized; return React.createElement('img', { alt: '', ...props }); } }));

const date = '2026-10-23T08:00:00Z';
const request = { id: 'request-1', deviceId: 'device-1', deviceName: 'Office PC', userId: 'staff', userName: 'Staff', kind: 'PauseAgent', category: 'Other', title: 'Authorized maintenance', description: 'Scheduled maintenance request', canWork: true, status: 'Open', assignedTechnicianId: null, assignedTechnicianName: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: date, createdAt: date, updatedAt: date };
const catalog = { id: 'app-1', name: 'Office Editor', description: 'Create documents', version: '1.0', packageUrl: 'https://packages.example/editor.msi', sha256: 'a'.repeat(64), publisherThumbprint: 'b'.repeat(40), isActive: false, requiresApproval: true, productCode: null, upgradeCode: null };
function change(label: string, value: string) { fireEvent.change(screen.getByLabelText(label), { target: { value } }); }
async function openRequest() { renderProviders(<SupportCenterView initialRequestId={request.id} />); return screen.findByRole('region', { name: 'Chi tiết yêu cầu' }); }
async function confirmProcessing() { await screen.findByText(request.description); change('Lý do / nội dung xử lý', 'Authorized maintenance'); fireEvent.click(screen.getByLabelText('Tôi xác nhận thao tác cho yêu cầu và máy này.')); }

beforeEach(() => {
  resetApi();
  fakeApi.supportRequests.mockResolvedValue([request]); fakeApi.supportRequest.mockResolvedValue(request);
  fakeApi.supportMessages.mockResolvedValue([]); fakeApi.supportAttachments.mockResolvedValue([]);
  fakeApi.supportTeam.mockResolvedValue([{ id: 'technician', displayName: 'IT Technician' }]);
  fakeApi.catalogApps.mockResolvedValue([catalog]);
  URL.createObjectURL = vi.fn(() => 'blob:evidence'); URL.revokeObjectURL = vi.fn();
});

it('keeps approval behind reason and confirmation and displays a one-use code until explicitly hidden', async () => {
  fakeApi.decideSupportRequest.mockResolvedValue({ otp: '12345678', expiresAt: date });
  await openRequest(); await screen.findByText(request.description);
  expect((screen.getByRole('button', { name: 'Duyệt yêu cầu' }) as HTMLButtonElement).disabled).toBe(true);
  await confirmProcessing(); fireEvent.click(screen.getByRole('button', { name: 'Duyệt yêu cầu' }));
  await screen.findByText('Mã một lần: 12345678');
  expect(fakeApi.decideSupportRequest).toHaveBeenCalledWith(request.id, { approved: true, reason: 'Authorized maintenance', confirmed: true });
  fireEvent.click(screen.getByRole('button', { name: 'Ẩn mã' })); expect(screen.queryByText('Mã một lần: 12345678')).toBeNull();
});
it('shows the bounded default expiry when approval returns an OTP without an expiry field', async () => {
  fakeApi.decideSupportRequest.mockResolvedValue({ otp: '87654321' }); await openRequest(); await confirmProcessing();
  fireEvent.click(screen.getByRole('button', { name: 'Duyệt yêu cầu' })); await screen.findByText('Hết hạn: sau 5 phút. Chuyển riêng cho nhân viên sau khi xác minh; mã chỉ hiện tại đây lần này.');
});
it('rejects a sensitive request with the operator reason and preserves a conflict outcome for retry', async () => {
  fakeApi.decideSupportRequest.mockRejectedValueOnce(new ApiError(409)); await openRequest(); await confirmProcessing();
  fireEvent.click(screen.getByRole('button', { name: 'Từ chối' })); await screen.findByRole('alert');
  expect(fakeApi.decideSupportRequest).toHaveBeenCalledWith(request.id, { approved: false, reason: 'Authorized maintenance', confirmed: true });
  fireEvent.click(screen.getByRole('button', { name: 'Từ chối' })); await waitFor(() => expect(fakeApi.decideSupportRequest).toHaveBeenCalledTimes(2));
});
it('assigns an authorized technician and state while retaining a message draft after send failure', async () => {
  fakeApi.supportRequest.mockResolvedValue({ ...request, kind: 'Incident' });
  fakeApi.sendSupportMessage.mockRejectedValueOnce(new Error('Message disconnected')); await openRequest(); await confirmProcessing();
  change('Người phụ trách', 'technician'); change('Trạng thái', 'InProgress'); fireEvent.click(screen.getByRole('button', { name: 'Cập nhật xử lý' }));
  await waitFor(() => expect(fakeApi.updateSupportRequest).toHaveBeenCalledWith(request.id, { status: 'InProgress', assignedTechnicianId: 'technician', reason: 'Authorized maintenance', confirmed: true }));
  change('Tin nhắn', 'Please save your work'); fireEvent.click(screen.getByRole('button', { name: 'Gửi tin nhắn' }));
  await screen.findByText('Message disconnected'); const key = fakeApi.sendSupportMessage.mock.calls[0][2];
  fireEvent.click(screen.getByRole('button', { name: 'Gửi tin nhắn' })); await waitFor(() => expect(fakeApi.sendSupportMessage).toHaveBeenCalledTimes(2));
  expect(fakeApi.sendSupportMessage.mock.calls[1][2]).toBe(key); await waitFor(() => expect((screen.getByLabelText('Tin nhắn') as HTMLTextAreaElement).value).toBe(''));
});
it('allows support staff to process incidents without exposing administrator approval controls', async () => {
  auth.session.role = 'Technician'; fakeApi.supportRequest.mockResolvedValue({ ...request, kind: 'Incident', assignedTechnicianName: 'IT Technician', commandMessage: 'Simulation completed' });
  await openRequest(); await confirmProcessing(); expect(screen.queryByRole('button', { name: 'Duyệt yêu cầu' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật xử lý' })); await waitFor(() => expect(fakeApi.updateSupportRequest).toHaveBeenCalledWith(request.id, expect.objectContaining({ assignedTechnicianId: undefined })));
  expect(screen.getByText('IT Technician', { selector: 'strong' })).toBeTruthy(); expect(screen.getByText('Simulation completed')).toBeTruthy();
});
it.each([403, 409, 500])('presents an actionable processing failure for HTTP %s', async status => {
  fakeApi.updateSupportRequest.mockRejectedValue(new ApiError(status)); await openRequest(); await confirmProcessing();
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật xử lý' })); const alert = await screen.findByRole('alert');
  expect(alert.textContent).toMatch(status === 403 ? /chưa được phép/ : status === 409 ? /đã thay đổi/ : /api unavailable/);
});
it('retains request context while browsing a previously read notification and its linked request', async () => {
  fakeApi.employeeNotifications.mockResolvedValue([{ id: 'note-1', title: 'IT reply', body: 'Maintenance approved', readAt: date, createdAt: date, requestId: request.id }]);
  renderProviders(<SupportCenterView />); await screen.findByText(request.title); fireEvent.click(screen.getByRole('button', { name: 'Thông báo (0)' }));
  expect((screen.getByRole('button', { name: 'Đã đọc' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Xem yêu cầu' })); await screen.findByText(request.description);
});
it('previews only the attachment returned by the authorized API and surfaces download failures', async () => {
  fakeApi.supportAttachments.mockResolvedValue([{ id: 'image-1', fileName: 'error.png' }]); fakeApi.downloadSupportAttachment.mockRejectedValueOnce(new Error('Attachment denied')).mockResolvedValue(new Blob(['image'], { type: 'image/png' }));
  await openRequest(); await screen.findByText(request.description); fireEvent.click(screen.getByRole('button', { name: 'error.png' })); await screen.findByText('Attachment denied');
  fireEvent.click(screen.getByRole('button', { name: 'error.png' })); await screen.findByAltText('error.png'); expect(fakeApi.downloadSupportAttachment).toHaveBeenCalledWith(request.id, 'image-1');
});

async function openCatalog() { renderProviders(<SupportCenterView />); await screen.findByText(request.title); fireEvent.click(screen.getByRole('button', { name: 'Phần mềm' })); }
it('edits an approved installer and requires explicit publication confirmation', async () => {
  await openCatalog(); fireEvent.click(screen.getByRole('button', { name: 'Sửa phần mềm / ngưng cung cấp' }));
  change('Tên phần mềm', 'Updated Editor'); change('Ứng dụng giúp nhân viên làm gì?', 'Edit documents'); change('Phiên bản', '2.0'); change('URL HTTPS của gói MSI', 'https://packages.example/editor-v2.msi'); change('SHA-256 của gói cài', 'c'.repeat(64)); change('Thumbprint chứng thư nhà phát hành', 'd'.repeat(40)); change('Lý do công bố / sửa đổi', 'Verified publisher');
  change('MSI ProductCode (UUID, tùy chọn)', '  product-uuid  '); change('MSI UpgradeCode (UUID, tùy chọn)', ' upgrade-uuid ');
  fireEvent.click(screen.getByLabelText('Đang cung cấp')); fireEvent.click(screen.getByLabelText('Yêu cầu Admin duyệt trước khi cài'));
  expect((screen.getByRole('button', { name: 'Lưu phần mềm' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('Tôi xác minh gói cài, nhà phát hành và xác nhận công bố.')); fireEvent.click(screen.getByRole('button', { name: 'Lưu phần mềm' }));
  await waitFor(() => expect(fakeApi.saveCatalogApp).toHaveBeenCalledWith(expect.objectContaining({ name: 'Updated Editor', isActive: true, requiresApproval: false, productCode: 'product-uuid', upgradeCode: 'upgrade-uuid', reason: 'Verified publisher', confirmed: true }), catalog.id));
});
it('preserves a new catalog entry after publication fails and allows optional MSI identifiers to be cleared', async () => {
  fakeApi.saveCatalogApp.mockRejectedValueOnce(new ApiError(409)); await openCatalog(); fireEvent.click(screen.getByRole('button', { name: 'Thêm phần mềm được phép' }));
  for (const [label, value] of [['Tên phần mềm', 'New app'], ['Ứng dụng giúp nhân viên làm gì?', 'Work'], ['Phiên bản', '1'], ['URL HTTPS của gói MSI', 'https://packages.example/app.msi'], ['SHA-256 của gói cài', 'e'.repeat(64)], ['Thumbprint chứng thư nhà phát hành', 'f'.repeat(40)], ['Lý do công bố / sửa đổi', 'Verified package']]) change(label, value);
  change('MSI ProductCode (UUID, tùy chọn)', ' '); change('MSI UpgradeCode (UUID, tùy chọn)', ' ');
  fireEvent.click(screen.getByLabelText('Tôi xác minh gói cài, nhà phát hành và xác nhận công bố.')); fireEvent.click(screen.getByRole('button', { name: 'Lưu phần mềm' }));
  await screen.findByRole('alert'); fireEvent.click(screen.getByRole('button', { name: 'Lưu phần mềm' }));
  await waitFor(() => expect(fakeApi.saveCatalogApp).toHaveBeenCalledTimes(2)); expect(fakeApi.saveCatalogApp.mock.calls[1]).toEqual([expect.objectContaining({ productCode: null, upgradeCode: null, name: 'New app' }), undefined]);
});
it('publishes a confirmed outage announcement with an acknowledgement requirement after a recoverable error', async () => {
  fakeApi.createAnnouncement.mockRejectedValueOnce(new Error('Announcement unavailable')); renderProviders(<SupportCenterView />); await screen.findByText(request.title);
  fireEvent.click(screen.getByRole('button', { name: 'Thông báo công ty' })); fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo / lịch bảo trì' }));
  change('Tiêu đề', 'Network maintenance'); change('Nội dung', 'Save work before maintenance'); change('Bắt đầu', '2026-10-24T12:00'); change('Kết thúc', '2026-10-24T13:00'); change('Lý do gửi', 'Approved change');
  fireEvent.click(screen.getByLabelText('Sự cố chung — cho phép nhân viên báo bị ảnh hưởng')); fireEvent.click(screen.getByLabelText('Yêu cầu xác nhận đã đọc'));
  expect((screen.getByRole('button', { name: 'Gửi thông báo' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('Tôi xác nhận gửi thông báo trong tổ chức.')); fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' })); await screen.findByText('Announcement unavailable');
  fireEvent.click(screen.getByRole('button', { name: 'Gửi thông báo' }));
  await waitFor(() => expect(fakeApi.createAnnouncement).toHaveBeenCalledTimes(2)); expect(fakeApi.createAnnouncement.mock.calls[1][0]).toMatchObject({ title: 'Network maintenance', body: 'Save work before maintenance', isOutage: true, requiresAcknowledgement: true, reason: 'Approved change', confirmed: true });
});
