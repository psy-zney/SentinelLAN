import React, { useRef, useState } from 'react';
import { Alert } from 'react-native';
import { useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { AppButton, AppCard } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { requestKey, ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';

export function SelfServiceHomeView() {
  const { user, apiClient } = useAuth();
  const router = useRouter();
  const active = useActiveScreen();
  const device = useQuery({ queryKey: ['my-device'], queryFn: () => apiClient.myDevice(), enabled: active });
  const [error, setError] = useState<unknown>();
  const [pending, setPending] = useState(false);
  const key = useRef(requestKey());
  const panic = async () => {
    if (pending) return;
    setPending(true); setError(undefined);
    try {
      const request = await apiClient.createSupportRequest({ kind: 'Panic', category: 'Suspicious', title: 'Tôi nghi máy bị nhiễm virus', description: 'Nhân viên chủ động báo động và yêu cầu IT kiểm tra máy.', canWork: false, confirmed: true, idempotencyKey: key.current });
      key.current = requestKey();
      router.push({ pathname: '/request-detail', params: { id: request.id } });
    } catch (err) { setError(err); } finally { setPending(false); }
  };
  return <ServicePage>
    <ServiceText heading>Xin chào, {user?.displayName ?? 'bạn'}</ServiceText>
    <ServiceText>Bạn cần IT giúp gì hôm nay?</ServiceText>
    <AppButton title="Tôi gặp sự cố" onPress={() => router.push('/request-create')} style={{ minHeight: 64 }} />
    <AppButton title="Tôi cần phần mềm" onPress={() => router.push('/app-catalog')} style={{ minHeight: 64 }} />
    <AppButton title="Yêu cầu của tôi" onPress={() => router.push('/support-requests')} style={{ minHeight: 64 }} />
    <AppCard>
      <ServiceText heading>Máy của bạn</ServiceText>
      <ServiceError error={device.error} retry={() => { void device.refetch(); }} />
      <ServiceText>{device.isPending ? 'Đang tải thông tin máy…' : device.data ? `${device.data.device.name} · ${device.data.device.isOnline ? 'Đang kết nối' : 'Đang mất kết nối'}` : 'Bạn chưa được phân công máy. Liên hệ IT hoặc quét mã QR trên máy.'}</ServiceText>
      <AppButton title="Xem máy hoặc quét mã QR" variant="outline" onPress={() => router.push(device.data ? '/(tabs)/my-device' : '/(tabs)/scan')} />
    </AppCard>
    <AppCard>
      <ServiceText heading>Báo khẩn cấp</ServiceText>
      <ServiceText>Nếu nghi máy bị nhiễm virus, gửi báo động ngay cho IT. Cô lập mạng cần máy nhận lệnh và chỉ khả dụng khi IT đã bật chế độ thử nghiệm được phép.</ServiceText>
      <AppButton title="Tôi nghi máy bị nhiễm virus" variant="danger" loading={pending} disabled={!device.data} onPress={() => Alert.alert('Báo động cho IT và yêu cầu ngắt mạng', `Máy ${device.data?.device.name ?? ''} có thể mất kết nối mạng nếu cô lập được thực hiện. App sẽ hiển thị kết quả do máy xác nhận.`, [{ text: 'Hủy', style: 'cancel' }, { text: 'Xác nhận báo khẩn cấp', style: 'destructive', onPress: () => { void panic(); } }])} />
      <ServiceError error={error} />
    </AppCard>
    <AppButton title="Thông báo và sự cố chung" variant="outline" onPress={() => router.push('/employee-notifications')} />
    <AppButton title="Hướng dẫn xử lý lỗi thường gặp" variant="outline" onPress={() => router.push('/self-service-help')} />
    <AppButton title="Chọn giờ IT hỗ trợ" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'Appointment' } })} />
    <AppButton title="Yêu cầu tạm dừng SentinelLAN" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'PauseAgent' } })} />
    <AppButton title="Yêu cầu gỡ SentinelLAN" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'UninstallAgent' } })} />
  </ServicePage>;
}
