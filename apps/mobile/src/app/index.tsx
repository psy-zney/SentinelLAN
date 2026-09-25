import React, { useEffect } from 'react';
import { View, Text, StyleSheet, ActivityIndicator, Alert } from 'react-native';
import { useRouter } from 'expo-router';
import { useAuth } from '../features/auth/auth-context';
import { useAppColors, AppButton, OfflineBanner } from '../components/common';
import { useI18n } from '../lib/i18n';
import { spacing, typography } from '../theme/tokens';

export default function SplashScreen() {
  const { status, dismissSessionExpired, retrySession } = useAuth();
  const router = useRouter();
  const colors = useAppColors();
  const { t } = useI18n();

  useEffect(() => {
    if (status === 'authenticated') {
      router.replace('/(tabs)');
    } else if (status === 'unauthenticated') {
      router.replace('/login');
    } else if (status === 'session-expired') {
      Alert.alert(t('appName'), t('sessionExpired'), [
        {
          text: 'OK',
          onPress: () => {
            dismissSessionExpired();
            router.replace('/login');
          },
        },
      ]);
    }
  }, [status, router, t, dismissSessionExpired]);

  if (status === 'update-required') {
    return (
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <View style={styles.card}>
          <Text style={styles.emoji}>📲</Text>
          <Text style={[styles.title, { color: colors.ink }]}>Yêu cầu cập nhật ứng dụng</Text>
          <Text style={[styles.subtitle, { color: colors.inkMuted }]}>
            Phiên bản ứng dụng hiện tại của bạn không còn được hỗ trợ. Vui lòng cập nhật phiên bản
            mới nhất từ bộ phận IT để tiếp tục sử dụng.
          </Text>
          <AppButton
            title="Đóng ứng dụng"
            variant="outline"
            onPress={() => {}}
            style={{ marginTop: spacing.xl, width: '100%' }}
          />
        </View>
      </View>
    );
  }

  if (status === 'offline') {
    return (
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <OfflineBanner message={t('offlineBanner')} />
        <AppButton title={t('retry')} onPress={() => { void retrySession(); }} style={{ marginTop: spacing.lg }} />
      </View>
    );
  }

  return (
    <View style={[styles.container, { backgroundColor: colors.primary }]}>
      <View style={styles.brandBox}>
        <View style={styles.logoCircle}>
          <Text style={styles.logoText}>S</Text>
        </View>
        <Text style={styles.brandName}>SentinelLAN</Text>
        <Text style={styles.brandTagline}>Secure Endpoint Governance</Text>
      </View>
      <ActivityIndicator size="large" color="#ffffff" style={{ marginTop: spacing.xxl }} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.xl,
  },
  brandBox: {
    alignItems: 'center',
  },
  logoCircle: {
    width: 72,
    height: 72,
    borderRadius: 20,
    backgroundColor: '#ffffff',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: spacing.md,
  },
  logoText: {
    fontSize: 40,
    fontWeight: '900',
    color: '#0b6b5f',
  },
  brandName: {
    fontSize: 26,
    fontWeight: '800',
    color: '#ffffff',
  },
  brandTagline: {
    ...typography.caption,
    color: 'rgba(255, 255, 255, 0.8)',
    marginTop: spacing.xs,
  },
  card: {
    width: '100%',
    alignItems: 'center',
    padding: spacing.xl,
  },
  emoji: {
    fontSize: 56,
    marginBottom: spacing.lg,
  },
  title: {
    ...typography.titleMedium,
    marginBottom: spacing.sm,
    textAlign: 'center',
  },
  subtitle: {
    ...typography.bodyMedium,
    textAlign: 'center',
    lineHeight: 22,
  },
});
