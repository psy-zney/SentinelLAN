import { ExpoConfig, ConfigContext } from 'expo/config';

const configuredApiUrl = process.env.EXPO_PUBLIC_API_URL;
const configuredWebUrl = process.env.EXPO_PUBLIC_WEB_URL;
const webUrl = configuredWebUrl ?? configuredApiUrl;
const webOrigin = (() => {
  try { return webUrl ? new URL(webUrl) : null; } catch { return null; }
})();
const universalLinkHost = webOrigin?.protocol === 'https:' && !webOrigin.port &&
  webOrigin.hostname !== 'localhost' && !webOrigin.hostname.endsWith('.local')
  ? webOrigin.hostname
  : null;
if (process.env.EAS_BUILD_PROFILE === 'preview' || process.env.EAS_BUILD_PROFILE === 'production') {
  for (const [name, value] of [['EXPO_PUBLIC_API_URL', configuredApiUrl], ['EXPO_PUBLIC_WEB_URL', webUrl]]) {
    try {
      const url = new URL(value ?? '');
      if (url.protocol !== 'https:' || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
        throw new Error('invalid origin');
      }
    } catch {
      throw new Error(`${name} must be an HTTPS origin for release builds`);
    }
  }
  if (!universalLinkHost) {
    throw new Error('EXPO_PUBLIC_WEB_URL must be an HTTPS origin on port 443 for universal links');
  }
}

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: 'SentinelLAN Employee',
  slug: 'sentinellan-employee',
  version: '1.0.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  scheme: 'sentinellan',
  userInterfaceStyle: 'automatic',
  platforms: ['ios', 'android'],
  ios: {
    supportsTablet: false,
    bundleIdentifier: 'com.sentinellan.employee',
    infoPlist: {
      NSCameraUsageDescription:
        'SentinelLAN uses the camera exclusively to scan physical QR asset labels on authorized computers.',
      NSPhotoLibraryUsageDescription:
        'SentinelLAN allows selecting an image from your photo library containing a QR code.',
    },
    associatedDomains: universalLinkHost ? [`applinks:${universalLinkHost}`] : [],
  },
  android: {
    package: 'com.sentinellan.employee',
    adaptiveIcon: {
      foregroundImage: './assets/adaptive-icon.png',
      backgroundColor: '#0b6b5f',
    },
    permissions: ['android.permission.CAMERA'],
    intentFilters: [
      {
        action: 'VIEW',
        data: [{ scheme: 'sentinellan' }],
        category: ['BROWSABLE', 'DEFAULT'],
      },
      ...(universalLinkHost ? [{
        action: 'VIEW' as const,
        autoVerify: true,
        data: [
          { scheme: 'https', host: universalLinkHost, pathPrefix: '/activate' },
        ],
        category: ['BROWSABLE', 'DEFAULT'],
      }] : []),
    ],
  },
  plugins: [
    'expo-router',
    [
      'expo-camera',
      {
        cameraPermission:
          'SentinelLAN uses your camera to scan equipment QR codes. No photos or videos are stored or transmitted.',
      },
    ],
    'expo-secure-store',
  ],
  extra: {
    apiUrl: configuredApiUrl || 'https://localhost:7147',
    webUrl: configuredWebUrl || configuredApiUrl || 'https://localhost:7147',
  },
});
