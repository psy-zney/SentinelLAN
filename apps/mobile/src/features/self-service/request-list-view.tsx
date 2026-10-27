import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { Clock3, Monitor, UserRound } from 'lucide-react-native';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppBadge, AppButton, AppCard, useAppColors } from '../../components/common';
import { RequestSummary } from '../../components/service-dashboard';
import { useAuth } from '../auth/auth-context';
import { statuses, ServicePage, ServicePages, ServiceText, ServiceError, useActiveScreen } from './shared';

export function RequestListView() {
  const { apiClient, user } = useAuth();
  const employee = user?.role === 'Employee';
  const active = useActiveScreen();
  const router = useRouter();
  const colors = useAppColors();
  const [offset, setOffset] = useState(0);
  const requests = useQuery({ queryKey: ['support-requests', offset], queryFn: () => apiClient.supportRequests(offset), enabled: active, staleTime: 0, refetchInterval: active ? 5000 : false, refetchIntervalInBackground: false });
  return <ServicePage onRefresh={() => { void requests.refetch(); }} refreshing={requests.isRefetching}>
    <ServiceText heading>{employee ? 'Yêu cầu của tôi' : 'Hàng đợi hỗ trợ công ty'}</ServiceText>
    <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>Tự cập nhật mỗi 5 giây khi mở màn hình. Kéo xuống để làm mới.</Text>
    {employee && <AppButton title="Gửi yêu cầu mới" onPress={() => router.push('/request-create')} />}
    <ServiceError error={requests.error} retry={() => { void requests.refetch(); }} />
    {requests.isPending ? <ServiceText>Đang tải…</ServiceText> : requests.data?.length === 0 ? <ServiceText>Bạn chưa có yêu cầu nào.</ServiceText> : null}
    {requests.data && requests.data.length > 0 ? <RequestSummary requests={requests.data} caption={`Thống kê ${requests.data.length} yêu cầu trong trang ${Math.floor(offset / 100) + 1}`} /> : null}
    {requests.data?.map(request => <AppCard key={request.id} style={{ gap: 12, marginBottom: 0 }}>
      <AppBadge text={statuses[request.status]} variant={['Resolved', 'Closed'].includes(request.status) ? 'success' : request.status === 'Rejected' ? 'danger' : request.status === 'AwaitingEmployee' ? 'warning' : 'info'} />
      <ServiceText heading>{request.title}</ServiceText>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}><UserRound size={17} color={colors.inkMuted} accessible={false} /><Text style={{ flex: 1, color: colors.inkMuted, fontSize: 14, lineHeight: 21 }}>{request.assignedTechnicianName ? `Người phụ trách: ${request.assignedTechnicianName}` : 'Đang chờ IT phân công người phụ trách'}</Text></View>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}><Monitor size={17} color={colors.inkMuted} accessible={false} /><Text style={{ flex: 1, color: colors.inkMuted, fontSize: 14 }}>{request.deviceName}</Text></View>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}><Clock3 size={17} color={colors.inkMuted} accessible={false} /><Text style={{ flex: 1, color: colors.inkMuted, fontSize: 14 }}>{new Date(request.createdAt).toLocaleString('vi-VN')}</Text></View>
      {!request.canWork && !['Resolved', 'Closed', 'Rejected'].includes(request.status) ? <AppBadge text="Công việc đang gián đoạn" variant="danger" /> : null}
      <AppButton title={employee ? "Xem tiến độ và nhắn IT" : "Xử lý và trả lời"} variant="outline" onPress={() => router.push({ pathname: '/request-detail', params: { id: request.id } })} />
    </AppCard>)}
    <ServicePages offset={offset} count={requests.data?.length ?? 0} onChange={setOffset} />
    {employee && <AppButton title="Xem báo cáo sự cố trước đây" variant="outline" onPress={() => router.push('/incident-history')} />}
  </ServicePage>;
}
