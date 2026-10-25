import React from 'react';
import { Pressable, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useAppColors } from './common';
import { useAuth } from '../features/auth/auth-context';
import { useI18n } from '../lib/i18n';

export function BackNavigation() {
  const router = useRouter();
  const colors = useAppColors();
  const { status } = useAuth();
  const { lang } = useI18n();
  const label = lang === 'en' ? 'Back' : 'Quay lại';
  return <Pressable accessibilityRole="button" accessibilityLabel={label} hitSlop={4}
    onPress={() => {
      if (router.canGoBack()) router.back();
      else router.replace(status === 'authenticated' ? '/(tabs)' : '/login');
    }} style={({ pressed }) => [styles.button, { opacity: pressed ? 0.65 : 1 }]}>
    <Text accessible={false} style={[styles.arrow, { color: colors.primary }]}>←</Text>
    <Text style={[styles.label, { color: colors.primary }]}>{label}</Text>
  </Pressable>;
}

const styles = StyleSheet.create({
  button: { flexDirection: 'row', alignItems: 'center', gap: 6, minHeight: 48, minWidth: 48, paddingHorizontal: 8 },
  label: { fontSize: 15, fontWeight: '600' },
  arrow: { fontSize: 24 },
});
