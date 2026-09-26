import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import { z } from 'zod';
import type { MobileApiClient } from '../../lib/api/client';

const PUSH_TOKEN_KEY = 'sentinellan_push_registration';
const RegistrationSchema = z.object({ token: z.string().min(1), userId: z.string().uuid() });
export function notificationRequestId(data: unknown): string | null {
  const parsed = z.object({ requestId: z.string().uuid() }).safeParse(data);
  return parsed.success ? parsed.data.requestId : null;
}
export async function enableEmployeePush(api: MobileApiClient, userId: string) {
  if (!Device.isDevice || (Platform.OS !== 'ios' && Platform.OS !== 'android')) throw new Error('Hãy bật thông báo trên điện thoại thật.');
  const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;
  if (typeof projectId !== 'string' || !projectId) throw new Error('IT cần cấu hình EAS projectId cho bản mobile trước khi bật thông báo.');
  if (Platform.OS === 'android') await Notifications.setNotificationChannelAsync('support', { name: 'Thông báo IT', importance: Notifications.AndroidImportance.HIGH });
  let permission = await Notifications.getPermissionsAsync();
  if (!permission.granted) permission = await Notifications.requestPermissionsAsync();
  if (!permission.granted) throw new Error('Bạn chưa cho phép thông báo. Vẫn có thể đọc trong hộp thông báo của app.');
  const token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
  const previous = await storedPushRegistration();
  if (previous && previous.token !== token && previous.userId === userId) await api.deregisterPushDevice(previous.token);
  await api.registerPushDevice(token, Platform.OS);
  await SecureStore.setItemAsync(PUSH_TOKEN_KEY, JSON.stringify({ token, userId }));
}
async function storedPushRegistration() {
  try { const text = await SecureStore.getItemAsync(PUSH_TOKEN_KEY); if (!text) return null; const parsed = RegistrationSchema.safeParse(JSON.parse(text)); return parsed.success ? parsed.data : null; } catch { return null; }
}
export async function refreshEmployeePush(api: MobileApiClient, userId: string) {
  const previous = await storedPushRegistration();
  if (!previous || previous.userId !== userId) return;
  const permission = await Notifications.getPermissionsAsync();
  if (!permission.granted) { await disableEmployeePush(api, userId); return; }
  await enableEmployeePush(api, userId);
}
export async function disableEmployeePush(api: MobileApiClient, userId: string) {
  const previous = await storedPushRegistration();
  if (previous?.userId === userId) await api.deregisterPushDevice(previous.token);
  await SecureStore.deleteItemAsync(PUSH_TOKEN_KEY);
}
export async function forgetEmployeePush() { await SecureStore.deleteItemAsync(PUSH_TOKEN_KEY); }
