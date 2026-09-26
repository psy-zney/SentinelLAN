import React, { useCallback, useEffect, useState } from 'react';
import { AppState, ScrollView, Text } from 'react-native';
import { Redirect, useFocusEffect } from 'expo-router';
import { AppButton, useAppColors } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { spacing } from '../../theme/tokens';
import type { SupportRequest } from '../../lib/validation/self-service';

export const categories = { Network: 'Không vào mạng', Printer: 'Không in được', Slow: 'Máy chậm', Application: 'Phần mềm không mở', Suspicious: 'Có thông báo đáng ngờ', Other: 'Vấn đề khác' } as const;
export const statuses: Record<SupportRequest['status'], string> = { Open: 'Đã nhận yêu cầu', InProgress: 'IT đang xử lý', AwaitingEmployee: 'Chờ bạn kiểm tra', Approved: 'IT đã duyệt', Rejected: 'IT chưa duyệt', Resolved: 'IT đã xử lý', Closed: 'Bạn đã xác nhận dùng được' };
export function requestKey() { return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.floor(Math.random() * 16); return (c === 'x' ? r : (r & 3) | 8).toString(16); }); }
export function useActiveScreen() {
  const [focused, setFocused] = useState(false);
  useFocusEffect(useCallback(() => { setFocused(true); return () => setFocused(false); }, []));
  const [active, setActive] = useState(AppState.currentState === 'active');
  const { status } = useAuth();
  useEffect(() => { const listener = AppState.addEventListener('change', value => setActive(value === 'active')); return () => listener.remove(); }, []);
  return focused && active && status === 'authenticated';
}
export function ServicePage({ children }: { children: React.ReactNode }) {
  const colors = useAppColors();
  const { status } = useAuth();
  if (status !== 'authenticated') return <Redirect href="/" />;
  return <ScrollView style={{ flex: 1, backgroundColor: colors.background }} contentContainerStyle={{ padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xxxl }}>{children}</ScrollView>;
}
export function ServiceText({ children, heading = false }: { children: React.ReactNode; heading?: boolean }) {
  const colors = useAppColors();
  return <Text accessibilityRole={heading ? 'header' : undefined} style={{ color: colors.ink, fontSize: heading ? 20 : 16, fontWeight: heading ? '700' : '400', lineHeight: heading ? 28 : 24 }}>{children}</Text>;
}
export function ServiceError({ error, retry }: { error: unknown; retry?: () => void }) {
  const colors = useAppColors();
  if (!error) return null;
  return <><Text accessibilityRole="alert" style={{ color: colors.danger, fontSize: 16 }}>Không hoàn tất được. {error instanceof Error ? error.message : 'Hãy kiểm tra kết nối và thử lại.'}</Text>{retry ? <AppButton title="Thử lại" variant="outline" onPress={retry} /> : null}</>;
}
export function commandCopy(request: SupportRequest) {
  if (request.commandStatus === 'Succeeded') return 'Máy đã gửi kết quả thành công. Xem chi tiết bên dưới để biết thao tác thực tế hay mô phỏng.';
  if (request.commandStatus === 'Failed' || request.commandStatus === 'Expired' || request.commandStatus === 'Rejected') return 'Thao tác chưa hoàn tất trên máy. IT đã có thông tin để kiểm tra.';
  if (request.commandId) return 'Đang chờ máy xác nhận kết quả. IT duyệt chưa có nghĩa là thao tác đã hoàn tất.';
  if (request.kind === 'Panic') return 'Đã gửi báo động cho IT. Chưa có xác nhận cô lập mạng.';
  return null;
}
