import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Auth from '../src/features/auth/auth-context';
import * as Push from '../src/features/self-service/push';
import { CatalogView, HelpView } from '../src/features/self-service/catalog-help-view';
import { NotificationsView } from '../src/features/self-service/notifications-view';
import { MobileApiClient } from '../src/lib/api/client';

const id = '10000000-0000-4000-8000-000000000001';
describe('Employee catalog, help and notification journeys', () => {
  let api: MobileApiClient;
  const push = jest.fn();
  async function mount(ui: React.ReactElement) {
    const query = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    return await render(<QueryClientProvider client={query}>{ui}</QueryClientProvider>);
  }
  beforeEach(() => {
    api = new MobileApiClient(); push.mockClear();
    jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push });
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated', user: { id }, apiClient: api } as ReturnType<typeof Auth.useAuth>);
  });
  afterEach(() => jest.restoreAllMocks());

  it('shows only active applications and sends scoped installation requests instead of installing directly', async () => {
    jest.spyOn(api, 'appCatalog').mockResolvedValue([
      { id, name: 'Office Tool', version: '1', description: 'Approved tool', isActive: true, requiresApproval: true },
      { id: 'inactive', name: 'Retired Tool', version: '1', description: '', isActive: false, requiresApproval: false },
      { id: 'active', name: 'Quick Tool', version: '2', description: '', isActive: true, requiresApproval: false },
    ] as Awaited<ReturnType<MobileApiClient['appCatalog']>>);
    await mount(<CatalogView />);
    await screen.findByText('Office Tool');
    expect(screen.queryByText('Retired Tool')).toBeNull();
    await fireEvent.press(screen.getByText('Yêu cầu cài Office Tool'));
    expect(push).toHaveBeenLastCalledWith({ pathname: '/request-create', params: expect.objectContaining({ kind: 'InstallApp', catalogAppId: id }) });
    await fireEvent.press(screen.getAllByText('Nhờ IT duyệt cài ứng dụng cần quyền cao')[0]);
    expect(push).toHaveBeenLastCalledWith({ pathname: '/request-create', params: expect.objectContaining({ kind: 'Privilege', catalogAppId: id }) });
    await fireEvent.press(screen.getByText('Không có phần mềm tôi cần — gửi nhu cầu'));
    expect(push).toHaveBeenLastCalledWith({ pathname: '/request-create', params: expect.objectContaining({ category: 'Application' }) });
  });

  it('allows retry after catalog failure and explains an empty catalog', async () => {
    const load = jest.spyOn(api, 'appCatalog').mockRejectedValueOnce(new Error('Catalog unavailable')).mockResolvedValue([]);
    await mount(<CatalogView />);
    await screen.findByText(/Catalog unavailable/);
    await fireEvent.press(screen.getByText('Thử lại'));
    await screen.findByText('IT chưa công bố ứng dụng. Bạn có thể gửi nhu cầu bên dưới.');
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('shows help steps and preserves the selected issue category when escalating to IT', async () => {
    jest.spyOn(api, 'helpArticles').mockResolvedValue([{ id, title: 'Cannot print', category: 'Printer', steps: ['Check paper', 'Check cable'] }]);
    await mount(<HelpView />);
    await screen.findByText('1. Check paper');
    expect(screen.getByText('2. Check cable')).toBeTruthy();
    await fireEvent.press(screen.getByText('Vẫn chưa được — nhờ IT'));
    expect(push).toHaveBeenLastCalledWith({ pathname: '/request-create', params: { category: 'Printer', title: 'Cannot print' } });
    await fireEvent.press(screen.getByText('Gửi yêu cầu cho IT'));
    expect(push).toHaveBeenLastCalledWith('/request-create');
  });

  it('permits retry when help could not be loaded', async () => {
    jest.spyOn(api, 'helpArticles').mockRejectedValueOnce(new Error('Help unavailable')).mockResolvedValue([]);
    await mount(<HelpView />);
    await screen.findByText(/Help unavailable/);
    await fireEvent.press(screen.getByText('Thử lại'));
    await screen.findByText('Chưa có hướng dẫn.');
  });

  it('acknowledges notifications and outages only after explicit employee actions', async () => {
    const load = jest.spyOn(api, 'employeeNotifications').mockResolvedValue([
      { id, title: 'Your request', body: 'IT replied', requestId: id, createdAt: '2026-10-23T00:00:00Z', readAt: null },
      { id: 'read', title: 'Read notice', body: 'Already read', requestId: null, createdAt: '2026-10-23T00:00:00Z', readAt: '2026-10-23T00:00:00Z' },
    ] as Awaited<ReturnType<MobileApiClient['employeeNotifications']>>);
    jest.spyOn(api, 'announcements').mockResolvedValue([
      { id, title: 'Network maintenance', body: 'Shared outage', startsAt: '2026-10-23T00:00:00Z', endsAt: '2026-10-23T01:00:00Z', requiresAcknowledgement: true, acknowledged: false, isOutage: true, affected: false },
      { id: 'done', title: 'Earlier notice', body: '', startsAt: '2026-10-22T00:00:00Z', endsAt: null, requiresAcknowledgement: true, acknowledged: true, isOutage: true, affected: true },
    ] as Awaited<ReturnType<MobileApiClient['announcements']>>);
    const read = jest.spyOn(api, 'readEmployeeNotification').mockResolvedValue();
    const acknowledge = jest.spyOn(api, 'acknowledgeAnnouncement').mockResolvedValue();
    const affected = jest.spyOn(api, 'markAnnouncementAffected').mockResolvedValue();
    await mount(<NotificationsView />);
    await screen.findByText('Your request · Chưa đọc');
    await screen.findByText('Network maintenance');
    expect(read).not.toHaveBeenCalled(); expect(acknowledge).not.toHaveBeenCalled(); expect(affected).not.toHaveBeenCalled();
    await fireEvent.press(screen.getAllByText('Tôi đã đọc')[0]);
    await waitFor(() => expect(read).toHaveBeenCalledWith(id));
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    await fireEvent.press(screen.getAllByText('Tôi đã đọc')[1]);
    await waitFor(() => expect(acknowledge).toHaveBeenCalledWith(id));
    await waitFor(() => expect(load).toHaveBeenCalledTimes(3));
    await fireEvent.press(screen.getByText('Tôi cũng bị ảnh hưởng'));
    await waitFor(() => expect(affected).toHaveBeenCalledWith(id));
    await fireEvent.press(screen.getByText('Xem yêu cầu liên quan'));
    expect(push).toHaveBeenCalledWith({ pathname: '/request-detail', params: { id } });
  });

  it('shows push registration failures, then confirms enable and disable after successful requests', async () => {
    jest.spyOn(api, 'employeeNotifications').mockResolvedValue([]);
    jest.spyOn(api, 'announcements').mockResolvedValue([]);
    const enable = jest.spyOn(Push, 'enableEmployeePush').mockRejectedValueOnce(new Error('Permission denied')).mockResolvedValue();
    const disable = jest.spyOn(Push, 'disableEmployeePush').mockResolvedValue();
    await mount(<NotificationsView />);
    await screen.findByText('Bạn chưa có thông báo mới.');
    await fireEvent.press(screen.getByText('Cho phép nhận thông báo trên điện thoại'));
    await screen.findByText(/Permission denied/);
    expect(screen.queryByText('Đã đăng ký nhận thông báo trên điện thoại này.')).toBeNull();
    await fireEvent.press(screen.getByText('Cho phép nhận thông báo trên điện thoại'));
    await screen.findByText('Đã đăng ký nhận thông báo trên điện thoại này.');
    expect(enable).toHaveBeenCalledWith(api, id);
    await fireEvent.press(screen.getByText('Tắt thông báo trên điện thoại này'));
    await screen.findByText('Đã tắt thông báo trên điện thoại này.');
    expect(disable).toHaveBeenCalledWith(api, id);
  });
});
