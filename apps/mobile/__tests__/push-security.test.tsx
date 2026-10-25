import React from 'react';
import Constants from 'expo-constants';
import * as Notifications from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';
import { AppState, Platform } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, waitFor } from '@testing-library/react-native';
import { enableEmployeePush, disableEmployeePush, refreshEmployeePush, notificationRequestId, forgetEmployeePush } from '../src/features/self-service/push';
import { EmployeePushObserver } from '../src/features/self-service/push-observer';
import * as Auth from '../src/features/auth/auth-context';
import { MobileApiClient } from '../src/lib/api/client';

const userId = '10000000-0000-4000-8000-000000000001';
const otherId = '10000000-0000-4000-8000-000000000002';
const key = 'sentinellan_push_registration';
const stored = (token: string, id = userId) => SecureStore.setItemAsync(key, JSON.stringify({ token, userId: id }));

describe('Push registration and navigation boundaries', () => {
  let api: MobileApiClient;
  beforeEach(async () => {
    jest.clearAllMocks();
    await SecureStore.deleteItemAsync(key);
    api = new MobileApiClient();
    jest.spyOn(api, 'registerPushDevice').mockResolvedValue();
    jest.spyOn(api, 'deregisterPushDevice').mockResolvedValue();
    Object.defineProperty(Platform, 'OS', { configurable: true, value: 'android' });
    Object.defineProperty(require('expo-device'), 'isDevice', { configurable: true, value: true });
    Constants.expoConfig!.extra = { apiUrl: 'https://sentinellan.local', eas: { projectId: 'test-project' } };
    jest.mocked(Notifications.getPermissionsAsync).mockResolvedValue({ granted: true } as Notifications.NotificationPermissionsStatus);
    jest.mocked(Notifications.requestPermissionsAsync).mockResolvedValue({ granted: true } as Notifications.NotificationPermissionsStatus);
    jest.mocked(Notifications.getExpoPushTokenAsync).mockResolvedValue({ data: 'new-token', type: 'expo' });
    jest.mocked(Notifications.getLastNotificationResponseAsync).mockResolvedValue(null);
  });
  afterEach(async () => {
    jest.restoreAllMocks();
    await SecureStore.deleteItemAsync(key);
    Constants.expoConfig!.extra = { apiUrl: 'https://sentinellan.local' };
  });

  it('validates request identifiers instead of opening arbitrary push URLs', () => {
    expect(notificationRequestId({ requestId: userId, url: 'https://evil.test' })).toBe(userId);
    for (const input of [null, {}, { requestId: 'https://evil.test' }, { url: 'sentinellan://account' }]) expect(notificationRequestId(input)).toBeNull();
  });
  it('registers only after permission and removes an old token belonging to the same user', async () => {
    await stored('old-token');
    jest.mocked(Notifications.getPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);
    await enableEmployeePush(api, userId);
    expect(Notifications.requestPermissionsAsync).toHaveBeenCalledTimes(1);
    expect(api.deregisterPushDevice).toHaveBeenCalledWith('old-token');
    expect(api.registerPushDevice).toHaveBeenCalledWith('new-token', 'android');
    expect(JSON.parse((await SecureStore.getItemAsync(key))!)).toEqual({ token: 'new-token', userId });
  });
  it('never deregisters a token belonging to another account while registering this account', async () => {
    await stored('foreign-token', otherId);
    Object.defineProperty(Platform, 'OS', { configurable: true, value: 'ios' });
    await enableEmployeePush(api, userId);
    expect(api.deregisterPushDevice).not.toHaveBeenCalled();
    expect(api.registerPushDevice).toHaveBeenCalledWith('new-token', 'ios');
    expect(Notifications.setNotificationChannelAsync).not.toHaveBeenCalled();
  });
  it.each(['emulator', 'web', 'project', 'permission'])('does not register when prerequisite %s fails', async (reason) => {
    if (reason === 'emulator') Object.defineProperty(require('expo-device'), 'isDevice', { configurable: true, value: false });
    if (reason === 'web') Object.defineProperty(Platform, 'OS', { configurable: true, value: 'web' });
    if (reason === 'project') Constants.expoConfig!.extra = {};
    if (reason === 'permission') {
      jest.mocked(Notifications.getPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);
      jest.mocked(Notifications.requestPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);
    }
    await expect(enableEmployeePush(api, userId)).rejects.toThrow();
    expect(api.registerPushDevice).not.toHaveBeenCalled();
    expect(await SecureStore.getItemAsync(key)).toBeNull();
  });
  it('does not store a registration that the server rejected', async () => {
    jest.mocked(api.registerPushDevice).mockRejectedValue(new Error('server unavailable'));
    await expect(enableEmployeePush(api, userId)).rejects.toThrow('server unavailable');
    expect(await SecureStore.getItemAsync(key)).toBeNull();
  });
  it('disables a revoked permission and removes the persisted registration', async () => {
    await stored('old-token');
    jest.mocked(Notifications.getPermissionsAsync).mockResolvedValue({ granted: false } as Notifications.NotificationPermissionsStatus);
    await refreshEmployeePush(api, userId);
    expect(api.deregisterPushDevice).toHaveBeenCalledWith('old-token');
    expect(await SecureStore.getItemAsync(key)).toBeNull();
  });
  it('refreshes the token for the same signed-in user only', async () => {
    await stored('new-token');
    await refreshEmployeePush(api, userId);
    expect(api.registerPushDevice).toHaveBeenCalledWith('new-token', 'android');
    expect(api.deregisterPushDevice).not.toHaveBeenCalled();
    jest.mocked(api.registerPushDevice).mockClear();
    await refreshEmployeePush(api, otherId);
    expect(api.registerPushDevice).not.toHaveBeenCalled();
  });
  it.each(['{invalid', '{}', ''])('ignores a corrupt stored registration %s', async (text) => {
    await SecureStore.setItemAsync(key, text);
    await refreshEmployeePush(api, userId);
    expect(api.registerPushDevice).not.toHaveBeenCalled();
    await disableEmployeePush(api, userId);
    expect(api.deregisterPushDevice).not.toHaveBeenCalled();
  });
  it('forgets a registration without sending a privileged server operation', async () => {
    await stored('old-token');
    await forgetEmployeePush();
    expect(await SecureStore.getItemAsync(key)).toBeNull();
    expect(api.deregisterPushDevice).not.toHaveBeenCalled();
  });

  it('deduplicates notification opens, refreshes lists and removes listeners on logout/unmount', async () => {
    const push = jest.fn();
    const receivedRemove = jest.fn(), responseRemove = jest.fn(), foregroundRemove = jest.fn();
    jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push });
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated', user: { id: userId }, apiClient: api } as ReturnType<typeof Auth.useAuth>);
    jest.spyOn(AppState, 'addEventListener').mockReturnValue({ remove: foregroundRemove });
    jest.mocked(Notifications.addNotificationReceivedListener).mockReturnValue({ remove: receivedRemove });
    jest.mocked(Notifications.addNotificationResponseReceivedListener).mockReturnValue({ remove: responseRemove });
    const response = (identifier: string, data: unknown) => ({ notification: { request: { identifier, content: { data } } } } as Notifications.NotificationResponse);
    jest.mocked(Notifications.getLastNotificationResponseAsync).mockResolvedValue(response('first', { requestId: userId }));
    const query = new QueryClient();
    const invalidate = jest.spyOn(query, 'invalidateQueries');
    const view = await render(<QueryClientProvider client={query}><EmployeePushObserver /></QueryClientProvider>);
    await waitFor(() => expect(push).toHaveBeenCalledWith({ pathname: '/request-detail', params: { id: userId } }));
    const onResponse = jest.mocked(Notifications.addNotificationResponseReceivedListener).mock.calls[0][0];
    const onReceived = jest.mocked(Notifications.addNotificationReceivedListener).mock.calls[0][0];
    await act(() => { onResponse(response('first', { requestId: userId })); onReceived({} as Notifications.Notification); });
    expect(push).toHaveBeenCalledTimes(1);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['support-requests'] });
    await act(() => onResponse(response('second', { url: 'https://evil.test' })));
    expect(push).toHaveBeenLastCalledWith('/employee-notifications');
    await view.unmount();
    expect(receivedRemove).toHaveBeenCalledTimes(1);
    expect(responseRemove).toHaveBeenCalledTimes(1);
    expect(foregroundRemove).toHaveBeenCalledTimes(1);
    await act(() => onResponse(response('third', { requestId: userId })));
    expect(push).toHaveBeenCalledTimes(2);
  });
  it('does not subscribe to notification navigation before authentication', async () => {
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'unauthenticated', user: null, apiClient: api } as ReturnType<typeof Auth.useAuth>);
    await render(<QueryClientProvider client={new QueryClient()}><EmployeePushObserver /></QueryClientProvider>);
    expect(Notifications.addNotificationResponseReceivedListener).not.toHaveBeenCalled();
  });
});
