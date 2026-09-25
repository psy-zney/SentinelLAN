import React from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, RefreshControl } from 'react-native';
import { useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../../features/auth/auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppCard, AppBadge, AppButton } from '../../components/common';
import { spacing, typography, radii } from '../../theme/tokens';

export default function OverviewScreen() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { user, apiClient } = useAuth();

  const {
    data: deviceData,
    refetch: refetchDevice,
    isRefetching,
  } = useQuery({
    queryKey: ['my-device'],
    queryFn: () => apiClient.myDevice(),
  });

  const { data: incidents, refetch: refetchIncidents } = useQuery({
    queryKey: ['my-device-incidents'],
    queryFn: () => apiClient.myDeviceIncidents(),
  });

  const onRefresh = () => {
    refetchDevice();
    refetchIncidents();
  };

  const openIncidentsCount = incidents?.filter((i) => i.status.toLowerCase() === 'open').length ?? 0;

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={onRefresh} />}
    >
      {/* Welcome Banner */}
      <View style={styles.welcomeBox}>
        <Text style={[styles.welcomeLabel, { color: colors.inkMuted }]}>Xin chào,</Text>
        <Text style={[styles.welcomeName, { color: colors.ink }]}>
          {user?.displayName || 'Nhân viên'}
        </Text>
        <Text style={[styles.orgText, { color: colors.primary }]}>
          Tổ chức: {user?.organizationCode}
        </Text>
      </View>

      {/* Assigned Device Status Card */}
      <AppCard>
        <Text style={[styles.cardHeader, { color: colors.ink }]}>{t('myDeviceTitle')}</Text>
        {deviceData ? (
          <View>
            <View style={styles.deviceRow}>
              <View>
                <Text style={[styles.deviceName, { color: colors.ink }]}>
                  {deviceData.device.name}
                </Text>
                <Text style={[styles.deviceSpec, { color: colors.inkMuted }]}>
                  {deviceData.device.osVersion ?? t('notReported')}
                </Text>
              </View>
              <AppBadge
                text={deviceData.device.isOnline ? t('deviceStatusOnline') : t('deviceStatusOffline')}
                variant={deviceData.device.isOnline ? 'success' : 'warning'}
              />
            </View>

            <TouchableOpacity
              style={[styles.linkRow, { borderTopColor: colors.border }]}
              onPress={() => router.push('/(tabs)/my-device')}
            >
              <Text style={[styles.linkText, { color: colors.primary }]}>
                Xem telemetry & thông số chi tiết →
              </Text>
            </TouchableOpacity>
          </View>
        ) : (
          <View style={styles.noDeviceRow}>
            <Text style={[styles.noDeviceNotice, { color: colors.inkMuted }]}>
              {t('noDeviceAssignedDesc')}
            </Text>
            <AppButton
              title="📷 Quét QR để nhận máy"
              onPress={() => router.push('/(tabs)/scan')}
              style={{ marginTop: spacing.md }}
            />
          </View>
        )}
      </AppCard>

      {/* Incidents Quick Card */}
      <AppCard>
        <View style={styles.incidentRow}>
          <View>
            <Text style={[styles.cardHeader, { color: colors.ink }]}>{t('incidentsTitle')}</Text>
            <Text style={[styles.incidentCountText, { color: colors.inkMuted }]}>
              {openIncidentsCount > 0
                ? `${openIncidentsCount} sự cố đang chờ xử lý`
                : 'Không có sự cố đang mở'}
            </Text>
          </View>
          <AppBadge
            text={openIncidentsCount > 0 ? `${openIncidentsCount} đang mở` : 'Ổn định'}
            variant={openIncidentsCount > 0 ? 'danger' : 'success'}
          />
        </View>

        <TouchableOpacity
          style={[styles.linkRow, { borderTopColor: colors.border }]}
          onPress={() => router.push('/(tabs)/incidents')}
        >
          <Text style={[styles.linkText, { color: colors.primary }]}>Xem toàn bộ lịch sử sự cố →</Text>
        </TouchableOpacity>
      </AppCard>

      {/* Quick Action Hub */}
      <Text style={[styles.sectionTitle, { color: colors.ink }]}>Thao tác nhanh</Text>
      <View style={styles.actionGrid}>
        <TouchableOpacity
          style={[styles.gridTile, { backgroundColor: colors.surface, borderColor: colors.border }]}
          onPress={() => router.push('/(tabs)/scan')}
        >
          <Text style={styles.tileEmoji}>📷</Text>
          <Text style={[styles.tileTitle, { color: colors.ink }]}>{t('tabScan')}</Text>
          <Text style={[styles.tileDesc, { color: colors.inkMuted }]}>Quét tem dán trên máy</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.gridTile, { backgroundColor: colors.surface, borderColor: colors.border }]}
          onPress={() => router.push('/incident-create')}
        >
          <Text style={styles.tileEmoji}>🚨</Text>
          <Text style={[styles.tileTitle, { color: colors.ink }]}>Báo sự cố</Text>
          <Text style={[styles.tileDesc, { color: colors.inkMuted }]}>Gửi phiếu hỗ trợ IT</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.gridTile, { backgroundColor: colors.surface, borderColor: colors.border }]}
          onPress={() => router.push('/privacy-manifest')}
        >
          <Text style={styles.tileEmoji}>🛡️</Text>
          <Text style={[styles.tileTitle, { color: colors.ink }]}>Minh bạch</Text>
          <Text style={[styles.tileDesc, { color: colors.inkMuted }]}>Quyền riêng tư</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.gridTile, { backgroundColor: colors.surface, borderColor: colors.border }]}
          onPress={() => router.push('/(tabs)/account')}
        >
          <Text style={styles.tileEmoji}>⚙️</Text>
          <Text style={[styles.tileTitle, { color: colors.ink }]}>Tài khoản</Text>
          <Text style={[styles.tileDesc, { color: colors.inkMuted }]}>Quản lý phiên & bảo mật</Text>
        </TouchableOpacity>
      </View>
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
  welcomeBox: {
    marginBottom: spacing.lg,
  },
  welcomeLabel: {
    ...typography.caption,
  },
  welcomeName: {
    ...typography.titleLarge,
  },
  orgText: {
    ...typography.label,
    marginTop: 2,
  },
  cardHeader: {
    ...typography.titleSmall,
    marginBottom: spacing.xs,
  },
  deviceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  deviceName: {
    ...typography.titleSmall,
  },
  deviceSpec: {
    ...typography.caption,
    marginTop: 2,
  },
  noDeviceRow: {
    paddingVertical: spacing.sm,
  },
  noDeviceNotice: {
    ...typography.bodyMedium,
    lineHeight: 20,
  },
  incidentRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  incidentCountText: {
    ...typography.caption,
    marginTop: 2,
  },
  linkRow: {
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingTop: spacing.md,
  },
  linkText: {
    ...typography.label,
  },
  sectionTitle: {
    ...typography.titleSmall,
    marginTop: spacing.md,
    marginBottom: spacing.sm,
  },
  actionGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.md,
    marginBottom: spacing.xxxl,
  },
  gridTile: {
    width: '47%',
    borderWidth: 1,
    borderRadius: radii.md,
    padding: spacing.md,
    alignItems: 'center',
  },
  tileEmoji: {
    fontSize: 28,
    marginBottom: spacing.xs,
  },
  tileTitle: {
    ...typography.label,
    marginBottom: 2,
  },
  tileDesc: {
    ...typography.caption,
    textAlign: 'center',
  },
});
