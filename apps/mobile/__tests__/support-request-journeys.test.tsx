import React from 'react';
import { Alert } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Auth from '../src/features/auth/auth-context';
import { MobileApiClient } from '../src/lib/api/client';
import { RequestListView } from '../src/features/self-service/request-list-view';
import { RequestDetailView } from '../src/features/self-service/request-detail-view';
import { SelfServiceHomeView } from '../src/features/self-service/home-view';
import { ImageAttachmentPicker } from '../src/features/self-service/image-attachment';
import * as ImagePicker from 'expo-image-picker';
import type { SupportRequest } from '../src/lib/validation/self-service';

const id = '10000000-0000-4000-8000-000000000001';
const request: SupportRequest = { id, deviceId: id, deviceName: 'Office PC', userId: id, userName: 'Employee', kind: 'Incident', category: 'Printer', title: 'Printer unavailable', description: 'Cannot work', canWork: false, status: 'Resolved', assignedTechnicianId: id, assignedTechnicianName: 'Admin IT', catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: '2026-10-23T02:00:00Z', createdAt: '2026-10-23T00:00:00Z', updatedAt: '2026-10-23T01:00:00Z', approvalExpiresAt: null };
describe('Support request confirmation and recovery', () => {
  let api: MobileApiClient;
  let role: 'Admin' | 'Employee';
  const push = jest.fn();
  async function mount(ui: React.ReactElement) {
    return await render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}>{ui}</QueryClientProvider>);
  }
  async function confirmLast(alert: jest.SpyInstance) {
    const buttons = alert.mock.calls[alert.mock.calls.length - 1][2];
    await act(async () => { buttons?.find((button: { text: string }) => button.text === 'Xác nhận')?.onPress?.(); });
  }
  beforeEach(() => {
    role = 'Employee'; api = new MobileApiClient(); push.mockClear();
    jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push });
    jest.spyOn(Auth, 'useAuth').mockImplementation(() => ({ status: 'authenticated', user: { id, role, displayName: 'Employee' }, apiClient: api } as ReturnType<typeof Auth.useAuth>));
    jest.mocked(useLocalSearchParams).mockReturnValue({ id });
    jest.spyOn(api, 'supportRequest').mockResolvedValue(request);
    jest.spyOn(api, 'supportMessages').mockResolvedValue([{ id, requestId: id, authorId: id, authorName: 'IT', body: 'Try printing again', createdAt: request.createdAt }]);
    jest.spyOn(api, 'supportAttachments').mockResolvedValue([{ id, requestId: id, fileName: 'error.png', contentType: 'image/png', size: 1500, createdAt: request.createdAt }]);
    jest.spyOn(api, 'supportRequests').mockResolvedValue([]);
  });
  afterEach(() => jest.restoreAllMocks());
  it('pages history and opens only the selected request identifier', async () => {
    const load = jest.spyOn(api, 'supportRequests').mockImplementation(async (offset) => offset ? [] : Array.from({ length: 100 }, (_, index) => ({ ...request, id: String(index), title: `Request ${index}`, assignedTechnicianName: index ? null : 'Admin IT' })));
    await mount(<RequestListView />); await screen.findByText('Request 0');
    await fireEvent.press(screen.getAllByText('Xem tiến độ và nhắn IT')[0]);
    expect(push).toHaveBeenLastCalledWith({ pathname: '/request-detail', params: { id: '0' } });
    await fireEvent.press(screen.getByText('Gửi yêu cầu mới'));
    expect(push).toHaveBeenLastCalledWith('/request-create');
    await fireEvent.press(screen.getByText('Xem báo cáo sự cố trước đây'));
    expect(push).toHaveBeenLastCalledWith('/incident-history');
    await fireEvent.press(screen.getByText('Trang sau')); await screen.findByText('Bạn chưa có yêu cầu nào.');
    expect(load).toHaveBeenCalledWith(100);
    await fireEvent.press(screen.getByText('Trang trước')); await screen.findByText('Request 0');
  });
  it('retries an Admin support list without offering employee create actions', async () => {
    role = 'Admin';
    jest.spyOn(api, 'supportRequests').mockRejectedValueOnce(new Error('Queue unavailable')).mockResolvedValue([request]);
    await mount(<RequestListView />); await screen.findByText(/Queue unavailable/);
    expect(screen.queryByText('Gửi yêu cầu mới')).toBeNull();
    await fireEvent.press(screen.getByText('Thử lại')); await screen.findByText('Printer unavailable');
    await fireEvent.press(screen.getByText('Xử lý và trả lời'));
    expect(push).toHaveBeenCalledWith({ pathname: '/request-detail', params: { id } });
  });
  it.each([['Đã dùng được', 'Closed'], ['Vẫn còn lỗi — nhờ IT kiểm tra lại', 'Open']])('confirms employee action %s before changing status', async (title, status) => {
    const update = jest.spyOn(api, 'updateOwnSupportRequest').mockResolvedValue(request);
    const alert = jest.spyOn(Alert, 'alert');
    await mount(<RequestDetailView />); await screen.findByText('Printer unavailable');
    expect(screen.getByText('error.png · 2 KiB')).toBeTruthy();
    await fireEvent.press(screen.getByText(title)); expect(update).not.toHaveBeenCalled();
    await confirmLast(alert);
    await waitFor(() => expect(update).toHaveBeenCalledWith(id, status, expect.any(String), true));
  });
  it('reuses message idempotency after failure and clears text only after receipt', async () => {
    const send = jest.spyOn(api, 'sendSupportMessage').mockRejectedValueOnce(new Error('Message not received')).mockResolvedValue({ id, requestId: id, authorId: id, authorName: 'Employee', body: 'Please help', createdAt: request.createdAt });
    await mount(<RequestDetailView />); await screen.findByText('Printer unavailable');
    await fireEvent.changeText(screen.getByLabelText('Tin nhắn cho IT'), ' Please help ');
    await fireEvent.press(screen.getByText('Gửi tin nhắn')); await screen.findByText(/Message not received/);
    expect(screen.getByLabelText('Tin nhắn cho IT').props.value).toBe(' Please help ');
    await fireEvent.press(screen.getByText('Gửi tin nhắn'));
    await waitFor(() => expect(send).toHaveBeenCalledTimes(2)); expect(send.mock.calls[0]).toEqual(send.mock.calls[1]);
    await waitFor(() => expect(screen.getByLabelText('Tin nhắn cho IT').props.value).toBe(''));
  });
  it.each([['Nhận xử lý', 'InProgress'], ['Chờ nhân viên kiểm tra', 'AwaitingEmployee'], ['Đã xử lý', 'Resolved']])('confirms Admin transition %s before issuing it', async (title, status) => {
    role = 'Admin'; const update = jest.spyOn(api, 'updateOperatorRequest').mockResolvedValue(request);
    const alert = jest.spyOn(Alert, 'alert');
    await mount(<RequestDetailView />); await screen.findByText('Xử lý yêu cầu');
    await fireEvent.changeText(screen.getByLabelText('Lý do hoặc kết quả xử lý'), 'Authorized maintenance');
    await fireEvent.press(screen.getByText(title)); expect(update).not.toHaveBeenCalled(); await confirmLast(alert);
    await waitFor(() => expect(update).toHaveBeenCalledWith(id, status, id, 'Authorized maintenance'));
  });
  it.each([true, false])('confirms approval %s and hides OTP on dismissal', async (approved) => {
    role = 'Admin'; jest.mocked(api.supportRequest).mockResolvedValue({ ...request, kind: 'PauseAgent', status: 'Open' });
    const decide = jest.spyOn(api, 'decideOperatorRequest').mockResolvedValue({ request, otp: approved ? '12345678' : null, expiresAt: '2026-10-23T00:05:00Z' });
    const alert = jest.spyOn(Alert, 'alert'); await mount(<RequestDetailView />); await screen.findByText('Xử lý yêu cầu');
    await fireEvent.changeText(screen.getByLabelText('Lý do hoặc kết quả xử lý'), 'Authorized maintenance');
    await fireEvent.press(screen.getByText(approved ? 'Phê duyệt' : 'Từ chối')); expect(decide).not.toHaveBeenCalled(); await confirmLast(alert);
    await waitFor(() => expect(decide).toHaveBeenCalledWith(id, approved, 'Authorized maintenance'));
    if (approved) { await screen.findByText(/Mã dùng một lần, gửi riêng cho nhân viên: 12345678/); await fireEvent.press(screen.getByText('Đã gửi — đóng mã')); }
    expect(screen.queryByText(/Mã dùng một lần, gửi riêng cho nhân viên:/)).toBeNull();
  });
  it('does not query an invalid navigation identifier', async () => {
    jest.mocked(useLocalSearchParams).mockReturnValue({ id: '../other-tenant' }); await mount(<RequestDetailView />);
    expect(screen.getByText('Không tìm thấy yêu cầu hợp lệ.')).toBeTruthy(); expect(api.supportRequest).not.toHaveBeenCalled();
  });
  it('routes home tasks with their intended context without triggering device actions', async () => {
    jest.spyOn(api, 'myDevice').mockResolvedValue(null); await mount(<SelfServiceHomeView />);
    await screen.findByText('Bạn chưa được phân công máy. Liên hệ IT hoặc quét mã QR trên máy.');
    for (const title of ['Tôi gặp sự cố', 'Tôi cần phần mềm', 'Yêu cầu của tôi', 'Xem máy hoặc quét mã QR', 'Thông báo và sự cố chung', 'Hướng dẫn xử lý lỗi thường gặp', 'Chọn giờ IT hỗ trợ', 'Yêu cầu tạm dừng SentinelLAN', 'Yêu cầu gỡ SentinelLAN']) await fireEvent.press(screen.getByText(title));
    for (const path of ['/request-create', '/app-catalog', '/support-requests', '/(tabs)/scan', '/employee-notifications', '/self-service-help']) expect(push).toHaveBeenCalledWith(path);
    for (const kind of ['Appointment', 'PauseAgent', 'UninstallAgent']) expect(push).toHaveBeenCalledWith({ pathname: '/request-create', params: { kind } });
  });
  it('previews a selected image only after permission and reports denied camera access', async () => {
    const onChange = jest.fn(), onConsent = jest.fn();
    jest.mocked(ImagePicker.requestCameraPermissionsAsync).mockResolvedValueOnce({ granted: false } as ImagePicker.CameraPermissionResponse).mockResolvedValueOnce({ granted: true } as ImagePicker.CameraPermissionResponse);
    jest.mocked(ImagePicker.launchCameraAsync).mockResolvedValue({ canceled: false, assets: [{ uri: 'file:///error.jpg', base64: '/9j/AAAA', width: 100, height: 100 }] });
    await mount(<ImageAttachmentPicker value={null} onChange={onChange} consent={false} onConsent={onConsent} />);
    await fireEvent.press(screen.getByText('Chụp ảnh màn hình máy tính')); await screen.findByText(/Bạn chưa cho phép chụp ảnh/); expect(onChange).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByText('Chụp ảnh màn hình máy tính'));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ contentType: 'image/jpeg', uri: 'file:///error.jpg' })));
    expect(onConsent).toHaveBeenCalledWith(false); expect(ImagePicker.launchCameraAsync).toHaveBeenCalledWith(expect.objectContaining({ exif: false, base64: true }));
  });
});
