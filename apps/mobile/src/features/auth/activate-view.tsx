import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import * as Linking from 'expo-linking';
import { useAuth } from './auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppInput, AppCard } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';
import { parseActivationUrl } from '../../lib/security/deep-link';

export function ActivateView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient } = useAuth();
  const params = useLocalSearchParams<{ token?: string; link?: string }>();
  const incomingUrl = Linking.useURL();

  // Secure token retention in local memory only
  const [token, setToken] = useState<string | null>(null);
  const [validating, setValidating] = useState(true);
  const [tokenInfo, setTokenInfo] = useState<{
    valid: boolean;
    email?: string;
    displayName?: string;
    organizationName?: string;
  } | null>(null);

  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (token) return;
    let rawToken: string | null = null;

    if (params.token) {
      rawToken = params.token;
    } else if (params.link) {
      const parsed = parseActivationUrl(params.link);
      if (parsed.valid && parsed.token) {
        rawToken = parsed.token;
      }
    }
    if (!rawToken && incomingUrl) {
      const parsed = parseActivationUrl(incomingUrl);
      if (parsed.valid && parsed.token) rawToken = parsed.token;
    }

    if (!rawToken) {
      setValidating(false);
      setTokenInfo({ valid: false });
      return;
    }

    setToken(rawToken);
    setValidating(true);
    // Strip token from router parameters to prevent leak via navigation history
    try {
      router.setParams({ token: undefined, link: undefined });
    } catch {
      // Ignored if router doesn't support setting undefined params in current screen
    }

  }, [params.token, params.link, incomingUrl, router, token]);

  useEffect(() => {
    if (!token) return;
    let active = true;
    apiClient
      .validateActivationToken(token)
      .then((res) => {
        if (active) {
          setTokenInfo(res);
          setValidating(false);
        }
      })
      .catch(() => {
        if (active) {
          setTokenInfo({ valid: false });
          setValidating(false);
        }
      });

    return () => {
      active = false;
    };
  }, [token, apiClient]);

  const handleSubmit = async () => {
    if (submitting || !token) return; // Guard double submit

    setErrorMessage(null);
    if (password.length < 12) {
      setErrorMessage(t('passwordTooShort'));
      return;
    }

    if (password !== confirmPassword) {
      setErrorMessage(t('passwordMismatch'));
      return;
    }

    setSubmitting(true);
    try {
      await apiClient.activateAccount({
        token,
        password,
        confirmPassword,
      });

      setSuccess(true);
      setTimeout(() => {
        router.replace('/login');
      }, 2000);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t('activationTokenInvalid');
      setErrorMessage(msg);
      setSubmitting(false);
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={[styles.container, { backgroundColor: colors.background }]}
    >
      <ScrollView contentContainerStyle={styles.scrollContent}>
        <View style={styles.brandContainer}>
          <Text style={[styles.title, { color: colors.ink }]}>{t('activationTitle')}</Text>
          <Text style={[styles.subtitle, { color: colors.inkMuted }]}>
            {t('activationSubtitle')}
          </Text>
        </View>

        <AppCard>
          {validating ? (
            <View style={styles.centered}>
              <ActivityIndicator size="large" color={colors.primary} />
              <Text style={[styles.loadingText, { color: colors.inkMuted }]}>
                Đang kiểm tra liên kết kích hoạt...
              </Text>
            </View>
          ) : success ? (
            <View style={styles.centered}>
              <Text style={styles.successEmoji}>🎉</Text>
              <Text style={[styles.successTitle, { color: colors.success }]}>
                {t('activationSuccess')}
              </Text>
            </View>
          ) : !tokenInfo?.valid ? (
            <View style={styles.centered}>
              <Text style={styles.errorEmoji}>⚠️</Text>
              <Text style={[styles.errorTitle, { color: colors.danger }]}>
                {t('activationTokenInvalid')}
              </Text>
              <AppButton
                title="Quay lại đăng nhập"
                variant="outline"
                onPress={() => router.replace('/login')}
                style={{ marginTop: spacing.lg, width: '100%' }}
              />
            </View>
          ) : (
            <View>
              {tokenInfo.organizationName && (
                <View style={[styles.orgBadge, { backgroundColor: colors.surfaceSubtle }]}>
                  <Text style={[styles.orgBadgeLabel, { color: colors.inkMuted }]}>
                    Tổ chức & Tài khoản:
                  </Text>
                  <Text style={[styles.orgBadgeValue, { color: colors.ink }]}>
                    {tokenInfo.displayName || tokenInfo.email} ({tokenInfo.organizationName})
                  </Text>
                </View>
              )}

              {errorMessage && (
                <View style={[styles.errorBox, { backgroundColor: colors.dangerLight }]}>
                  <Text style={[styles.errorText, { color: colors.danger }]}>{errorMessage}</Text>
                </View>
              )}

              <AppInput
                testID="new-password-input"
                label={t('newPasswordLabel')}
                value={password}
                onChangeText={setPassword}
                secureTextEntry
                placeholder="Tối thiểu 12 ký tự..."
              />

              <AppInput
                testID="confirm-password-input"
                label={t('confirmPasswordLabel')}
                value={confirmPassword}
                onChangeText={setConfirmPassword}
                secureTextEntry
                placeholder="Nhập lại mật khẩu..."
              />

              <AppButton
                testID="activate-submit-button"
                title={t('activateButton')}
                onPress={handleSubmit}
                loading={submitting}
                disabled={submitting}
                style={{ marginTop: spacing.md }}
              />
            </View>
          )}
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
    marginBottom: spacing.xl,
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
  centered: {
    alignItems: 'center',
    paddingVertical: spacing.xl,
  },
  loadingText: {
    ...typography.bodyMedium,
    marginTop: spacing.md,
  },
  successEmoji: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  successTitle: {
    ...typography.titleSmall,
    textAlign: 'center',
  },
  errorEmoji: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  errorTitle: {
    ...typography.bodyMedium,
    textAlign: 'center',
  },
  orgBadge: {
    padding: spacing.md,
    borderRadius: 8,
    marginBottom: spacing.lg,
  },
  orgBadgeLabel: {
    ...typography.caption,
    textTransform: 'uppercase',
  },
  orgBadgeValue: {
    ...typography.label,
    marginTop: 2,
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
