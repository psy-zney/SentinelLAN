import React from 'react';
import {
  View,
  Text,
  ScrollView,
  RefreshControl,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useAuth } from '../auth/auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppCard, AppBadge } from '../../components/common';
import { spacing, typography, radii } from '../../theme/tokens';

export function MyDeviceView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient } = useAuth();

  const {
    data: deviceData,
    isLoading,
    isError,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['my-device'],
    queryFn: () => apiClient.myDevice(),
  });

  if (isLoading) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.background }]}>
        <ActivityIndicator size="large" color={colors.primary} />
        <Text style={[styles.loadingText, { color: colors.inkMuted }]}>{t('loading')}</Text>
      </View>
    );
  }

  if (isError) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.background, padding: spacing.xl }]}>
        <Text style={[styles.errorEmoji]}>⚠️</Text>
        <Text style={[styles.errorTitle, { color: colors.ink }]}>{t('error')}</Text>
        <Text style={[styles.errorDesc, { color: colors.inkMuted }]}>
          {error instanceof Error ? error.message : t('error')}
        </Text>
        <AppButton title={t('retry')} onPress={() => refetch()} style={{ marginTop: spacing.lg }} />
      </View>
    );
  }

  if (!deviceData) {
    return (
      <ScrollView
        contentContainerStyle={[styles.centered, { backgroundColor: colors.background, padding: spacing.xl }]}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={() => { void refetch(); }} />
        }
      >
        <AppCard style={styles.noDeviceCard}>
          <Text style={styles.noDeviceEmoji}>💻</Text>
          <Text style={[styles.noDeviceTitle, { color: colors.ink }]}>
            {t('noDeviceAssignedTitle')}
          </Text>
          <Text style={[styles.noDeviceDesc, { color: colors.inkMuted }]}>
            {t('noDeviceAssignedDesc')}
          </Text>
          <AppButton
            title={t('tabScan')}
            onPress={() => router.push('/(tabs)/scan')}
            style={{ width: '100%', marginTop: spacing.lg }}
          />
        </AppCard>
      </ScrollView>
    );
  }

  const { device, appliedPolicy, latestTelemetry, serialNumber, manufacturer, model, location, assignedAt } =
    deviceData;

  const isOnline = device.isOnline;

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
      refreshControl={
        <RefreshControl refreshing={isRefetching} onRefresh={() => { void refetch(); }} />
      }
    >
      {/* Device Header Card */}
      <AppCard>
        <View style={styles.headerRow}>
          <View style={{ flex: 1 }}>
            <Text style={[styles.deviceName, { color: colors.ink }]}>{device.name}</Text>
            <Text style={[styles.assignedDate, { color: colors.inkMuted }]}>
              Gán từ: {new Date(assignedAt).toLocaleDateString()}
            </Text>
          </View>
          <AppBadge
            text={isOnline ? t('deviceStatusOnline') : t('deviceStatusOffline')}
            variant={isOnline ? 'success' : 'warning'}
          />
        </View>

        {device.lastSeen && (
          <Text style={[styles.lastSeenText, { color: colors.inkSubtle }]}>
            {t('lastSeen')}: {new Date(device.lastSeen).toLocaleString()}
          </Text>
        )}
      </AppCard>

      {/* Telemetry Gauge Cards */}
      <Text style={[styles.sectionTitle, { color: colors.ink }]}>Telemetry kỹ thuật</Text>
      <View style={styles.telemetryRow}>
        <AppCard style={styles.telemetryCard}>
          <Text style={[styles.telemetryLabel, { color: colors.inkMuted }]}>{t('cpuUsage')}</Text>
          <Text style={[styles.telemetryValue, { color: colors.primary }]}>
            {latestTelemetry?.cpuPercent !== undefined ? `${Math.round(latestTelemetry.cpuPercent)}%` : '--'}
          </Text>
        </AppCard>

        <AppCard style={styles.telemetryCard}>
          <Text style={[styles.telemetryLabel, { color: colors.inkMuted }]}>{t('ramUsage')}</Text>
          <Text style={[styles.telemetryValue, { color: colors.primary }]}>
            {latestTelemetry?.ramPercent !== undefined ? `${Math.round(latestTelemetry.ramPercent)}%` : '--'}
          </Text>
        </AppCard>

        <AppCard style={styles.telemetryCard}>
          <Text style={[styles.telemetryLabel, { color: colors.inkMuted }]}>{t('diskUsage')}</Text>
          <Text style={[styles.telemetryValue, { color: colors.primary }]}>
            {latestTelemetry?.diskPercent !== undefined ? `${Math.round(latestTelemetry.diskPercent)}%` : '--'}
          </Text>
        </AppCard>
      </View>

      {/* System Specifications Card */}
      <Text style={[styles.sectionTitle, { color: colors.ink }]}>{t('systemSpecs')}</Text>
      <AppCard>
        <SpecRow label={t('osVersion')} value={device.osVersion ?? t('notReported')} />
        <SpecRow label={t('agentVersion')} value={device.agentVersion ?? t('notReported')} />
        {serialNumber && <SpecRow label={t('serialNumber')} value={serialNumber} />}
        {manufacturer && <SpecRow label="Nhà sản xuất" value={manufacturer} />}
        {model && <SpecRow label="Model" value={model} />}
        {location && <SpecRow label={t('location')} value={location} />}
        <SpecRow label={t('appliedPolicy')} value={appliedPolicy ?? t('noAssignedPolicy')} />
      </AppCard>

      {/* Quick Action Buttons */}
      <View style={styles.actionContainer}>
        <AppButton
          title={`🚨 ${t('reportIncidentButton')}`}
          onPress={() => router.push('/incident-create')}
          style={{ marginBottom: spacing.md }}
        />
        <AppButton
          title={t('privacyManifestLink')}
          variant="outline"
          onPress={() => router.push('/privacy-manifest')}
        />
      </View>
    </ScrollView>
  );
}

function SpecRow({ label, value }: { label: string; value: string }) {
  const colors = useAppColors();
  return (
    <View style={styles.specRow}>
      <Text style={[styles.specLabel, { color: colors.inkMuted }]}>{label}</Text>
      <Text style={[styles.specValue, { color: colors.ink }]}>{value}</Text>
    </View>
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
  },
  loadingText: {
    ...typography.bodyMedium,
    marginTop: spacing.md,
  },
  errorEmoji: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  errorTitle: {
    ...typography.titleMedium,
    marginBottom: spacing.sm,
  },
  errorDesc: {
    ...typography.bodyMedium,
    textAlign: 'center',
  },
  noDeviceCard: {
    alignItems: 'center',
    padding: spacing.xxl,
  },
  noDeviceEmoji: {
    fontSize: 56,
    marginBottom: spacing.md,
  },
  noDeviceTitle: {
    ...typography.titleMedium,
    marginBottom: spacing.sm,
    textAlign: 'center',
  },
  noDeviceDesc: {
    ...typography.bodyMedium,
    textAlign: 'center',
    lineHeight: 22,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: spacing.sm,
  },
  deviceName: {
    ...typography.titleMedium,
  },
  assignedDate: {
    ...typography.caption,
    marginTop: spacing.xs,
  },
  lastSeenText: {
    ...typography.caption,
  },
  sectionTitle: {
    ...typography.titleSmall,
    marginTop: spacing.md,
    marginBottom: spacing.sm,
  },
  telemetryRow: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  telemetryCard: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: spacing.lg,
    paddingHorizontal: spacing.sm,
    marginBottom: 0,
  },
  telemetryLabel: {
    ...typography.caption,
    fontWeight: '600',
    marginBottom: spacing.xs,
  },
  telemetryValue: {
    ...typography.titleLarge,
  },
  specRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#e2e8e5',
  },
  specLabel: {
    ...typography.bodyMedium,
  },
  specValue: {
    ...typography.label,
  },
  actionContainer: {
    marginTop: spacing.md,
    marginBottom: spacing.xxxl,
  },
});
