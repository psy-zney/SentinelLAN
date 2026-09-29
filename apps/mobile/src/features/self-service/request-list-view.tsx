import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppButton, AppCard } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { statuses, ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';

export function RequestListView() {
  const { apiClient, user } = useAuth();
  const employee = user?.role === 'Employee';
  const active = useActiveScreen();
  const router = useRouter();
  const requests = useQuery({ queryKey: ['support-requests'], queryFn: () => apiClient.supportRequests(), enabled: active, refetchInterval: active ? 5000 : false, refetchIntervalInBackground: false });
  return <ServicePage>
    <ServiceText heading>{employee ? 'Yêu cầu của tôi' : 'Hàng đợi hỗ trợ công ty'}</ServiceText>
    {employee && <AppButton title="Gửi yêu cầu mới" onPress={() => router.push('/request-create')} />}
    <ServiceError error={requests.error} retry={() => { void requests.refetch(); }} />
    {requests.isPending ? <ServiceText>Đang tải…</ServiceText> : requests.data?.length === 0 ? <ServiceText>Bạn chưa có yêu cầu nào.</ServiceText> : null}
    {requests.data?.map(request => <AppCard key={request.id}>
      <ServiceText heading>{request.title}</ServiceText>
      <ServiceText>{statuses[request.status]}</ServiceText>
      <ServiceText>{request.assignedTechnicianName ? `Người phụ trách: ${request.assignedTechnicianName}` : 'Đang chờ IT phân công người phụ trách'}</ServiceText>
      <ServiceText>{request.deviceName} · {new Date(request.createdAt).toLocaleString('vi-VN')}</ServiceText>
      <AppButton title={employee ? "Xem tiến độ và nhắn IT" : "Xử lý và trả lời"} variant="outline" onPress={() => router.push({ pathname: '/request-detail', params: { id: request.id } })} />
    </AppCard>)}
    {employee && <AppButton title="Xem báo cáo sự cố trước đây" variant="outline" onPress={() => router.push('/(tabs)/incidents')} />}
  </ServicePage>;
}
