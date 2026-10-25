import Constants from 'expo-constants';
import { Platform } from 'react-native';

export function nativeNotifications(): typeof import('expo-notifications') | null {
  if (Constants.executionEnvironment === 'storeClient' || (Platform.OS !== 'android' && Platform.OS !== 'ios')) return null;
  // Expo Go does not include the native push modules. Load them only in the app.
  return require('expo-notifications') as typeof import('expo-notifications');
}

export async function clearNotificationResponse() {
  await nativeNotifications()?.clearLastNotificationResponseAsync();
}
