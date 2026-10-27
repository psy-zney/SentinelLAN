import React, { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Headset, Server, UsersRound } from 'lucide-react-native';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppButton, AppCard, AppInput, useAppColors } from '../../components/common';
import { ActionRow, ResourceMeter, StatusPill } from '../../components/management';
import { connectionStatus, displayDate, displayDeviceName, isSampleDevice } from '../../lib/device-presentation';
import { ApiError } from '../../lib/api/client';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServicePages, ServiceText, ServiceError, useActiveScreen } from '../self-service/shared';

export function OperatorOverview({ scope }: { scope: string }) {
  const { apiClient } = useAuth();
  const colors = useAppColors();
  const active = useActiveScreen();
  const router = useRouter();
  const [offset, setOffset] = useState(0);
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [status, setStatus] = useState<'all' | 'online' | 'offline'>('all');
  const [enrolling, setEnrolling] = useState(false);
  const [reason, setReason] = useState('');
  const [token, setToken] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();

  useEffect(() => {
    const timer = setTimeout(() => { setAppliedSearch(search.trim()); setOffset(0); }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  const dashboard = useQuery({
    queryKey: ['operator-dashboard', scope], queryFn: () => apiClient.operatorDashboard(),
    enabled: active, staleTime: 0, gcTime: 0, refetchInterval: active ? 15000 : false,
  });
  const devices = useQuery({
    queryKey: ['operator-devices', scope, offset, appliedSearch, status],
    queryFn: ({ signal }) => apiClient.operatorDevices(offset, 100, { search: appliedSearch, status }, signal),
    enabled: active, staleTime: 0, gcTime: 0, refetchInterval: active ? 15000 : false,
  });
  const denied = [dashboard.error, devices.error].some(error => error instanceof ApiError && [401, 403].includes(error.status));
  const summary = denied ? undefined : dashboard.data;
  const rows = denied ? undefined : devices.data;
  const sampleCount = rows?.filter(device => isSampleDevice(device)).length ?? 0;

  const issueCode = () => Alert.alert('Cấp mã đăng ký', 'Mã cho phép đăng ký một máy vào công ty này.', [
    { text: 'Hủy', style: 'cancel' },
    { text: 'Xác nhận', onPress: () => {
      setBusy(true); setError(undefined);
      void apiClient.issueEnrollment(reason)
        .then(result => setToken(`${result.token}\nHết hạn: ${displayDate(result.expiresAt)}`))
        .catch(setError).finally(() => setBusy(false));
    } },
  ]);

  return <ServicePage onRefresh={() => { void dashboard.refetch(); void devices.refetch(); }} refreshing={dashboard.isRefetching || devices.isRefetching}>
    <View style={{ gap: 6, paddingVertical: 8 }}>
      <Text accessibilityRole="header" style={{ color: colors.ink, fontSize: 28, lineHeight: 36, fontWeight: '700' }}>Quản trị công ty</Text>
      <Text style={{ color: colors.inkMuted, fontSize: 16, lineHeight: 24 }}>Theo dõi máy tính và xử lý công việc IT.</Text>
    </View>
    <ServiceError error={dashboard.error || devices.error || error} retry={() => { void dashboard.refetch(); void devices.refetch(); }} />
    {summary ? <AppCard>
      <View style={styles.summary}>
        {[['Tổng số máy', summary.totalDevices], ['Đang kết nối', summary.onlineDevices], ['Cảnh báo mở', summary.openAlerts]].map(([label, value]) =>
          <View key={label} style={{ flex: 1, minWidth: 82, gap: 4 }}>
            <Text style={{ color: colors.ink, fontSize: 28, lineHeight: 36, fontWeight: '700' }}>{value}</Text>
            <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>{label}</Text>
          </View>)}
      </View>
      <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21, marginTop: 12 }}>{summary.onlineDevices}/{summary.totalDevices} máy đang kết nối</Text>
      {summary.totalDevices > 0 ? <ResourceMeter label="Tỷ lệ máy đang kết nối" value={summary.onlineDevices / summary.totalDevices * 100} /> : null}
    </AppCard> : dashboard.isPending ? <ServiceText>Đang tải tình trạng công ty…</ServiceText> : null}
    <View style={[styles.group, { borderColor: colors.border }]}>
      <ActionRow icon={Headset} title="Xử lý yêu cầu từ nhân viên" description="Xem hàng đợi và tiến độ hỗ trợ" onPress={() => router.push('/support-requests')} />
      <ActionRow icon={Server} title="Theo dõi VPS" description="CPU, bộ nhớ và dịch vụ máy chủ" onPress={() => router.push('/vps-status')} />
      <ActionRow icon={UsersRound} title="Quản lý tài khoản công ty" description="Danh sách và trạng thái tài khoản" onPress={() => router.push('/company-users')} />
    </View>
    <View style={{ gap: 6, marginTop: 12 }}>
      <ServiceText heading>Thiết bị công ty</ServiceText>
      <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>Chọn một máy để xem cấu hình, người sử dụng và số liệu.</Text>
    </View>
    <AppInput label="Tìm máy" placeholder="Tên máy hoặc hệ điều hành" value={search} onChangeText={setSearch} maxLength={100} autoCapitalize="none" />
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
      {([['all', 'Tất cả'], ['online', 'Đang kết nối'], ['offline', 'Ngoại tuyến']] as const).map(([value, label]) =>
        <Pressable key={value} accessibilityRole="button" accessibilityLabel={`Lọc: ${label}`} accessibilityState={{ selected: status === value }}
          onPress={() => { setStatus(value); setOffset(0); }}
          style={[styles.filter, { backgroundColor: status === value ? colors.primary : colors.surface, borderColor: status === value ? colors.primary : colors.border }]}>
          <Text style={{ color: status === value ? colors.inkInverse : colors.inkMuted, fontSize: 14, fontWeight: '600' }}>{label}</Text>
        </Pressable>)}
    </View>
    {sampleCount > 0 ? <View style={[styles.sampleNote, { backgroundColor: colors.infoLight }]}>
      <Text style={{ color: colors.info, fontSize: 14, lineHeight: 22 }}>Các máy có nhãn “Dữ liệu mẫu” dùng để minh họa, chưa có Agent thật kết nối.</Text>
    </View> : null}
    {devices.isPending ? <ServiceText>Đang tải danh sách máy…</ServiceText> : null}
    {rows?.length === 0 ? <AppCard>
      <ServiceText>{appliedSearch || status !== 'all' ? 'Không tìm thấy máy phù hợp. Thử đổi từ khóa hoặc bộ lọc.' : 'Chưa có Agent đăng ký.'}</ServiceText>
    </AppCard> : null}
    {rows?.length ? <View style={[styles.group, { borderColor: colors.border }]}>
      {rows.map(device => {
        const sample = isSampleDevice(device);
        const name = displayDeviceName(device.name);
        const connection = connectionStatus(device);
        return <Pressable key={device.id} accessibilityRole="button" accessibilityLabel={`Xem chi tiết máy ${name}`}
          accessibilityHint={sample ? 'Mở cấu hình và số liệu minh họa của máy' : 'Mở thông tin và số liệu của máy'}
          onPress={() => router.push({ pathname: '/device-detail', params: { deviceId: device.id } })}
          style={({ pressed }) => [styles.deviceRow, { backgroundColor: pressed ? colors.surfaceSubtle : colors.surface, borderBottomColor: colors.border }]}>
          <View style={{ flex: 1, gap: 10 }}>
            <Text style={{ color: colors.ink, fontSize: 19, lineHeight: 27, fontWeight: '700' }}>{name}</Text>
            {sample ? <StatusPill label="Dữ liệu mẫu" tone="info" /> : <StatusPill label={connection.label} tone={connection.tone} />}
            <Text style={{ color: colors.inkMuted, fontSize: 15, lineHeight: 23 }}>{device.osVersion || 'Chưa có hệ điều hành'}</Text>
            <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>{sample ? 'Cấu hình minh họa, chưa có kết nối thật' : device.lastSeenAt ? `Liên lạc: ${displayDate(device.lastSeenAt)}` : 'Chưa ghi nhận kết nối'}</Text>
            <Text style={{ color: colors.primary, fontSize: 14, fontWeight: '600' }}>Xem chi tiết</Text>
          </View>
          <Text importantForAccessibility="no" accessibilityElementsHidden style={{ color: colors.primary, fontSize: 28 }}>›</Text>
        </Pressable>;
      })}
    </View> : null}
    {(rows?.length ?? 0) > 0 || offset > 0 ? <ServicePages offset={offset} count={rows?.length ?? 0} onChange={setOffset} /> : null}
    <View style={[styles.group, { borderColor: colors.border, marginTop: 12 }]}>
      <ActionRow title="Đăng ký Agent mới" description="Cấp mã khi cần cài thêm máy" expanded={enrolling}
        onPress={() => { setEnrolling(value => !value); setToken(''); }} />
    </View>
    {enrolling ? <AppCard>
      <ServiceText>Mã dùng cho một máy và hết hạn sau 15 phút.</ServiceText>
      <AppInput label="Lý do cấp mã" value={reason} onChangeText={setReason} />
      <AppButton title="Cấp mã dùng một lần, hạn 15 phút" disabled={reason.trim().length < 3 || busy} loading={busy} onPress={issueCode} />
      {token ? <><Text selectable style={{ color: colors.ink, fontSize: 16, lineHeight: 26 }}>{token}</Text><AppButton title="Đã lưu mã — đóng" onPress={() => setToken('')} /></> : null}
    </AppCard> : null}
  </ServicePage>;
}

const styles = StyleSheet.create({
  summary: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  group: { borderWidth: 1, borderRadius: 14, overflow: 'hidden' },
  deviceRow: { flexDirection: 'row', alignItems: 'center', gap: 16, padding: 18, borderBottomWidth: StyleSheet.hairlineWidth },
  filter: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 14, paddingVertical: 10, borderRadius: 10, borderWidth: 1 },
  sampleNote: { padding: 14, borderRadius: 10 },
});
