/* eslint-disable @typescript-eslint/no-explicit-any */
// Mock expo-secure-store in memory for Jest tests
const mockStore: Record<string, string> = {};

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (key: string) => mockStore[key] ?? null),
  setItemAsync: jest.fn(async (key: string, value: string) => {
    mockStore[key] = value;
  }),
  deleteItemAsync: jest.fn(async (key: string) => {
    delete mockStore[key];
  }),
}));

// Mock expo-constants
jest.mock('expo-constants', () => ({
  expoConfig: {
    extra: {
      apiUrl: 'https://sentinellan.local',
    },
    version: '1.0.0',
  },
}));

// Mock expo-linking
jest.mock('expo-linking', () => ({
  createURL: jest.fn((path: string) => `sentinellan://${path}`),
  addEventListener: jest.fn(() => ({ remove: jest.fn() })),
  getInitialURL: jest.fn(async () => null),
  useURL: jest.fn(() => null),
  openSettings: jest.fn(async () => {}),
}));

// Mock expo-camera
jest.mock('expo-camera', () => {
  const React = require('react');
  const { View } = require('react-native');
  return {
    CameraView: (props: any) => React.createElement(View, { testID: 'mock-camera-view', ...props }),
    Camera: { scanFromURLAsync: jest.fn(async () => []) },
    useCameraPermissions: jest.fn(() => [
      { granted: true, canAskAgain: true, status: 'granted' },
      jest.fn(async () => ({ granted: true })),
    ]),
  };
});

// Mock expo-image-picker
jest.mock('expo-image-picker', () => ({
  launchImageLibraryAsync: jest.fn(async () => ({
    canceled: false,
    assets: [{ uri: 'file://mock/qr-image.png' }],
  })),
  MediaTypeOptions: { Images: 'Images' },
}));

// Mock expo-router
jest.mock('expo-router', () => ({
  useRouter: () => ({
    push: jest.fn(),
    replace: jest.fn(),
    back: jest.fn(),
    setParams: jest.fn(),
  }),
  useLocalSearchParams: jest.fn(() => ({})),
  usePathname: () => '/',
  Link: ({ children }: any) => children,
  Slot: ({ children }: any) => children,
  Stack: ({ children }: any) => children,
  Tabs: ({ children }: any) => children,
}));

// Set AppState.currentState to 'active' in Jest environment
const { AppState } = require('react-native');
if (AppState) {
  try {
    AppState.currentState = 'active';
  } catch {
    // Ignore if readonly
  }
}
