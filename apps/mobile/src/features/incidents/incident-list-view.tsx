import React from 'react';
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useAuth } from '../auth/auth-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppCard, AppBadge } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';
import { IncidentDto } from '../../lib/validation/schemas';

export function IncidentListView() {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient } = useAuth();

  const {
    data: incidents,
    isLoading,
    isError,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['my-device-incidents'],
    queryFn: () => apiClient.myDeviceIncidents(),
  });

  const getSeverityVariant = (severity: string): 'info' | 'warning' | 'danger' => {
    switch (severity.toLowerCase()) {
      case 'critical':
      case 'high':
        return 'danger';
      case 'medium':
        return 'warning';
      default:
        return 'info';
    }
  };

  const getStatusVariant = (status: string): 'info' | 'warning' | 'success' | 'danger' => {
    switch (status.toLowerCase()) {
      case 'resolved':
      case 'closed':
        return 'success';
      case 'inprogress':
      case 'in-progress':
        return 'info';
      default:
        return 'warning';
    }
  };

  const renderItem = ({ item }: { item: IncidentDto }) => (
    <TouchableOpacity
      accessibilityRole="button"
      accessibilityLabel={`Sự cố: ${item.title}`}
      activeOpacity={0.8}
      onPress={() =>
        router.push({
          pathname: '/incident-detail',
          params: { incidentJson: JSON.stringify(item) },
        })
      }
    >
      <AppCard style={styles.incidentCard}>
        <View style={styles.cardHeader}>
          <Text style={[styles.incidentTitle, { color: colors.ink }]} numberOfLines={2}>
            {item.title}
          </Text>
          <AppBadge text={item.severity} variant={getSeverityVariant(item.severity)} />
        </View>

        <Text style={[styles.deviceName, { color: colors.inkMuted }]}>{item.deviceName}</Text>

        {item.description ? (
          <Text style={[styles.description, { color: colors.inkMuted }]} numberOfLines={2}>
            {item.description}
          </Text>
        ) : null}

        <View style={styles.cardFooter}>
          <Text style={[styles.dateText, { color: colors.inkSubtle }]}>
            {new Date(item.createdAt).toLocaleDateString()}
          </Text>
          <AppBadge text={item.status} variant={getStatusVariant(item.status)} />
        </View>
      </AppCard>
    </TouchableOpacity>
  );

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <View style={styles.headerBar}>
        <Text style={[styles.screenTitle, { color: colors.ink }]}>{t('incidentsTitle')}</Text>
        <AppButton
          title={`+ ${t('reportIncidentButton')}`}
          onPress={() => router.push('/incident-create')}
          style={styles.createButton}
        />
      </View>

      {isLoading ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={[styles.statusText, { color: colors.inkMuted }]}>{t('loading')}</Text>
        </View>
      ) : isError ? (
        <View style={styles.centered}>
          <Text style={[styles.statusText, { color: colors.danger }]}>
            {error instanceof Error ? error.message : t('error')}
          </Text>
          <AppButton title={t('retry')} onPress={() => refetch()} style={{ marginTop: spacing.md }} />
        </View>
      ) : (
        <FlatList
          data={incidents ?? []}
          keyExtractor={(item) => item.id}
          renderItem={renderItem}
          contentContainerStyle={styles.listContent}
          refreshControl={
            <RefreshControl refreshing={isRefetching} onRefresh={() => { void refetch(); }} />
          }
          ListEmptyComponent={
            <View style={styles.emptyContainer}>
              <Text style={styles.emptyEmoji}>📋</Text>
              <Text style={[styles.emptyText, { color: colors.inkMuted }]}>{t('noIncidents')}</Text>
            </View>
          }
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  headerBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  screenTitle: {
    ...typography.titleMedium,
  },
  createButton: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
  },
  listContent: {
    padding: spacing.lg,
  },
  incidentCard: {
    marginBottom: spacing.md,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: spacing.xs,
  },
  incidentTitle: {
    flex: 1,
    ...typography.titleSmall,
    marginRight: spacing.sm,
  },
  deviceName: {
    ...typography.caption,
    marginBottom: spacing.xs,
  },
  description: {
    ...typography.bodyMedium,
    marginBottom: spacing.sm,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: spacing.xs,
  },
  dateText: {
    ...typography.caption,
  },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.xl,
  },
  statusText: {
    ...typography.bodyMedium,
    marginTop: spacing.md,
  },
  emptyContainer: {
    alignItems: 'center',
    paddingVertical: spacing.xxxl,
  },
  emptyEmoji: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  emptyText: {
    ...typography.bodyMedium,
    textAlign: 'center',
  },
});
