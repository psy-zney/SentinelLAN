import React, { useEffect } from 'react';
import { useRouter } from 'expo-router';
import { AppState } from 'react-native';
import * as Notifications from 'expo-notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/auth-context';
import { notificationRequestId, refreshEmployeePush } from './push';

Notifications.setNotificationHandler({ handleNotification: async () => ({ shouldPlaySound: true, shouldSetBadge: false, shouldShowBanner: true, shouldShowList: true }) });
export function EmployeePushObserver() {
  const { status, user, apiClient } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();
  useEffect(() => {
    if (status !== 'authenticated' || !user) return;
    let mounted = true;
    let handled: string | null = null;
    const redirect = (response: Notifications.NotificationResponse) => {
      if (!mounted || response.notification.request.identifier === handled) return;
      handled = response.notification.request.identifier;
      const id = notificationRequestId(response.notification.request.content.data);
      // No arbitrary URLs from push data; the API rechecks request ownership.
      router.push(id ? { pathname: '/request-detail', params: { id } } : '/employee-notifications');
      void Notifications.clearLastNotificationResponseAsync();
    };
    void Notifications.getLastNotificationResponseAsync().then(response => { if (response) redirect(response); }).catch(() => {});
    const received = Notifications.addNotificationReceivedListener(() => { void queryClient.invalidateQueries({ queryKey: ['employee-notifications'] }); void queryClient.invalidateQueries({ queryKey: ['support-requests'] }); });
    const responses = Notifications.addNotificationResponseReceivedListener(redirect);
    const refresh = () => { void refreshEmployeePush(apiClient, user.id).catch(() => {}); };
    refresh();
    const foreground = AppState.addEventListener('change', state => { if (state === 'active') refresh(); });
    return () => { mounted = false; received.remove(); responses.remove(); foreground.remove(); };
  }, [status, user, apiClient, router, queryClient]);
  return null;
}
