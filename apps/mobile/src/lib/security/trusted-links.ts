import Constants from 'expo-constants';

export function isTrustedWebUrl(url: URL): boolean {
  if (url.username || url.password) return false;
  if (url.protocol !== 'https:' && !(__DEV__ && url.protocol === 'http:')) return false;

  const configured = [Constants.expoConfig?.extra?.apiUrl, Constants.expoConfig?.extra?.webUrl];
  return configured.some((value) => {
    if (typeof value !== 'string') return false;
    try {
      const allowed = new URL(value);
      return allowed.origin === url.origin && allowed.protocol === url.protocol;
    } catch {
      return false;
    }
  });
}
