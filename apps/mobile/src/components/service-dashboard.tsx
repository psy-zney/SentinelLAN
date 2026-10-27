import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { ArrowUpRight, ChartNoAxesCombined, type LucideIcon } from 'lucide-react-native';
import { AppCard, useAppColors } from './common';
import type { SupportRequest } from '../lib/validation/self-service';

export function ServiceShortcut({ title, description, icon: Icon, onPress }: {
  title: string; description: string; icon: LucideIcon; onPress: () => void;
}) {
  const colors = useAppColors();
  return <Pressable accessibilityRole="button" accessibilityLabel={title} accessibilityHint={description}
    onPress={onPress} style={({ pressed }) => [styles.shortcut, { borderColor: colors.border, backgroundColor: pressed ? colors.primaryLight : colors.surface }]}>
    <View style={styles.row}>
      <View style={[styles.icon, { backgroundColor: colors.primaryLight }]}><Icon size={23} color={colors.primary} accessible={false} /></View>
      <ArrowUpRight size={18} color={colors.inkMuted} accessible={false} />
    </View>
    <Text style={[styles.title, { color: colors.ink }]}>{title}</Text>
    <Text style={[styles.description, { color: colors.inkMuted }]}>{description}</Text>
  </Pressable>;
}

export function RequestSummary({ requests, caption = 'Tối đa 100 yêu cầu gần nhất' }: { requests: SupportRequest[]; caption?: string }) {
  const colors = useAppColors();
  const groups = [
    { label: 'Đang xử lý', count: requests.filter(item => ['Open', 'InProgress', 'Approved'].includes(item.status)).length, color: colors.info },
    { label: 'Chờ bạn', count: requests.filter(item => item.status === 'AwaitingEmployee').length, color: colors.warning },
    { label: 'Đã xử lý', count: requests.filter(item => ['Resolved', 'Closed'].includes(item.status)).length, color: colors.success },
    { label: 'Chưa duyệt', count: requests.filter(item => item.status === 'Rejected').length, color: colors.inkMuted },
  ];
  return <AppCard style={{ gap: 14, marginBottom: 0 }}>
    <View style={[styles.row, { justifyContent: 'flex-start' }]}>
      <ChartNoAxesCombined size={22} color={colors.primary} accessible={false} />
      <Text accessibilityRole="header" style={[styles.title, { color: colors.ink }]}>Tiến độ hỗ trợ</Text>
    </View>
    <Text style={[styles.description, { color: colors.inkMuted }]}>{caption}</Text>
    {requests.length > 0 ? <View accessible accessibilityLabel={groups.map(group => `${group.label}: ${group.count}`).join(', ')}
      style={[styles.chart, { backgroundColor: colors.surfaceSubtle }]}>
      {groups.filter(group => group.count > 0).map(group => <View key={group.label} style={{ flex: group.count, backgroundColor: group.color }} />)}
    </View> : <Text style={[styles.description, { color: colors.inkMuted }]}>Yêu cầu mới sẽ xuất hiện tại đây sau khi gửi.</Text>}
    <View style={styles.metrics}>
      {groups.map(group => <View key={group.label} style={[styles.metric, { backgroundColor: colors.surfaceSubtle }]}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          <View style={{ width: 7, height: 7, borderRadius: 4, backgroundColor: group.color }} />
          <Text style={[styles.description, { color: colors.inkMuted }]}>{group.label}</Text>
        </View>
        <Text style={{ color: colors.ink, fontSize: 26, fontWeight: '700' }}>{group.count}</Text>
      </View>)}
    </View>
  </AppCard>;
}

const styles = StyleSheet.create({
  shortcut: { flexGrow: 1, flexBasis: 145, minWidth: 140, borderWidth: 1, borderRadius: 18, padding: 16, gap: 10 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10 },
  icon: { width: 44, height: 44, borderRadius: 13, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 17, lineHeight: 24, fontWeight: '700' },
  description: { fontSize: 13, lineHeight: 20 },
  chart: { flexDirection: 'row', height: 12, borderRadius: 6, overflow: 'hidden', gap: 3 },
  metrics: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  metric: { flexBasis: 125, flexGrow: 1, borderRadius: 12, padding: 12, gap: 5 },
});
