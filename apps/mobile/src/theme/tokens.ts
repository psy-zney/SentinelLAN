export const lightColors = {
  primary: '#0b6b5f',
  primaryDark: '#074d44',
  primaryLight: '#e8f5f1',
  background: '#f8faf9',
  surface: '#ffffff',
  surfaceSubtle: '#f1f5f3',
  border: '#dbe5e1',
  borderStrong: '#b5c7c1',
  ink: '#142623',
  inkMuted: '#526662',
  inkSubtle: '#7d948f',
  inkInverse: '#ffffff',
  success: '#059669',
  successLight: '#ecfdf5',
  warning: '#d97706',
  warningLight: '#fffbeb',
  danger: '#dc2626',
  dangerLight: '#fef2f2',
  info: '#2563eb',
  infoLight: '#eff6ff',
  cardShadow: 'rgba(20, 38, 35, 0.06)',
};

export const darkColors = {
  primary: '#14b8a6',
  primaryDark: '#0d9488',
  primaryLight: '#132e2b',
  background: '#0d1715',
  surface: '#152422',
  surfaceSubtle: '#1b302d',
  border: '#25423e',
  borderStrong: '#335954',
  ink: '#f0fdf4',
  inkMuted: '#94a3b8',
  inkSubtle: '#64748b',
  inkInverse: '#0d1715',
  success: '#10b981',
  successLight: '#064e3b',
  warning: '#f59e0b',
  warningLight: '#451a03',
  danger: '#ef4444',
  dangerLight: '#450a0a',
  info: '#3b82f6',
  infoLight: '#1e3a8a',
  cardShadow: 'rgba(0, 0, 0, 0.4)',
};

export type ThemeColors = typeof lightColors;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
};

export const radii = {
  sm: 6,
  md: 10,
  lg: 14,
  full: 9999,
};

export const typography = {
  titleLarge: { fontSize: 24, lineHeight: 30, fontWeight: '700' as const },
  titleMedium: { fontSize: 20, lineHeight: 26, fontWeight: '700' as const },
  titleSmall: { fontSize: 16, lineHeight: 22, fontWeight: '600' as const },
  bodyLarge: { fontSize: 16, lineHeight: 24, fontWeight: '400' as const },
  bodyMedium: { fontSize: 14, lineHeight: 20, fontWeight: '400' as const },
  bodySmall: { fontSize: 12, lineHeight: 16, fontWeight: '400' as const },
  caption: { fontSize: 11, lineHeight: 14, fontWeight: '500' as const },
  label: { fontSize: 13, lineHeight: 18, fontWeight: '600' as const },
};

export const minTouchTarget = 44;
