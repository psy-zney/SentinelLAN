import React, { useEffect, useMemo, useState } from 'react';
import { Text, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AppButton, AppCard, useAppColors } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, useActiveScreen } from '../self-service/shared';
import { ApiError } from '../../lib/api/client';

function isAccessError(error: unknown) {
  return error instanceof ApiError && (error.status === 401 || error.status === 403);
}

function bytes(value: number) {
  if (value >= 1024 ** 3) return `${(value / 1024 ** 3).toFixed(1)} GiB`;
  if (value >= 1024 ** 2) return `${(value / 1024 ** 2).toFixed(1)} MiB`;
  return `${value} B`;
}

function uptime(seconds: number) {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor(seconds % 86400 / 3600);
  const minutes = Math.floor(seconds % 3600 / 60);
  return days > 0 ? `${days} ngày ${hours} giờ` : `${hours} giờ ${minutes} phút`;
}

function stateLabel(state: string) {
  const labels: Record<string, string> = {
    running: 'Đang chạy', active: 'Đang hoạt động', degraded: 'Có dịch vụ cần kiểm tra',
    inactive: 'Chưa chạy', failed: 'Lỗi', exited: 'Đã dừng', dead: 'Lỗi',
    paused: 'Tạm dừng', restarting: 'Đang khởi động lại', starting: 'Đang khởi động',
    activating: 'Đang khởi động', stopping: 'Đang dừng', created: 'Mới tạo',
    enabled: 'Tự khởi động', disabled: 'Không tự khởi động', static: 'Theo dịch vụ phụ thuộc',
    healthy: 'Tốt', unhealthy: 'Không đạt',
  };
  return labels[state] ?? state;
}

function Notice({ children }: { children: React.ReactNode }) {
  const colors = useAppColors();
  return <Text accessibilityRole="alert" style={{ color: colors.danger, fontSize: 16, lineHeight: 24 }}>{children}</Text>;
}

function Metric({ label, value, detail }: { label: string; value: number; detail?: string }) {
  const colors = useAppColors();
  return <AppCard>
    <ServiceText heading>{label}: {value.toFixed(1)}%</ServiceText>
    <View accessible accessibilityRole="progressbar" accessibilityLabel={label}
      accessibilityValue={{ min: 0, max: 100, now: value }}
      style={{ height: 8, borderRadius: 4, backgroundColor: colors.border }}>
      <View style={{ height: 8, borderRadius: 4, width: `${value}%`, backgroundColor: value >= 90 ? colors.danger : colors.primary }} />
    </View>
    {detail ? <ServiceText>{detail}</ServiceText> : null}
  </AppCard>;
}

export function VpsStatusView() {
  const { user, status } = useAuth();
  if (status !== 'authenticated') return <ServicePage>{null}</ServicePage>;
  if (user?.role !== 'Admin') return <ServicePage><Notice>Chỉ quản trị viên được xem trạng thái VPS.</Notice></ServicePage>;
  const scope = `${user.organizationId}:${user.id}`;
  return <VpsStatusContent key={scope} scope={scope} />;
}

function VpsStatusContent({ scope }: { scope: string }) {
  const { apiClient } = useAuth();
  const active = useActiveScreen();
  const queryClient = useQueryClient();
  const queryKey = useMemo(() => ['vps-host-status', scope], [scope]);
  const [now, setNow] = useState(Date.now);
  const [showServices, setShowServices] = useState(false);
  const [showPorts, setShowPorts] = useState(false);
  const query = useQuery({
    queryKey,
    queryFn: ({ signal }) => apiClient.hostStatus(signal),
    enabled: active,
    retry: false,
    gcTime: 0,
    refetchInterval: active ? query => isAccessError(query.state.error) ? false : 60000 : false,
    refetchOnWindowFocus: false,
  });

  useEffect(() => {
    if (!active) {
      void queryClient.cancelQueries({ queryKey, exact: true });
      return;
    }
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), 15000);
    return () => clearInterval(timer);
  }, [active, queryClient, queryKey]);

  useEffect(() => () => { void queryClient.cancelQueries({ queryKey, exact: true }); }, [queryClient, queryKey]);

  const denied = isAccessError(query.error);
  const data = denied ? undefined : query.data;
  const snapshot = data?.configured && data.available ? data.snapshot : null;
  const age = snapshot ? (now - Date.parse(snapshot.capturedAtUtc)) / 1000 : 0;
  const stale = data?.stale || age > 180 || age < -60 || query.isError;

  return <ServicePage>
    <ServiceText heading>Trạng thái VPS</ServiceText>
    <ServiceText>Tự động kiểm tra mỗi phút khi đang xem màn hình này.</ServiceText>
    <AppButton title="Làm mới" variant="outline" loading={query.isFetching} disabled={!active}
      onPress={() => { void query.refetch(); }} />
    {query.isPending && !query.isError ? <ServiceText>Đang tải trạng thái VPS…</ServiceText> : null}
    {denied ? <Notice>{query.error instanceof ApiError && query.error.status === 403
      ? 'Tài khoản này không được phép xem VPS của đơn vị vận hành.'
      : 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'}</Notice> : null}
    {query.isError && !denied ? <Notice>Không tải được trạng thái VPS. Kiểm tra kết nối và bấm Làm mới để thử lại.</Notice> : null}
    {data && !data.configured ? <AppCard><ServiceText heading>Chưa cấu hình giám sát VPS</ServiceText><ServiceText>{data.message}</ServiceText></AppCard> : null}
    {data?.configured && (!data.available || !data.snapshot) ? <AppCard><ServiceText heading>Chưa đọc được trạng thái VPS</ServiceText><ServiceText>{data.message}</ServiceText></AppCard> : null}
    {snapshot ? <>
      <AppCard>
        <ServiceText heading>{snapshot.name}</ServiceText>
        <ServiceText>{snapshot.host}</ServiceText>
        <ServiceText>{snapshot.osInfo}</ServiceText>
        <ServiceText>Thời gian hoạt động: {uptime(snapshot.uptimeSeconds)}</ServiceText>
        <ServiceText>Thời điểm thu thập: {new Date(snapshot.capturedAtUtc).toLocaleString('vi-VN')}</ServiceText>
        {stale ? <Notice>Dữ liệu cũ hoặc chưa xác minh lại được. Các số liệu dưới đây thuộc lần thu thập gần nhất.</Notice>
          : <ServiceText>Đã nhận dữ liệu mới.</ServiceText>}
        <ServiceText>Hệ thống: {stateLabel(snapshot.runtime.systemState)}</ServiceText>
      </AppCard>
      {snapshot.warnings.length ? <AppCard><ServiceText heading>Cảnh báo từ máy chủ</ServiceText>{snapshot.warnings.map((warning, index) => <Notice key={index}>{warning}</Notice>)}</AppCard> : null}
      <Metric label="CPU" value={snapshot.cpuPercent} />
      <Metric label="RAM" value={snapshot.ramPercent} detail={`${bytes(snapshot.memoryUsedBytes)} / ${bytes(snapshot.memoryTotalBytes)}`} />
      <Metric label="Ổ đĩa" value={snapshot.diskPercent} detail={`${bytes(snapshot.diskUsedBytes)} / ${bytes(snapshot.diskTotalBytes)}`} />
      <ServiceText heading>Docker</ServiceText>
      {!snapshot.runtime.dockerAvailable ? <AppCard><Notice>Chưa đọc được Docker.</Notice><ServiceText>{snapshot.runtime.dockerError ?? 'Chưa có thông tin từ Docker.'}</ServiceText></AppCard> : <>
        <ServiceText>{snapshot.runtime.containers.filter(container => container.state === 'running').length}/{snapshot.runtime.containers.length} container đang chạy</ServiceText>
        {!snapshot.runtime.containers.length ? <ServiceText>Không có container trong bản ghi.</ServiceText> : null}
        {snapshot.runtime.containers.map(container => <AppCard key={container.id}>
          <ServiceText heading>{container.name}</ServiceText>
          {container.project ? <ServiceText>Dự án: {container.project}</ServiceText> : null}
          <ServiceText>Trạng thái: {stateLabel(container.state)} · {container.status}</ServiceText>
          <ServiceText>Image: {container.image}</ServiceText>
          {container.health === 'unhealthy' ? <Notice>Kiểm tra sức khỏe: Không đạt</Notice>
            : <ServiceText>{container.health ? `Kiểm tra sức khỏe: ${stateLabel(container.health)}` : 'Chưa có kiểm tra sức khỏe.'}</ServiceText>}
          <ServiceText>CPU container: {container.cpuPercent === null ? 'Chưa có số liệu' : `${container.cpuPercent.toFixed(1)}%`}</ServiceText>
          <ServiceText>RAM container: {container.memoryUsage ?? 'Chưa có số liệu'}{container.memoryPercent === null ? '' : ` (${container.memoryPercent.toFixed(1)}%)`}</ServiceText>
        </AppCard>)}
      </>}
      <AppButton title={`${showServices ? 'Ẩn' : 'Xem'} dịch vụ hệ thống (${snapshot.runtime.services.length})`} variant="outline" onPress={() => setShowServices(value => !value)} />
      {showServices ? <AppCard>
        {!snapshot.runtime.services.length ? <ServiceText>Không có dữ liệu dịch vụ.</ServiceText> : null}
        {snapshot.runtime.services.map((service, index) => <View key={`${service.name}-${index}`}>
          <ServiceText heading>{service.name}</ServiceText>
          <ServiceText>{stateLabel(service.activeState)} · {stateLabel(service.startupState)}</ServiceText>
        </View>)}
      </AppCard> : null}
      <AppButton title={`${showPorts ? 'Ẩn' : 'Xem'} cổng đang lắng nghe (${snapshot.listeningPorts.length})`} variant="outline" onPress={() => setShowPorts(value => !value)} />
      {showPorts ? <AppCard>
        <ServiceText>Địa chỉ lắng nghe không xác nhận cổng truy cập được từ Internet; còn phụ thuộc tường lửa và proxy.</ServiceText>
        {!snapshot.listeningPorts.length ? <ServiceText>Không có dữ liệu cổng đang lắng nghe.</ServiceText> : null}
        {snapshot.listeningPorts.map((port, index) => <ServiceText key={index}>
          {port.address.includes(':') ? `[${port.address}]` : port.address}:{port.port}/{port.protocol.toUpperCase()} · {port.process ?? 'Chưa xác định tiến trình'}
        </ServiceText>)}
      </AppCard> : null}
    </> : null}
  </ServicePage>;
}
