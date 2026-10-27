import React, { useRef, useState } from 'react';
import { Alert, Pressable, Switch, View, Text } from 'react-native';
import { Check, CircleHelp, Gauge, LayoutGrid, Printer, ShieldAlert, Wifi } from 'lucide-react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useQueryClient } from '@tanstack/react-query';
import { AppButton, AppCard, AppInput, useAppColors } from '../../components/common';
import { SupportCategorySchema, SupportKindSchema, type CreateSupportRequest } from '../../lib/validation/self-service';
import { useAuth } from '../auth/auth-context';
import { categories, requestKey, ServicePage, ServiceText, ServiceError } from './shared';
import { ImageAttachmentPicker, type SelectedAttachment } from './image-attachment';
import { cacheCreatedRequest } from './request-cache';

const categoryIcons = { Network: Wifi, Printer, Slow: Gauge, Application: LayoutGrid, Suspicious: ShieldAlert, Other: CircleHelp };

export function RequestCreateView() {
  const params = useLocalSearchParams<{ kind?: string; category?: string; catalogAppId?: string; title?: string }>();
  const parsedKind = SupportKindSchema.safeParse(params.kind);
  const kind = parsedKind.success ? parsedKind.data : 'Incident';
  const initialCategory = SupportCategorySchema.safeParse(params.category);
  const [category, setCategory] = useState<CreateSupportRequest['category']>(initialCategory.success ? initialCategory.data : 'Other');
  const [title, setTitle] = useState(params.title ?? (kind === 'PauseAgent' ? 'Xin tạm dừng SentinelLAN' : kind === 'UninstallAgent' ? 'Xin gỡ SentinelLAN' : kind === 'Appointment' ? 'Xin hẹn IT hỗ trợ' : ''));
  const [description, setDescription] = useState('');
  const [canWork, setCanWork] = useState(true);
  const [appointment, setAppointment] = useState('');
  const [image, setImage] = useState<SelectedAttachment | null>(null);
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState<unknown>();
  const [submitting, setSubmitting] = useState(false);
  const key = useRef(requestKey());
  const createdId = useRef<string | null>(null);
  const { apiClient } = useAuth();
  const colors = useAppColors();
  const router = useRouter();
  const queryClient = useQueryClient();
  const submit = async () => {
    if (submitting) return;
    setSubmitting(true); setError(undefined);
    try {
      if (image && !consent) throw new Error('Hãy xem ảnh và đồng ý gửi trước khi tiếp tục.');
      let appointmentAt: string | undefined;
      if (kind === 'Appointment') {
        const normalized = appointment.trim().replace(' ', 'T');
        if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(normalized)) throw new Error('Nhập ngày giờ theo mẫu 2026-10-01 14:30.');
        const date = new Date(normalized);
        if (!Number.isFinite(date.getTime()) || date.getTime() <= Date.now() || date.getTime() > Date.now() + 30 * 86400000) throw new Error('Chọn thời gian trong 30 ngày tới.');
        appointmentAt = date.toISOString();
      }
      if (!createdId.current) {
        const request = await apiClient.createSupportRequest({ kind, category, title: title.trim() || categories[category], description: description.trim(), canWork, catalogAppId: params.catalogAppId, appointmentAt, confirmed: true, idempotencyKey: key.current });
        createdId.current = request.id;
        await cacheCreatedRequest(queryClient, request);
      }
      if (image) await apiClient.uploadSupportAttachment(createdId.current, { fileName: image.fileName, contentType: image.contentType, base64: image.base64 });
      await queryClient.invalidateQueries({ queryKey: ['support-requests'] });
      router.replace({ pathname: '/request-detail', params: { id: createdId.current } });
    } catch (err) { setError(createdId.current ? new Error(`Yêu cầu đã gửi nhưng ảnh chưa gửi được. Thử lại để gửi ảnh hoặc bỏ ảnh rồi tiếp tục. ${err instanceof Error ? err.message : ''}`) : err); }
    finally { setSubmitting(false); }
  };
  const confirmSubmit = () => {
    if (kind === 'PauseAgent' || kind === 'UninstallAgent' || kind === 'Privilege') {
      Alert.alert('Gửi yêu cầu cho IT', kind === 'Privilege' ? 'IT sẽ duyệt cài ứng dụng cần thiết trong thời hạn tối đa 30 phút. Bạn cần chọn đúng ứng dụng từ danh sách.' : 'Thao tác chỉ được thực hiện sau khi IT duyệt và bạn nhập mã xác nhận. Tạm dừng tối đa 15 phút.', [{ text: 'Hủy', style: 'cancel' }, { text: 'Gửi yêu cầu', onPress: () => { void submit(); } }]);
    } else { void submit(); }
  };
  return <ServicePage>
    <ServiceText heading>{kind === 'Incident' ? 'Bạn gặp vấn đề gì?' : kind === 'Appointment' ? 'Chọn giờ IT hỗ trợ' : 'Gửi yêu cầu cho IT'}</ServiceText>
    {kind === 'Incident' ? <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 10 }}>
      {Object.entries(categories).map(([value, label]) => {
        const selected = category === value;
        const Icon = categoryIcons[value as keyof typeof categoryIcons];
        return <Pressable key={value} accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ selected, disabled: submitting || Boolean(createdId.current) }}
          disabled={submitting || Boolean(createdId.current)} onPress={() => setCategory(value as CreateSupportRequest['category'])}
          style={{ flexBasis: 140, flexGrow: 1, minHeight: 104, padding: 14, borderRadius: 14, borderWidth: 1.5, borderColor: selected ? colors.primary : colors.border, backgroundColor: selected ? colors.primaryLight : colors.surface, gap: 10 }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}><Icon size={24} color={colors.primary} accessible={false} />{selected ? <Check size={18} color={colors.primary} accessible={false} /> : null}</View>
          <Text style={{ color: colors.ink, fontSize: 15, lineHeight: 22, fontWeight: '600' }}>{label}</Text>
        </Pressable>;
      })}
    </View> : null}
    <AppCard style={{ marginBottom: 0, gap: 4 }}>
    <ServiceText heading>Thông tin gửi IT</ServiceText>
    <AppInput testID="incident-title-input" label="Tiêu đề (có thể để trống khi báo sự cố)" value={title} maxLength={200} editable={!submitting && !createdId.current} onChangeText={setTitle} />
    <AppInput testID="incident-desc-input" label="Mô tả thêm hoặc lý do yêu cầu" value={description} maxLength={2000} multiline editable={!submitting && !createdId.current} onChangeText={setDescription} />
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}><Switch accessibilityLabel="Tôi vẫn tiếp tục làm việc được" value={canWork} disabled={submitting || Boolean(createdId.current)} onValueChange={setCanWork} /><Text style={{ flex: 1, color: colors.ink, fontSize: 16 }}>{canWork ? 'Tôi vẫn tiếp tục làm việc được' : 'Tôi không tiếp tục làm việc được'}</Text></View>
    {kind === 'Appointment' ? <AppInput label="Ngày và giờ tại nơi bạn đang ở" hint="Ví dụ: 2026-10-01 14:30; trong 30 ngày tới." value={appointment} onChangeText={setAppointment} editable={!submitting && !createdId.current} /> : null}
    <ImageAttachmentPicker value={image} onChange={setImage} consent={consent} onConsent={setConsent} disabled={submitting} />
    <ServiceError error={error} />
    <AppButton testID="submit-incident-button" title={createdId.current ? 'Tiếp tục yêu cầu đã gửi' : 'Gửi yêu cầu cho IT'} onPress={confirmSubmit} loading={submitting} disabled={(image !== null && !consent) || (kind !== 'Incident' && title.trim().length < 3) || (kind === 'Privilege' && !params.catalogAppId)} />
    </AppCard>
  </ServicePage>;
}
