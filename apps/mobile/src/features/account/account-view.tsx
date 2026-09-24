import React, { useState } from 'react';
import { View, Text, ScrollView, StyleSheet, Alert, TouchableOpacity } from 'react-native';
import Constants from 'expo-constants';
import { useRouter } from 'expo-router';
import { useAuth } from '../auth/auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppCard, AppBadge } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';

export function AccountView() {
  const { t, lang, setLang } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { user, logout, logoutAll } = useAuth();

  const [loggingOut, setLoggingOut] = useState(false);

  const handleLogout = () => {
    Alert.alert(t('logoutConfirmTitle'), t('logoutConfirmMsg'), [
      { text: t('cancel'), style: 'cancel' },
      {
        text: t('logoutButton'),
        style: 'destructive',
        onPress: async () => {
          setLoggingOut(true);
          try {
            await logout();
            router.replace('/login');
          } finally {
            setLoggingOut(false);
          }
        },
      },
    ]);
  };

  const handleLogoutAll = () => {
    Alert.alert(t('logoutAllButton'), t('logoutAllConfirmMsg'), [
      { text: t('cancel'), style: 'cancel' },
      {
        text: t('logoutAllButton'),
        style: 'destructive',
        onPress: async () => {
          setLoggingOut(true);
          try {
            await logoutAll();
            router.replace('/login');
          } finally {
            setLoggingOut(false);
          }
        },
      },
    ]);
  };

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
    >
      {/* User Info Card */}
      <AppCard>
        <View style={styles.userHeader}>
          <View style={[styles.avatarBox, { backgroundColor: colors.primaryLight }]}>
            <Text style={[styles.avatarText, { color: colors.primary }]}>
              {user?.displayName?.charAt(0).toUpperCase() || 'U'}
            </Text>
          </View>
          <View style={{ flex: 1, marginLeft: spacing.md }}>
            <Text style={[styles.userName, { color: colors.ink }]}>
              {user?.displayName || 'Nhân viên'}
            </Text>
            <Text style={[styles.userEmail, { color: colors.inkMuted }]}>{user?.email}</Text>
            <View style={{ marginTop: spacing.xs }}>
              <AppBadge text={user?.role || 'Employee'} variant="info" />
            </View>
          </View>
        </View>

        <View style={styles.divider} />

        <View style={styles.infoRow}>
          <Text style={[styles.infoLabel, { color: colors.inkMuted }]}>{t('organization')}:</Text>
          <Text style={[styles.infoValue, { color: colors.ink }]}>{user?.organizationCode}</Text>
        </View>

        <View style={styles.infoRow}>
          <Text style={[styles.infoLabel, { color: colors.inkMuted }]}>{t('appVersion')}:</Text>
          <Text style={[styles.infoValue, { color: colors.ink }]}>
            v{Constants.expoConfig?.version || '1.0.0'} (New Architecture)
          </Text>
        </View>
      </AppCard>

      {/* Language Preferences Card */}
      <AppCard>
        <Text style={[styles.sectionTitle, { color: colors.ink }]}>{t('switchLanguage')}</Text>
        <View style={styles.languageRow}>
          <TouchableOpacity
            accessibilityRole="button"
            accessibilityLabel="Tiếng Việt"
            style={[
              styles.langPill,
              {
                backgroundColor: lang === 'vi' ? colors.primary : colors.surfaceSubtle,
                borderColor: lang === 'vi' ? colors.primary : colors.border,
              },
            ]}
            onPress={() => setLang('vi')}
          >
            <Text style={[styles.langText, { color: lang === 'vi' ? colors.inkInverse : colors.ink }]}>
              🇻🇳 Tiếng Việt
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            accessibilityRole="button"
            accessibilityLabel="English"
            style={[
              styles.langPill,
              {
                backgroundColor: lang === 'en' ? colors.primary : colors.surfaceSubtle,
                borderColor: lang === 'en' ? colors.primary : colors.border,
              },
            ]}
            onPress={() => setLang('en')}
          >
            <Text style={[styles.langText, { color: lang === 'en' ? colors.inkInverse : colors.ink }]}>
              🇬🇧 English
            </Text>
          </TouchableOpacity>
        </View>
      </AppCard>

      {/* Privacy Manifest Link */}
      <AppButton
        title={t('privacyManifestTitle')}
        variant="outline"
        onPress={() => router.push('/privacy-manifest')}
        style={{ marginBottom: spacing.lg }}
      />

      {/* Session Management Actions */}
      <AppButton
        testID="logout-button"
        title={t('logoutButton')}
        variant="outline"
        onPress={handleLogout}
        loading={loggingOut}
        style={{ marginBottom: spacing.md, borderColor: colors.danger }}
      />

      <AppButton
        testID="logout-all-button"
        title={t('logoutAllButton')}
        variant="danger"
        onPress={handleLogoutAll}
        loading={loggingOut}
        style={{ marginBottom: spacing.xxxl }}
      />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  content: {
    padding: spacing.lg,
  },
  userHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  avatarBox: {
    width: 56,
    height: 56,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarText: {
    fontSize: 24,
    fontWeight: '800',
  },
  userName: {
    ...typography.titleSmall,
  },
  userEmail: {
    ...typography.caption,
    marginTop: 2,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: '#dbe5e1',
    marginVertical: spacing.md,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.xs,
  },
  infoLabel: {
    ...typography.bodyMedium,
  },
  infoValue: {
    ...typography.label,
  },
  sectionTitle: {
    ...typography.titleSmall,
    marginBottom: spacing.md,
  },
  languageRow: {
    flexDirection: 'row',
    gap: spacing.md,
  },
  langPill: {
    flex: 1,
    minHeight: 44,
    borderWidth: 1,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  langText: {
    ...typography.label,
  },
});
