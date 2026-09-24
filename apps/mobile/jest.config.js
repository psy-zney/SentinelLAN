module.exports = {
  preset: 'jest-expo',
  transformIgnorePatterns: [
    'node_modules/(?!((jest-)?react-native|@react-native(-community)?)|expo(nent)?|@expo(nent)?/.*|@expo-google-fonts/.*|react-navigation|@react-navigation/.*|@sentry/react-native|native-base|react-native-svg)',
  ],
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
    '^@react-native/assets-registry/registry$': '<rootDir>/../../node_modules/react-native/src/private/assets/AssetRegistry.js',
    '^@react-native/assets-registry/(.*)$': '<rootDir>/../../node_modules/react-native/src/private/assets/AssetRegistry.js',
  },
  testPathIgnorePatterns: ['/node_modules/', '/e2e/'],
};
