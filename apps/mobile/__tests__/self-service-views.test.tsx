import React from 'react';
import { Alert } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, fireEvent, waitFor } from '@testing-library/react-native';
import { useLocalSearchParams } from 'expo-router';
import { RequestCreateView } from '../src/features/self-service/request-create-view';
import { SelfServiceHomeView } from '../src/features/self-service/home-view';
import { RequestDetailView } from '../src/features/self-service/request-detail-view';
import { ImageAttachmentPicker } from '../src/features/self-service/image-attachment';
import { MobileApiClient } from '../src/lib/api/client';
import * as Auth from '../src/features/auth/auth-context';

const id = '00000000-0000-4000-8000-000000000001';
const request = { id, deviceId: id, deviceName: 'PC An', userId: id, userName: 'An', kind: 'PauseAgent' as const, category: 'Other' as const, title: 'Xin tạm dừng SentinelLAN', description: '', canWork: true, status: 'Approved' as const, assignedTechnicianId: null, assignedTechnicianName: null, catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: null, createdAt: '2026-09-26T00:00:00Z', updatedAt: '2026-09-26T00:00:00Z', approvalExpiresAt: '2026-09-26T00:05:00Z' };
function wrap(ui: React.ReactElement) { const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>); }
describe('Employee self-service screens', () => {
  let api: MobileApiClient;
  beforeEach(() => {
    api = new MobileApiClient('https://sentinellan.local');
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated', user: { id, email: 'an@example.test', role: 'Employee', displayName: 'An', organizationId: id, organizationCode: 'company' }, apiClient: api, login: jest.fn(), logout: jest.fn(), logoutAll: jest.fn(), retrySession: jest.fn(), dismissSessionExpired: jest.fn() });
    (useLocalSearchParams as jest.Mock).mockReturnValue({});
  });
  afterEach(() => jest.restoreAllMocks());
  it('reports an ordinary office problem without asking the employee to rank severity', async () => {
    const create = jest.spyOn(api, 'createSupportRequest').mockResolvedValue({ ...request, kind: 'Incident', status: 'Open' });
    wrap(<RequestCreateView />);
    fireEvent.press(screen.getByText('Không in được'));
    fireEvent(screen.getByLabelText('Tôi vẫn tiếp tục làm việc được'), 'valueChange', false);
    fireEvent.press(screen.getByTestId('submit-incident-button'));
    await waitFor(() => expect(create).toHaveBeenCalledWith(expect.objectContaining({ category: 'Printer', title: 'Không in được', canWork: false, confirmed: true })));
    expect(screen.queryByText('Critical')).toBeNull();
  });
  it('shows an API failure and allows retry without pretending a request was received', async () => {
    const create = jest.spyOn(api, 'createSupportRequest').mockRejectedValue(new Error('Không có máy được phân công'));
    wrap(<RequestCreateView />);
    fireEvent.press(screen.getByTestId('submit-incident-button'));
    await waitFor(() => expect(screen.getByText(/Không có máy được phân công/)).toBeTruthy());
    fireEvent.press(screen.getByTestId('submit-incident-button'));
    await waitFor(() => expect(create).toHaveBeenCalledTimes(2));
    expect(create.mock.calls[0][0].idempotencyKey).toBe(create.mock.calls[1][0].idempotencyKey);
  });
  it('does not send a panic command before explicit confirmation', async () => {
    jest.spyOn(api, 'myDevice').mockResolvedValue({ device: { id, name: 'PC An', isOnline: true } } as Awaited<ReturnType<MobileApiClient['myDevice']>>);
    const create = jest.spyOn(api, 'createSupportRequest').mockResolvedValue({ ...request, kind: 'Panic' });
    const alert = jest.spyOn(Alert, 'alert');
    wrap(<SelfServiceHomeView />);
    await waitFor(() => expect(screen.getByText('PC An · Đang kết nối')).toBeTruthy());
    fireEvent.press(screen.getByText('Tôi nghi máy bị nhiễm virus'));
    expect(create).not.toHaveBeenCalled();
    const buttons = alert.mock.calls[0][2];
    await act(async () => { buttons?.find(button => button.text === 'Xác nhận báo khẩn cấp')?.onPress?.(); });
    await waitFor(() => expect(create).toHaveBeenCalledWith(expect.objectContaining({ kind: 'Panic', confirmed: true })));
  });
  it('requires a second confirmation before redeeming a maintenance OTP', async () => {
    (useLocalSearchParams as jest.Mock).mockReturnValue({ id });
    jest.spyOn(api, 'supportRequest').mockResolvedValue(request);
    jest.spyOn(api, 'supportMessages').mockResolvedValue([]);
    jest.spyOn(api, 'supportAttachments').mockResolvedValue([]);
    const redeem = jest.spyOn(api, 'redeemMaintenance').mockResolvedValue({ ...request, commandId: id });
    const alert = jest.spyOn(Alert, 'alert');
    wrap(<RequestDetailView />);
    await waitFor(() => expect(screen.getByLabelText('Mã 8 chữ số')).toBeTruthy());
    fireEvent.changeText(screen.getByLabelText('Mã 8 chữ số'), '12345678');
    fireEvent.press(screen.getByText('Xác nhận thao tác với mã IT'));
    expect(redeem).not.toHaveBeenCalled();
    await act(async () => { alert.mock.calls[0][2]?.find(button => button.text === 'Xác nhận')?.onPress?.(); });
    await waitFor(() => expect(redeem).toHaveBeenCalledWith(id, '12345678', true));
  });
  it('previews selected images and asks consent without uploading automatically', () => {
    const onConsent = jest.fn();
    const onChange = jest.fn();
    wrap(<ImageAttachmentPicker value={{ uri: 'file:///error.jpg', fileName: 'error.jpg', contentType: 'image/jpeg', base64: '/9j/AAAA' }} onChange={onChange} consent={false} onConsent={onConsent} />);
    expect(screen.getByLabelText('Ảnh lỗi bạn chuẩn bị gửi cho IT')).toBeTruthy();
    fireEvent(screen.getByLabelText('Tôi đã xem ảnh và đồng ý gửi cho IT'), 'valueChange', true);
    expect(onConsent).toHaveBeenCalledWith(true);
    fireEvent.press(screen.getByText('Bỏ ảnh'));
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
