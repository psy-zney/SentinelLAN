import Constants from 'expo-constants';
import { AuthProvider } from '../src/features/auth/auth-context';
import { EmployeePushObserver } from '../src/features/self-service/push-observer';
import { clearNotificationResponse, nativeNotifications } from '../src/features/self-service/notification-runtime';
import { enableEmployeePush, refreshEmployeePush } from '../src/features/self-service/push';
import { MobileApiClient } from '../src/lib/api/client';

jest.mock('expo-constants', () => ({ executionEnvironment: 'storeClient', expoConfig: { extra: {} } }));
jest.mock('expo-notifications', () => { throw new Error('Native push module is unavailable in Expo Go'); });

it('loads authentication and notification navigation in Expo Go without importing native push modules', async () => {
  expect(Constants.executionEnvironment).toBe('storeClient');
  expect(typeof AuthProvider).toBe('function');
  expect(typeof EmployeePushObserver).toBe('function');
  expect(nativeNotifications()).toBeNull();
  await expect(clearNotificationResponse()).resolves.toBeUndefined();
  const api = new MobileApiClient();
  await expect(enableEmployeePush(api, '10000000-0000-4000-8000-000000000001')).rejects.toThrow('bản SentinelLAN do IT cung cấp');
  await expect(refreshEmployeePush(api, '10000000-0000-4000-8000-000000000001')).resolves.toBeUndefined();
});
