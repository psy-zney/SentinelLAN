import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useAppColors } from './common';
import { ChevronRight, ChevronUp, type LucideIcon } from 'lucide-react-native';

export function StatusPill({ label, tone = 'neutral' }: { label: string; tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info' }) {
  const colors = useAppColors();
  const foreground = tone === 'neutral' ? colors.inkMuted : colors.ink;
  const background = tone === 'neutral' ? colors.surfaceSubtle : colors[`${tone}Light`];
  return <View style={[styles.pill, { backgroundColor: background }]}>
    <Text style={{ color: foreground, fontSize: 13, lineHeight: 19, fontWeight: '600' }}>{label}</Text>
  </View>;
}

export function InfoRow({ label, value }: { label: string; value?: string | null }) {
  const colors = useAppColors();
  return <View style={[styles.infoRow, { borderBottomColor: colors.border }]}>
    <Text style={[styles.label, { color: colors.inkMuted }]}>{label}</Text>
    <Text selectable style={[styles.value, { color: colors.ink }]}>{value?.trim() || 'Chưa có thông tin'}</Text>
  </View>;
}

export function ActionRow({ title, description, onPress, expanded, icon: Icon }: {
  title: string; description: string; onPress: () => void; expanded?: boolean; icon?: LucideIcon;
}) {
  const colors = useAppColors();
  return <Pressable accessibilityRole="button" accessibilityLabel={title}
    accessibilityHint={description} accessibilityState={expanded === undefined ? undefined : { expanded }}
    onPress={onPress} style={({ pressed }) => [styles.action, { borderBottomColor: colors.border, backgroundColor: pressed ? colors.surfaceSubtle : colors.surface }]}>
    {Icon ? <View style={{ padding: 10, borderRadius: 12, backgroundColor: colors.primaryLight }}><Icon size={22} color={colors.primary} accessible={false} /></View> : null}
    <View style={{ flex: 1, gap: 4 }}>
      <Text style={{ color: colors.ink, fontWeight: '600', fontSize: 16, lineHeight: 23 }}>{title}</Text>
      <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>{description}</Text>
    </View>
    {expanded ? <ChevronUp size={20} color={colors.primary} accessible={false} /> : <ChevronRight size={20} color={colors.primary} accessible={false} />}
  </Pressable>;
}

export function ResourceMeter({ label, value }: { label: string; value: number }) {
  const colors = useAppColors();
  const valid = Number.isFinite(value) && value >= 0 && value <= 100;
  return <View style={{ gap: 9, paddingVertical: 10 }}>
    <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: 12 }}>
      <Text style={{ color: colors.inkMuted, fontSize: 16 }}>{label}</Text>
      <Text style={{ color: colors.ink, fontSize: 18, fontWeight: '700' }}>{valid ? `${value.toFixed(1)}%` : 'Chưa có số liệu'}</Text>
    </View>
    {valid ? <View accessible accessibilityRole="progressbar" accessibilityLabel={label}
      accessibilityValue={{ min: 0, max: 100, now: value }} style={{ height: 7, borderRadius: 4, backgroundColor: colors.border }}>
      <View style={{ width: `${value}%`, height: 7, borderRadius: 4, backgroundColor: value >= 90 ? colors.warning : colors.primary }} />
    </View> : null}
  </View>;
}

const styles = StyleSheet.create({
  pill: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 7, alignSelf: 'flex-start' },
  infoRow: { paddingVertical: 12, gap: 5, borderBottomWidth: StyleSheet.hairlineWidth },
  label: { fontSize: 14, lineHeight: 21 },
  value: { fontSize: 16, lineHeight: 24, fontWeight: '500' },
  action: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: 16, padding: 16, borderBottomWidth: StyleSheet.hairlineWidth },
});
