import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppInput, AppCard } from '../../components/common';
import { spacing, typography, radii } from '../../theme/tokens';

type Severity = 'Low' | 'Medium' | 'High' | 'Critical';

export function IncidentCreateView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [severity, setSeverity] = useState<Severity>('Medium');
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Client-generated idempotency key to prevent accidental duplicate submission
  const [idempotencyKey] = useState(() => {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  });

  const handleSubmit = async () => {
    if (submitting) return; // Guard double submit

    setErrorMessage(null);
    const trimmedTitle = title.trim();
    if (trimmedTitle.length < 3) {
      setErrorMessage('Tiêu đề sự cố phải có ít nhất 3 ký tự');
      return;
    }

    setSubmitting(true);
    try {
      await apiClient.reportIncident({
        title: trimmedTitle,
        description: description.trim() || undefined,
        severity,
        idempotencyKey,
      });

      // Invalidate cache to fetch fresh incident list
      await queryClient.invalidateQueries({ queryKey: ['my-device-incidents'] });
      await queryClient.invalidateQueries({ queryKey: ['my-device'] });

      Alert.alert(t('success'), t('reportSuccess'), [
        {
          text: 'OK',
          onPress: () => router.back(),
        },
      ]);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t('error');
      setErrorMessage(msg);
      setSubmitting(false);
    }
  };

  const severityOptions: Severity[] = ['Low', 'Medium', 'High', 'Critical'];

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
    >
      <AppCard>
        <Text style={[styles.cardTitle, { color: colors.ink }]}>{t('reportIncidentButton')}</Text>

        {errorMessage && (
          <View style={[styles.errorBox, { backgroundColor: colors.dangerLight }]}>
            <Text style={[styles.errorText, { color: colors.danger }]}>{errorMessage}</Text>
          </View>
        )}

        <AppInput
          testID="incident-title-input"
          label={t('incidentTitleLabel')}
          placeholder={t('incidentTitlePlaceholder')}
          value={title}
          onChangeText={setTitle}
          maxLength={200}
        />

        <AppInput
          testID="incident-desc-input"
          label={t('incidentDescLabel')}
          placeholder={t('incidentDescPlaceholder')}
          value={description}
          onChangeText={setDescription}
          multiline
          numberOfLines={4}
          style={styles.textArea}
          maxLength={2000}
        />

        {/* Severity Selector */}
        <Text style={[styles.fieldLabel, { color: colors.ink }]}>{t('severityLabel')}</Text>
        <View style={styles.severityRow}>
          {severityOptions.map((opt) => {
            const isSelected = severity === opt;
            return (
              <TouchableOpacity
                key={opt}
                accessibilityRole="button"
                accessibilityLabel={`Mức độ ${opt}`}
                style={[
                  styles.severityPill,
                  {
                    backgroundColor: isSelected ? colors.primary : colors.surfaceSubtle,
                    borderColor: isSelected ? colors.primary : colors.border,
                  },
                ]}
                onPress={() => setSeverity(opt)}
              >
                <Text
                  style={[
                    styles.severityText,
                    { color: isSelected ? colors.inkInverse : colors.ink },
                  ]}
                >
                  {opt === 'Low'
                    ? t('severityLow')
                    : opt === 'Medium'
                    ? t('severityMedium')
                    : opt === 'High'
                    ? t('severityHigh')
                    : t('severityCritical')}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>

        <View style={styles.actionRow}>
          <AppButton
            title={t('cancel')}
            variant="outline"
            onPress={() => router.back()}
            disabled={submitting}
            style={{ flex: 1, marginRight: spacing.sm }}
          />
          <AppButton
            testID="submit-incident-button"
            title={t('reportIncidentButton')}
            onPress={handleSubmit}
            loading={submitting}
            disabled={submitting || title.trim().length < 3}
            style={{ flex: 1.5 }}
          />
        </View>
      </AppCard>
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
  cardTitle: {
    ...typography.titleMedium,
    marginBottom: spacing.lg,
  },
  errorBox: {
    padding: spacing.md,
    borderRadius: radii.md,
    marginBottom: spacing.md,
  },
  errorText: {
    ...typography.bodySmall,
    fontWeight: '600',
  },
  textArea: {
    minHeight: 100,
    textAlignVertical: 'top',
  },
  fieldLabel: {
    ...typography.label,
    marginBottom: spacing.sm,
  },
  severityRow: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginBottom: spacing.xl,
  },
  severityPill: {
    flex: 1,
    minHeight: 40,
    borderRadius: radii.md,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: spacing.xs,
  },
  severityText: {
    ...typography.caption,
    fontWeight: '700',
  },
  actionRow: {
    flexDirection: 'row',
    marginTop: spacing.sm,
  },
});
