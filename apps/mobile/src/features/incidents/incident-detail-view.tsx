import React from 'react';
import { View, Text, ScrollView, StyleSheet } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppCard, AppBadge } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';
import { IncidentDto } from '../../lib/validation/schemas';

export function IncidentDetailView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const params = useLocalSearchParams<{ incidentJson?: string }>();

  let incident: IncidentDto | null = null;
  if (params.incidentJson) {
    try {
      incident = JSON.parse(params.incidentJson) as IncidentDto;
    } catch {
      incident = null;
    }
  }

  if (!incident) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.background }]}>
        <Text style={[styles.errorText, { color: colors.danger }]}>
          Không tìm thấy thông tin sự cố
        </Text>
        <AppButton title="Quay lại" onPress={() => router.back()} style={{ marginTop: spacing.md }} />
      </View>
    );
  }

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
    >
      <AppCard>
        <View style={styles.headerRow}>
          <Text style={[styles.title, { color: colors.ink }]}>{incident.title}</Text>
          <AppBadge text={incident.severity} variant="warning" />
        </View>

        <Text style={[styles.deviceName, { color: colors.inkMuted }]}>
          Thiết bị: {incident.deviceName}
        </Text>
        <Text style={[styles.dateText, { color: colors.inkSubtle }]}>
          Ngày báo: {new Date(incident.createdAt).toLocaleString()}
        </Text>

        <View style={styles.divider} />

        <Text style={[styles.sectionTitle, { color: colors.ink }]}>Mô tả sự cố:</Text>
        <Text style={[styles.bodyText, { color: colors.ink }]}>
          {incident.description || 'Không có mô tả chi tiết'}
        </Text>

        <View style={styles.divider} />

        <View style={styles.statusRow}>
          <Text style={[styles.statusLabel, { color: colors.inkMuted }]}>Trạng thái hiện tại:</Text>
          <AppBadge text={incident.status} variant="info" />
        </View>

        {incident.resolutionNotes ? (
          <View style={styles.resolutionBox}>
            <Text style={[styles.sectionTitle, { color: colors.success }]}>
              {t('resolutionNotesTitle')}
            </Text>
            <Text style={[styles.bodyText, { color: colors.ink }]}>{incident.resolutionNotes}</Text>
            {incident.resolvedAt && (
              <Text style={[styles.dateText, { color: colors.inkSubtle, marginTop: spacing.xs }]}>
                Đã giải quyết vào: {new Date(incident.resolvedAt).toLocaleString()}
              </Text>
            )}
          </View>
        ) : null}
      </AppCard>

      <AppButton
        title="Quay lại danh sách"
        variant="outline"
        onPress={() => router.back()}
        style={{ marginTop: spacing.md }}
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
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.xl,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: spacing.xs,
  },
  title: {
    flex: 1,
    ...typography.titleMedium,
    marginRight: spacing.sm,
  },
  deviceName: {
    ...typography.caption,
    marginTop: spacing.xs,
  },
  dateText: {
    ...typography.caption,
    marginTop: spacing.xs,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: '#dbe5e1',
    marginVertical: spacing.md,
  },
  sectionTitle: {
    ...typography.titleSmall,
    marginBottom: spacing.xs,
  },
  bodyText: {
    ...typography.bodyMedium,
    lineHeight: 22,
  },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: spacing.xs,
  },
  statusLabel: {
    ...typography.label,
  },
  resolutionBox: {
    marginTop: spacing.md,
    padding: spacing.md,
    backgroundColor: '#ecfdf5',
    borderRadius: 8,
  },
  errorText: {
    ...typography.bodyMedium,
  },
});
