import React, { useRef, useState } from 'react';
import { Alert } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { AppButton, AppCard, AppInput } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { commandCopy, requestKey, statuses, ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';
import { ImageAttachmentPicker, type SelectedAttachment } from './image-attachment';

export function RequestDetailView() {
  const params = useLocalSearchParams<{ id?: string }>();
  const id = z.string().uuid().safeParse(params.id);
  const requestId = id.success ? id.data : '';
  const { apiClient } = useAuth();
  const active = useActiveScreen();
  const queryClient = useQueryClient();
  const [body, setBody] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [image, setImage] = useState<SelectedAttachment | null>(null);
  const [consent, setConsent] = useState(false);
  const messageKey = useRef(requestKey());
  const pendingMessage = useRef<string | null>(null);
  const request = useQuery({ queryKey: ['support-request', requestId], queryFn: () => apiClient.supportRequest(requestId), enabled: active && id.success, refetchInterval: active ? 5000 : false, refetchIntervalInBackground: false });
  const messages = useQuery({ queryKey: ['support-messages', requestId], queryFn: ({ signal }) => apiClient.supportMessages(requestId, signal), enabled: active && id.success, refetchInterval: active ? 5000 : false, refetchIntervalInBackground: false });
  const attachments = useQuery({ queryKey: ['support-attachments', requestId], queryFn: () => apiClient.supportAttachments(requestId), enabled: active && id.success });
  const refresh = async () => { await Promise.all([queryClient.invalidateQueries({ queryKey: ['support-request', requestId] }), queryClient.invalidateQueries({ queryKey: ['support-messages', requestId] }), queryClient.invalidateQueries({ queryKey: ['support-attachments', requestId] }), queryClient.invalidateQueries({ queryKey: ['support-requests'] })]); };
  const perform = async (action: () => Promise<unknown>) => { if (busy) return; setBusy(true); setError(undefined); try { await action(); await refresh(); } catch (err) { setError(err); } finally { setBusy(false); } };
  const send = () => { void perform(async () => {
    const trimmed = body.trim();
    if (pendingMessage.current !== trimmed) { pendingMessage.current = trimmed; messageKey.current = requestKey(); }
    await apiClient.sendSupportMessage(requestId, trimmed, messageKey.current);
    pendingMessage.current = null; messageKey.current = requestKey(); setBody('');
  }); };
  if (!id.success) return <ServicePage><ServiceText>Không tìm thấy yêu cầu hợp lệ.</ServiceText></ServicePage>;
  const item = request.data;
  const maintenance = item && (item.kind === 'PauseAgent' || item.kind === 'UninstallAgent');
  const close = (status: 'Open' | 'Closed') => Alert.alert(status === 'Closed' ? 'Bạn đã dùng được máy?' : 'Bạn vẫn còn gặp lỗi?', status === 'Closed' ? 'Xác nhận hoàn tất yêu cầu này.' : 'IT sẽ nhận được yêu cầu kiểm tra lại.', [{ text: 'Hủy', style: 'cancel' }, { text: 'Xác nhận', onPress: () => { void perform(() => apiClient.updateOwnSupportRequest(requestId, status, status === 'Closed' ? 'Nhân viên xác nhận đã dùng được.' : 'Nhân viên báo vẫn còn lỗi, cần IT kiểm tra lại.', true)); } }]);
  return <ServicePage>
    <ServiceError error={request.error} retry={() => { void request.refetch(); }} />
    {item ? <AppCard>
      <ServiceText heading>{item.title}</ServiceText>
      <ServiceText>{statuses[item.status]}</ServiceText>
      <ServiceText>{item.assignedTechnicianName ? `Người phụ trách: ${item.assignedTechnicianName}` : 'Đang chờ IT phân công người phụ trách'}</ServiceText>
      <ServiceText>{item.description}</ServiceText>
      <ServiceText>{item.canWork ? 'Bạn vẫn làm việc được' : 'Bạn đang không tiếp tục làm việc được'}</ServiceText>
      {item.appointmentAt ? <ServiceText>Giờ hỗ trợ đã yêu cầu: {new Date(item.appointmentAt).toLocaleString('vi-VN')}</ServiceText> : null}
      {commandCopy(item) ? <ServiceText>{commandCopy(item)}</ServiceText> : null}
      {item.commandMessage ? <ServiceText>{item.commandMessage}</ServiceText> : null}
      {item.status !== 'Closed' ? <AppButton title="Đã dùng được" disabled={busy} onPress={() => close('Closed')} /> : null}
      {['Closed', 'Resolved', 'AwaitingEmployee'].includes(item.status) ? <AppButton title="Vẫn còn lỗi — nhờ IT kiểm tra lại" variant="outline" disabled={busy} onPress={() => close('Open')} /> : null}
    </AppCard> : <ServiceText>Đang tải yêu cầu…</ServiceText>}
    {maintenance && item?.status === 'Approved' && !item.commandId ? <AppCard>
      <ServiceText heading>Mã xác nhận do IT cấp</ServiceText>
      <ServiceText>{item.kind === 'PauseAgent' ? 'Tạm dừng bảo vệ tối đa 15 phút; Agent tự hoạt động lại.' : 'Gỡ SentinelLAN khỏi đúng máy được phân công.'} Mã dùng một lần và hết hạn sau 5 phút.</ServiceText>
      <AppInput label="Mã 8 chữ số" value={code} onChangeText={setCode} keyboardType="number-pad" maxLength={8} secureTextEntry autoComplete="off" />
      <AppButton title="Xác nhận thao tác với mã IT" loading={busy} disabled={!/^\d{8}$/.test(code)} onPress={() => Alert.alert('Xác nhận thao tác', item.kind === 'PauseAgent' ? 'Bạn đồng ý tạm dừng SentinelLAN tối đa 15 phút?' : 'Bạn đồng ý gỡ SentinelLAN trên máy được phân công?', [{ text: 'Hủy', style: 'cancel' }, { text: 'Xác nhận', style: 'destructive', onPress: () => { void perform(async () => { try { return await apiClient.redeemMaintenance(requestId, code, true); } finally { setCode(''); } }); } }])} />
    </AppCard> : null}
    <ServiceText heading>Trao đổi với IT</ServiceText>
    <ServiceText>IT có thể trả lời sau khi nhận yêu cầu. Tin nhắn được lưu trong yêu cầu này.</ServiceText>
    <ServiceError error={messages.error} retry={() => { void messages.refetch(); }} />
    {messages.data?.map(message => <AppCard key={message.id}><ServiceText heading>{message.authorName}</ServiceText><ServiceText>{message.body}</ServiceText><ServiceText>{new Date(message.createdAt).toLocaleString('vi-VN')}</ServiceText></AppCard>)}
    <AppInput label="Tin nhắn cho IT" value={body} onChangeText={setBody} multiline maxLength={2000} editable={!busy} />
    <AppButton title="Gửi tin nhắn" disabled={!body.trim() || !item} loading={busy} onPress={send} />
    <ServiceText heading>Ảnh đã gửi</ServiceText>
    <ServiceError error={attachments.error} retry={() => { void attachments.refetch(); }} />
    {attachments.data?.map(attachment => <ServiceText key={attachment.id}>{attachment.fileName} · {Math.ceil(attachment.size / 1024)} KiB</ServiceText>)}
    <ImageAttachmentPicker value={image} onChange={setImage} consent={consent} onConsent={setConsent} disabled={busy} />
    {image ? <AppButton title="Gửi ảnh cho IT" disabled={!consent || !item} loading={busy} onPress={() => { void perform(async () => { if (!consent) return; await apiClient.uploadSupportAttachment(requestId, { fileName: image.fileName, contentType: image.contentType, base64: image.base64 }); setImage(null); setConsent(false); }); }} /> : null}
    <ServiceError error={error} />
  </ServicePage>;
}
