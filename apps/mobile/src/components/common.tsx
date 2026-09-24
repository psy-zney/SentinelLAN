import React from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  ViewStyle,
  TextStyle,
  TextInputProps,
  useColorScheme,
} from 'react-native';
import { lightColors, darkColors, spacing, radii, typography, minTouchTarget } from '../theme/tokens';

export function useAppColors() {
  const isDark = useColorScheme() === 'dark';
  return isDark ? darkColors : lightColors;
}

interface ButtonProps {
  title: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'danger' | 'outline';
  loading?: boolean;
  disabled?: boolean;
  style?: ViewStyle;
  testID?: string;
  accessibilityLabel?: string;
}

export function AppButton({
  title,
  onPress,
  variant = 'primary',
  loading = false,
  disabled = false,
  style,
  testID,
  accessibilityLabel,
}: ButtonProps) {
  const colors = useAppColors();

  const getBackgroundColor = () => {
    if (disabled || loading) return colors.border;
    switch (variant) {
      case 'primary':
        return colors.primary;
      case 'secondary':
        return colors.surfaceSubtle;
      case 'danger':
        return colors.danger;
      case 'outline':
        return 'transparent';
    }
  };

  const getTextColor = () => {
    if (disabled || loading) return colors.inkSubtle;
    switch (variant) {
      case 'primary':
      case 'danger':
        return colors.inkInverse;
      case 'secondary':
      case 'outline':
        return colors.primary;
    }
  };

  const getBorderColor = () => {
    if (variant === 'outline') return colors.primary;
    return 'transparent';
  };

  return (
    <TouchableOpacity
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel || title}
      accessibilityState={{ disabled: disabled || loading, busy: loading }}
      activeOpacity={0.8}
      onPress={onPress}
      disabled={disabled || loading}
      style={[
        styles.buttonBase,
        {
          backgroundColor: getBackgroundColor(),
          borderColor: getBorderColor(),
          borderWidth: variant === 'outline' ? 1.5 : 0,
        },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={getTextColor()} size="small" />
      ) : (
        <Text style={[styles.buttonText, { color: getTextColor() }]}>{title}</Text>
      )}
    </TouchableOpacity>
  );
}

interface InputProps extends TextInputProps {
  label: string;
  error?: string | null;
  hint?: string;
  testID?: string;
}

export function AppInput({ label, error, hint, style, testID, ...rest }: InputProps) {
  const colors = useAppColors();

  return (
    <View style={styles.inputWrapper}>
      <Text style={[styles.inputLabel, { color: colors.ink }]}>{label}</Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        accessibilityHint={hint}
        placeholderTextColor={colors.inkSubtle}
        style={[
          styles.inputBase,
          {
            backgroundColor: colors.surface,
            borderColor: error ? colors.danger : colors.border,
            color: colors.ink,
          },
          style,
        ]}
        {...rest}
      />
      {error ? (
        <Text accessibilityRole="alert" style={[styles.inputError, { color: colors.danger }]}>
          {error}
        </Text>
      ) : hint ? (
        <Text style={[styles.inputHint, { color: colors.inkMuted }]}>{hint}</Text>
      ) : null}
    </View>
  );
}

export function AppCard({
  children,
  style,
  testID,
}: {
  children: React.ReactNode;
  style?: ViewStyle;
  testID?: string;
}) {
  const colors = useAppColors();
  return (
    <View
      testID={testID}
      style={[
        styles.cardBase,
        {
          backgroundColor: colors.surface,
          borderColor: colors.border,
          shadowColor: colors.cardShadow,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}

export function AppBadge({
  text,
  variant = 'info',
}: {
  text: string;
  variant?: 'success' | 'warning' | 'danger' | 'info';
}) {
  const colors = useAppColors();

  const getColors = () => {
    switch (variant) {
      case 'success':
        return { bg: colors.successLight, text: colors.success };
      case 'warning':
        return { bg: colors.warningLight, text: colors.warning };
      case 'danger':
        return { bg: colors.dangerLight, text: colors.danger };
      case 'info':
        return { bg: colors.infoLight, text: colors.info };
    }
  };

  const scheme = getColors();

  return (
    <View style={[styles.badgeBase, { backgroundColor: scheme.bg }]}>
      <Text style={[styles.badgeText, { color: scheme.text }]}>{text}</Text>
    </View>
  );
}

export function OfflineBanner({ message }: { message: string }) {
  const colors = useAppColors();
  return (
    <View
      accessibilityRole="alert"
      style={[styles.bannerBase, { backgroundColor: colors.warningLight, borderColor: colors.warning }]}
    >
      <Text style={[styles.bannerText, { color: colors.warning }]}>⚠️ {message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  buttonBase: {
    minHeight: minTouchTarget,
    minWidth: minTouchTarget,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
    borderRadius: radii.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  buttonText: {
    ...typography.label,
    textAlign: 'center',
  },
  inputWrapper: {
    marginBottom: spacing.lg,
  },
  inputLabel: {
    ...typography.label,
    marginBottom: spacing.xs,
  },
  inputBase: {
    minHeight: minTouchTarget,
    borderWidth: 1,
    borderRadius: radii.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    ...typography.bodyMedium,
  },
  inputError: {
    ...typography.caption,
    marginTop: spacing.xs,
  },
  inputHint: {
    ...typography.caption,
    marginTop: spacing.xs,
  },
  cardBase: {
    borderRadius: radii.lg,
    borderWidth: 1,
    padding: spacing.lg,
    marginBottom: spacing.lg,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 1,
    shadowRadius: 6,
    elevation: 2,
  },
  badgeBase: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radii.full,
    alignSelf: 'flex-start',
  },
  badgeText: {
    ...typography.caption,
    fontWeight: '700',
  },
  bannerBase: {
    padding: spacing.md,
    borderBottomWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  bannerText: {
    ...typography.bodySmall,
    fontWeight: '600',
  },
});
