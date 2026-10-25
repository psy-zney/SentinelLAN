import React, { useEffect, useMemo, useState } from 'react';
import { Text, View } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { AppButton, AppCard, useAppColors } from '../../components/common';
import { InfoRow, ResourceMeter, StatusPill } from '../../components/management';
import { ApiError } from '../../lib/api/client';
import { connectionStatus, displayDate, displayDeviceName, isSampleDevice, readDeviceSpecifications } from '../../lib/device-presentation';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, useActiveScreen } from '../self-service/shared';

function blocked(error: unknown) {
  return error instanceof ApiError && [401, 403, 404].includes(error.status);
}

export function DeviceDetailView() {
  const { user, status } = useAuth();
  const { deviceId } = useLocalSearchParams<{ deviceId?: string | string[] }>();
  const router = useRouter();
  if (status !== 'authenticated') return <ServicePage>{null}</ServicePage>;
  if (user?.role !== 'Admin') return <ServicePage><ServiceText>Chỉ quản trị viên được xem chi tiết máy công ty.</ServiceText></ServicePage>;
  const parsed = z.string().uuid().safeParse(deviceId);
  if (!parsed.success) return <ServicePage>
    <ServiceText>Liên kết máy không hợp lệ. Vui lòng chọn lại máy từ danh sách.</ServiceText>
    <AppButton title="Quay lại danh sách máy" onPress={() => router.replace('/(tabs)')} />
  </ServicePage>;
  const scope = `${user.organizationId}:${user.id}:${parsed.data}`;
  return <DeviceDetails key={scope} id={parsed.data} scope={scope} />;
}

function DeviceDetails({ id, scope }: { id: string; scope: string }) {
  const { apiClient } = useAuth();
  const colors = useAppColors();
  const active = useActiveScreen();
  const queryClient = useQueryClient();
  const queryKey = useMemo(() => ['operator-device-detail', scope], [scope]);
  const [now, setNow] = useState(Date.now);
  const profile = useQuery({
    queryKey: [...queryKey, 'profile'], queryFn: ({ signal }) => apiClient.operatorDeviceDetail(id, signal),
    enabled: active, retry: false, gcTime: 0, refetchInterval: active ? 30000 : false,
  });
  const telemetry = useQuery({
    queryKey: [...queryKey, 'telemetry'], queryFn: ({ signal }) => apiClient.operatorDeviceTelemetry(id, signal),
    enabled: active, retry: false, gcTime: 0, refetchInterval: active ? 30000 : false,
  });

  useEffect(() => {
    if (!active) { void queryClient.cancelQueries({ queryKey }); return; }
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), 30000);
    return () => clearInterval(timer);
  }, [active, queryClient, queryKey]);
  useEffect(() => () => { void queryClient.cancelQueries({ queryKey }); }, [queryClient, queryKey]);

  const denied = blocked(profile.error) || blocked(telemetry.error);
  const device = denied ? undefined : profile.data;
  const latest = denied ? undefined : telemetry.data?.[0];
  const specifications = readDeviceSpecifications(device?.specificationsJson ?? null);
  const sample = device ? isSampleDevice(device, specifications.data?.sampleData) : false;
  const stamp = latest?.collectedAt ?? latest?.createdAt;
  const age = stamp ? now - Date.parse(stamp) : NaN;
  const stale = !Number.isFinite(age) || age > 180000 || age < -60000 || telemetry.isError;
  const connection = device ? connectionStatus(device) : null;
  const refresh = () => { void profile.refetch(); void telemetry.refetch(); };
  const notice = { color: colors.danger, fontSize: 16, lineHeight: 24 };

  return <ServicePage>
    <View style={{ gap: 10, paddingVertical: 8 }}>
      <Text style={{ color: colors.inkMuted, fontSize: 14 }}>Thiết bị công ty</Text>
      <Text accessibilityRole="header" style={{ color: colors.ink, fontSize: 28, fontWeight: '700', lineHeight: 36 }}>{device ? displayDeviceName(device.name) : 'Chi tiết máy'}</Text>
      {device && connection ? <StatusPill label={sample ? 'Dữ liệu mẫu' : connection.label} tone={sample ? 'info' : connection.tone} /> : null}
    </View>
    <AppButton title="Làm mới thông tin máy" variant="outline" loading={profile.isFetching || telemetry.isFetching} disabled={!active} onPress={refresh} />
    {denied ? <Text accessibilityRole="alert" style={notice}>Không tìm thấy máy hoặc tài khoản không có quyền xem. Hãy quay lại danh sách máy.</Text> : null}
    {profile.isPending && !denied ? <ServiceText>Đang tải thông tin máy…</ServiceText> : null}
    {profile.isError && !denied ? <Text accessibilityRole="alert" style={notice}>Chưa tải được thông tin máy. Kiểm tra kết nối và bấm Làm mới thông tin máy.</Text> : null}
    {device ? <>
      {sample ? <View style={{ backgroundColor: colors.infoLight, padding: 16, borderRadius: 12, gap: 6 }}>
        <Text style={{ color: colors.info, fontSize: 16, fontWeight: '600' }}>Máy minh họa</Text>
        <Text style={{ color: colors.info, fontSize: 15, lineHeight: 24 }}>Cấu hình, người sử dụng và số liệu dưới đây là dữ liệu mẫu. Máy chưa có Agent thật kết nối.</Text>
      </View> : null}
      <ServiceText heading>Thông tin máy</ServiceText>
      <AppCard>
        <InfoRow label="Hãng sản xuất" value={device.manufacturer} />
        <InfoRow label="Dòng máy" value={device.model} />
        <InfoRow label="Loại thiết bị" value={device.assetType === 'Workstation' ? 'Máy tính để bàn' : device.assetType === 'Laptop' ? 'Máy tính xách tay' : device.assetType} />
        <InfoRow label="Số sê-ri" value={device.serialNumber} />
        <InfoRow label="Hệ điều hành" value={device.osVersion} />
        <InfoRow label="Phiên bản Agent" value={sample ? 'Chưa cài Agent thật' : device.agentVersion} />
        <InfoRow label="Kết nối gần nhất" value={device.lastSeenAt ? displayDate(device.lastSeenAt) : 'Chưa ghi nhận kết nối'} />
      </AppCard>
      <ServiceText heading>Người sử dụng và vị trí</ServiceText>
      <AppCard>
        <InfoRow label="Người được phân công" value={device.assignedUserName ? displayDeviceName(device.assignedUserName) : 'Chưa phân công'} />
        <InfoRow label="Phòng / bộ phận" value={device.locationRoom ? displayDeviceName(device.locationRoom) : null} />
        <InfoRow label="Tầng" value={device.locationFloor} />
        <InfoRow label="Tòa nhà" value={device.locationBuilding} />
        <InfoRow label="Cơ sở" value={device.locationCampus} />
      </AppCard>
      <ServiceText heading>Cấu hình phần cứng</ServiceText>
      <AppCard>
        {specifications.invalid ? <Text accessibilityRole="alert" style={notice}>Chưa đọc được thông số chi tiết. Liên hệ IT để kiểm tra hồ sơ máy.</Text> : null}
        <InfoRow label="Bộ xử lý" value={specifications.data?.cpu} />
        <InfoRow label="Bộ nhớ RAM" value={specifications.data?.ram} />
        <InfoRow label="Ổ lưu trữ" value={specifications.data?.storage} />
      </AppCard>
      <ServiceText heading>{sample ? 'Số liệu minh họa' : 'Mức sử dụng tài nguyên'}</ServiceText>
      <AppCard>
        {telemetry.isError ? <Text accessibilityRole="alert" style={notice}>Chưa cập nhật được số liệu. Thông tin hồ sơ máy vẫn hiển thị ở trên.</Text> : null}
        {latest ? <>
          <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 22 }}>Thời điểm bản ghi: {displayDate(stamp)}</Text>
          {!sample && stale ? <Text accessibilityRole="alert" style={notice}>Số liệu cũ hoặc chưa xác minh lại được; không thể hiện trạng thái hiện tại.</Text> : null}
          <ResourceMeter label="CPU" value={latest.cpuPercent} />
          <ResourceMeter label="RAM" value={latest.ramPercent} />
          <ResourceMeter label="Ổ đĩa" value={latest.diskPercent} />
        </> : <ServiceText>{telemetry.isPending ? 'Đang tải số liệu…' : 'Chưa có số liệu từ Agent.'}</ServiceText>}
      </AppCard>
      {sample && specifications.data?.sampleInstalledApplications?.length ? <>
        <ServiceText heading>Phần mềm minh họa</ServiceText>
        <AppCard>
          <Text style={{ color: colors.inkMuted, fontSize: 14, lineHeight: 22 }}>Danh sách dựng mẫu, không phải kiểm kê phần mềm đã cài trên máy.</Text>
          {specifications.data.sampleInstalledApplications.map((name, index) => <InfoRow key={index} label={`Phần mềm ${index + 1}`} value={name} />)}
        </AppCard>
      </> : null}
      <ServiceText heading>Thông tin tài sản</ServiceText>
      <AppCard>
        <InfoRow label="Trạng thái tài sản" value={device.assetStatus === 'Active' ? 'Đang sử dụng' : device.assetStatus} />
        <InfoRow label="Ngày mua" value={displayDate(device.purchaseDate, false)} />
        <InfoRow label="Giá mua" value={device.purchaseCost === null ? null : device.purchaseCost.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' })} />
        <InfoRow label="Hết bảo hành" value={displayDate(device.warrantyExpiresAt, false)} />
        <InfoRow label="Nhà cung cấp" value={device.vendorName ? displayDeviceName(device.vendorName) : null} />
      </AppCard>
    </> : null}
  </ServicePage>;
}
