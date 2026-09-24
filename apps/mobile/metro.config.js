const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// In React Native 0.87+, rn-get-polyfills was relocated to @react-native/js-polyfills
config.serializer.getPolyfills = ({ platform }) => {
  if (!platform) {
    return [];
  }
  return require('@react-native/js-polyfills')();
};

module.exports = config;
