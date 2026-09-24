import { ExpoConfig, ConfigContext } from 'expo/config';

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
    associatedDomains: ['applinks:sentinellan.local', 'applinks:dashboard.sentinellan.local'],
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
        data: [
          { scheme: 'sentinellan' },
          {
            scheme: 'https',
            host: 'sentinellan.local',
            pathPrefix: '/activate',
          },
        ],
        category: ['BROWSABLE', 'DEFAULT'],
      },
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
    apiUrl: process.env.EXPO_PUBLIC_API_URL || 'https://localhost:7147',
    eas: {
      projectId: 'sentinellan-employee',
    },
  },
});
