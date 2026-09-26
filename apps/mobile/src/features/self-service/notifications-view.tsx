import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppButton, AppCard } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';
import { disableEmployeePush, enableEmployeePush } from './push';

export function NotificationsView() {
  const { apiClient, user } = useAuth();
  const active = useActiveScreen();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [error, setError] = useState<unknown>();
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const notifications = useQuery({ queryKey: ['employee-notifications'], queryFn: () => apiClient.employeeNotifications(), enabled: active, refetchInterval: active ? 15000 : false });
  const announcements = useQuery({ queryKey: ['employee-announcements'], queryFn: () => apiClient.announcements(), enabled: active, refetchInterval: active ? 15000 : false });
  const perform = async (action: () => Promise<unknown>) => { if (busy) return; setBusy(true); setError(undefined); setMessage(''); try { await action(); await Promise.all([queryClient.invalidateQueries({ queryKey: ['employee-notifications'] }), queryClient.invalidateQueries({ queryKey: ['employee-announcements'] })]); } catch (err) { setError(err); } finally { setBusy(false); } };
  return <ServicePage>
    <ServiceText heading>Thông báo của bạn</ServiceText>
    <ServiceText>Bật thông báo để nhận tin IT trên điện thoại. Nội dung chi tiết luôn nằm trong app sau khi đăng nhập.</ServiceText>
    <AppButton title="Cho phép nhận thông báo trên điện thoại" loading={busy} onPress={() => { void perform(async () => { if (user) { await enableEmployeePush(apiClient, user.id); setMessage('Đã đăng ký nhận thông báo trên điện thoại này.'); } }); }} />
    <AppButton title="Tắt thông báo trên điện thoại này" variant="outline" disabled={busy} onPress={() => { void perform(async () => { if (user) { await disableEmployeePush(apiClient, user.id); setMessage('Đã tắt thông báo trên điện thoại này.'); } }); }} />
    {message ? <ServiceText>{message}</ServiceText> : null}<ServiceError error={error} />
    <ServiceError error={notifications.error} retry={() => { void notifications.refetch(); }} />
    {notifications.isPending ? <ServiceText>Đang tải…</ServiceText> : notifications.data?.length === 0 ? <ServiceText>Bạn chưa có thông báo mới.</ServiceText> : null}
    {notifications.data?.map(notification => <AppCard key={notification.id}>
      <ServiceText heading>{notification.title}{notification.readAt ? '' : ' · Chưa đọc'}</ServiceText><ServiceText>{notification.body}</ServiceText>
      <ServiceText>{new Date(notification.createdAt).toLocaleString('vi-VN')}</ServiceText>
      {!notification.readAt ? <AppButton title="Tôi đã đọc" variant="outline" disabled={busy} onPress={() => { void perform(() => apiClient.readEmployeeNotification(notification.id)); }} /> : null}
      {notification.requestId ? <AppButton title="Xem yêu cầu liên quan" onPress={() => router.push({ pathname: '/request-detail', params: { id: notification.requestId! } })} /> : null}
    </AppCard>)}
    <ServiceText heading>Thông báo công ty và sự cố chung</ServiceText>
    <ServiceError error={announcements.error} retry={() => { void announcements.refetch(); }} />
    {announcements.data?.map(announcement => <AppCard key={announcement.id}>
      <ServiceText heading>{announcement.title}</ServiceText><ServiceText>{announcement.body}</ServiceText>
      <ServiceText>Từ {new Date(announcement.startsAt).toLocaleString('vi-VN')}{announcement.endsAt ? ` đến ${new Date(announcement.endsAt).toLocaleString('vi-VN')}` : ''}</ServiceText>
      {announcement.requiresAcknowledgement ? <AppButton title={announcement.acknowledged ? 'Bạn đã xác nhận đọc' : 'Tôi đã đọc'} variant="outline" disabled={busy || announcement.acknowledged} onPress={() => { void perform(() => apiClient.acknowledgeAnnouncement(announcement.id)); }} /> : null}
      {announcement.isOutage ? <AppButton title={announcement.affected ? 'IT đã biết bạn bị ảnh hưởng' : 'Tôi cũng bị ảnh hưởng'} disabled={busy || announcement.affected} onPress={() => { void perform(() => apiClient.markAnnouncementAffected(announcement.id)); }} /> : null}
    </AppCard>)}
  </ServicePage>;
}
