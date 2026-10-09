import React, { useState } from 'react';
import { Alert } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppButton, AppCard, AppInput } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, ServiceError, useActiveScreen } from '../self-service/shared';

export function OperatorView() {
  const {user,apiClient} = useAuth(); const active = useActiveScreen(); const router = useRouter();
  const operator = user?.role === 'Admin';
  const dashboard = useQuery({queryKey:['operator-dashboard'],queryFn:()=>apiClient.operatorDashboard(),enabled:active && operator,refetchInterval:active ? 15000 : false});
  const devices = useQuery({queryKey:['operator-devices'],queryFn:()=>apiClient.operatorDevices(),enabled:active && operator,refetchInterval:active ? 15000 : false});
  const [reason,setReason] = useState(''); const [token,setToken] = useState(''); const [busy,setBusy] = useState(false); const [error,setError] = useState<unknown>();
  if (!operator) return <ServicePage><ServiceText>Bạn không có quyền quản trị công ty.</ServiceText></ServicePage>;
  return <ServicePage><ServiceText heading>{user?.role === 'Admin' ? 'Quản trị công ty' : 'Công việc IT'}</ServiceText>
    <ServiceError error={dashboard.error || devices.error || error} retry={()=>{void dashboard.refetch();void devices.refetch();}} />
    {dashboard.data ? <AppCard><ServiceText>{dashboard.data.onlineDevices}/{dashboard.data.totalDevices} máy đang kết nối</ServiceText><ServiceText>{dashboard.data.openAlerts} cảnh báo đang mở</ServiceText></AppCard> : <ServiceText>Đang tải tình trạng công ty…</ServiceText>}
    <AppButton title="Xử lý yêu cầu từ nhân viên" onPress={()=>router.push('/support-requests')} />
    {user?.role === 'Admin' && <><AppButton title="Quản lý tài khoản công ty" onPress={()=>router.push('/company-users')} /><AppCard><ServiceText heading>Đăng ký Agent mới</ServiceText><AppInput label="Lý do cấp mã" value={reason} onChangeText={setReason}/><AppButton title="Cấp mã dùng một lần, hạn 15 phút" disabled={reason.trim().length < 3 || busy} onPress={()=>Alert.alert('Cấp mã đăng ký','Mã cho phép đăng ký một máy vào công ty này.',[{text:'Hủy',style:'cancel'},{text:'Xác nhận',onPress:()=>{setBusy(true);setError(undefined);void apiClient.issueEnrollment(reason).then(result=>setToken(`${result.token}\nHết hạn: ${new Date(result.expiresAt).toLocaleString('vi-VN')}`)).catch(setError).finally(()=>setBusy(false));}}])}/>{token && <><ServiceText>{token}</ServiceText><AppButton title="Đã lưu mã — đóng" onPress={()=>setToken('')}/></>}</AppCard></>}
    <ServiceText heading>Thiết bị công ty</ServiceText>{devices.data?.length === 0 && <ServiceText>Chưa có Agent đăng ký.</ServiceText>}{devices.data?.map(device=><AppCard key={device.id}><ServiceText heading>{device.name}</ServiceText><ServiceText>{device.osVersion} · Agent {device.agentVersion}</ServiceText><ServiceText>{device.isRevoked ? 'Đã thu hồi' : device.lastSeenAt ? `Kết nối gần nhất: ${new Date(device.lastSeenAt).toLocaleString('vi-VN')}` : 'Chưa kết nối'}</ServiceText></AppCard>)}
  </ServicePage>;
}
