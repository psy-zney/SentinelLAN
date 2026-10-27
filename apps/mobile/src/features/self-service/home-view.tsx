import React, { useRef, useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { CalendarDays, Download, Headset, ListChecks, Monitor, ShieldAlert, Bell, BookOpen } from 'lucide-react-native';
import { useRouter } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AppButton, AppCard, useAppColors } from '../../components/common';
import { ActionRow } from '../../components/management';
import { RequestSummary, ServiceShortcut } from '../../components/service-dashboard';
import { cacheCreatedRequest } from './request-cache';
import { useAuth } from '../auth/auth-context';
import { requestKey, ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';

export function SelfServiceHomeView() {
  const { user, apiClient } = useAuth();
  const router = useRouter();
  const colors = useAppColors();
  const queryClient = useQueryClient();
  const active = useActiveScreen();
  const device = useQuery({ queryKey: ['my-device'], queryFn: () => apiClient.myDevice(), enabled: active, staleTime: 0, refetchInterval: active ? 15000 : false });
  const requests = useQuery({ queryKey: ['support-requests', 0], queryFn: () => apiClient.supportRequests(0), enabled: active, staleTime: 0, refetchInterval: active ? 5000 : false, refetchIntervalInBackground: false });
  const [error, setError] = useState<unknown>();
  const [pending, setPending] = useState(false);
  const key = useRef(requestKey());
  const panic = async () => {
    if (pending) return;
    setPending(true); setError(undefined);
    try {
      const request = await apiClient.createSupportRequest({ kind: 'Panic', category: 'Suspicious', title: 'Tôi nghi máy bị nhiễm virus', description: 'Nhân viên chủ động báo động và yêu cầu IT kiểm tra máy.', canWork: false, confirmed: true, idempotencyKey: key.current });
      key.current = requestKey();
      await cacheCreatedRequest(queryClient, request);
      router.push({ pathname: '/request-detail', params: { id: request.id } });
    } catch (err) { setError(err); } finally { setPending(false); }
  };
  return <ServicePage onRefresh={() => { void device.refetch(); void requests.refetch(); }} refreshing={device.isRefetching || requests.isRefetching}>
    <View style={{ paddingVertical: 10, gap: 6 }}>
      <Text style={{ color: colors.primary, fontSize: 12, fontWeight: '700', letterSpacing: 1.5 }}>SENTINELLAN · HỖ TRỢ IT</Text>
      <Text accessibilityRole="header" style={{ color: colors.ink, fontSize: 28, lineHeight: 36, fontWeight: '700' }}>Xin chào, {user?.displayName ?? 'bạn'}</Text>
      <ServiceText>Bạn cần IT giúp gì hôm nay?</ServiceText>
    </View>
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
      <ServiceShortcut title="Tôi gặp sự cố" description="Gửi báo cáo và nhận hỗ trợ" icon={Headset} onPress={() => router.push('/request-create')} />
      <ServiceShortcut title="Tôi cần phần mềm" description="Ứng dụng công ty cho phép" icon={Download} onPress={() => router.push('/app-catalog')} />
      <ServiceShortcut title="Yêu cầu của tôi" description="Theo dõi tiến độ, nhắn với IT" icon={ListChecks} onPress={() => router.push('/support-requests')} />
    </View>
    <ServiceError error={requests.error} retry={() => { void requests.refetch(); }} />
    {requests.data ? <RequestSummary requests={requests.data} /> : requests.isPending ? <ServiceText>Đang tải tiến độ hỗ trợ…</ServiceText> : null}
    <AppCard style={{ gap: 12, marginBottom: 0 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}><Monitor size={22} color={colors.primary} accessible={false} /><ServiceText heading>Máy của bạn</ServiceText></View>
      <ServiceError error={device.error} retry={() => { void device.refetch(); }} />
      <ServiceText>{device.isPending ? 'Đang tải thông tin máy…' : device.data ? `${device.data.device.name} · ${device.data.device.isOnline ? 'Đang kết nối' : 'Đang mất kết nối'}` : 'Bạn chưa được phân công máy. Liên hệ IT hoặc quét mã QR trên máy.'}</ServiceText>
      <AppButton title="Xem máy hoặc quét mã QR" variant="outline" onPress={() => router.push(device.data ? '/(tabs)/my-device' : '/(tabs)/scan')} />
    </AppCard>
    <View style={{ borderWidth: 1, borderColor: colors.border, borderRadius: 16, overflow: 'hidden' }}>
      <ActionRow icon={Bell} title="Thông báo và sự cố chung" description="Thông tin mới từ IT và công ty" onPress={() => router.push('/employee-notifications')} />
      <ActionRow icon={BookOpen} title="Hướng dẫn xử lý lỗi thường gặp" description="Các bước đơn giản có thể tự làm" onPress={() => router.push('/self-service-help')} />
      <ActionRow icon={CalendarDays} title="Chọn giờ IT hỗ trợ" description="Hẹn thời gian phù hợp với công việc" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'Appointment' } })} />
    </View>
    <AppCard style={{ gap: 12, marginBottom: 0, borderColor: colors.danger }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}><ShieldAlert size={22} color={colors.danger} accessible={false} /><ServiceText heading>Báo khẩn cấp</ServiceText></View>
      <ServiceText>Nếu nghi máy bị nhiễm virus, gửi báo động ngay cho IT. Cô lập mạng cần máy nhận lệnh và chỉ khả dụng khi IT đã bật chế độ thử nghiệm được phép.</ServiceText>
      <AppButton title="Tôi nghi máy bị nhiễm virus" variant="danger" loading={pending} disabled={!device.data} onPress={() => Alert.alert('Báo động cho IT và yêu cầu ngắt mạng', `Máy ${device.data?.device.name ?? ''} có thể mất kết nối mạng nếu cô lập được thực hiện. App sẽ hiển thị kết quả do máy xác nhận.`, [{ text: 'Hủy', style: 'cancel' }, { text: 'Xác nhận báo khẩn cấp', style: 'destructive', onPress: () => { void panic(); } }])} />
      <ServiceError error={error} />
    </AppCard>
    <AppCard style={{ gap: 12, marginBottom: 0 }}>
      <ServiceText heading>Tùy chọn SentinelLAN</ServiceText>
      <AppButton title="Yêu cầu tạm dừng SentinelLAN" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'PauseAgent' } })} />
      <AppButton title="Yêu cầu gỡ SentinelLAN" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'UninstallAgent' } })} />
    </AppCard>
  </ServicePage>;
}
