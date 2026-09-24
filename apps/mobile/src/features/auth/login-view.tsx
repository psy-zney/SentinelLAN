import React, { useState } from 'react';
import { View, Text, ScrollView, StyleSheet, KeyboardAvoidingView, Platform } from 'react-native';
import { useRouter } from 'expo-router';
import { useAuth } from './auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppInput, AppCard } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';

export function LoginView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { login } = useAuth();

  const [orgCode, setOrgCode] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleLogin = async () => {
    setErrorMsg(null);

    const trimmedOrg = orgCode.trim();
    const trimmedEmail = email.trim();

    if (!trimmedOrg || !trimmedEmail || !password) {
      setErrorMsg('Vui lòng điền đầy đủ mã tổ chức, email và mật khẩu.');
      return;
    }

    setLoading(true);
    try {
      await login({
        organizationCode: trimmedOrg,
        email: trimmedEmail,
        password,
      });
      router.replace('/(tabs)');
    } catch {
      setErrorMsg(t('loginFailed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={[styles.container, { backgroundColor: colors.background }]}
    >
      <ScrollView contentContainerStyle={styles.scrollContent}>
        {/* Brand Header */}
        <View style={styles.brandContainer}>
          <View style={[styles.logoBadge, { borderColor: colors.primary }]}>
            <Text style={[styles.logoText, { color: colors.primary }]}>S</Text>
          </View>
          <Text style={[styles.title, { color: colors.ink }]}>{t('loginTitle')}</Text>
          <Text style={[styles.subtitle, { color: colors.inkMuted }]}>{t('loginSubtitle')}</Text>
        </View>

        <AppCard>
          {errorMsg ? (
            <View style={[styles.errorBox, { backgroundColor: colors.dangerLight }]}>
              <Text style={[styles.errorText, { color: colors.danger }]}>{errorMsg}</Text>
            </View>
          ) : null}

          <AppInput
            testID="org-code-input"
            label={t('orgCodeLabel')}
            placeholder={t('orgCodePlaceholder')}
            value={orgCode}
            onChangeText={setOrgCode}
            autoCapitalize="none"
            autoCorrect={false}
          />

          <AppInput
            testID="email-input"
            label={t('emailLabel')}
            placeholder={t('emailPlaceholder')}
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
          />

          <AppInput
            testID="password-input"
            label={t('passwordLabel')}
            placeholder={t('passwordPlaceholder')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
          />

          <AppButton
            testID="login-submit-button"
            title={t('loginButton')}
            onPress={handleLogin}
            loading={loading}
            disabled={loading}
            style={{ marginTop: spacing.md }}
          />
        </AppCard>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.xl,
    paddingTop: spacing.xxxl,
    justifyContent: 'center',
  },
  brandContainer: {
    alignItems: 'center',
    marginBottom: spacing.xxl,
  },
  logoBadge: {
    width: 56,
    height: 60,
    borderWidth: 3,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: spacing.md,
  },
  logoText: {
    fontSize: 32,
    fontWeight: '900',
  },
  title: {
    ...typography.titleLarge,
    marginBottom: spacing.xs,
    textAlign: 'center',
  },
  subtitle: {
    ...typography.bodyMedium,
    textAlign: 'center',
    lineHeight: 20,
  },
  errorBox: {
    padding: spacing.md,
    borderRadius: 8,
    marginBottom: spacing.lg,
  },
  errorText: {
    ...typography.bodySmall,
    fontWeight: '600',
    textAlign: 'center',
  },
});
