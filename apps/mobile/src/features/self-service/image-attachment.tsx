import React, { useState } from 'react';
import { Image, Switch, View, Text } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { AppButton, useAppColors } from '../../components/common';
import type { AttachmentUpload } from '../../lib/validation/self-service';
import { ServiceError, ServiceText } from './shared';

export type SelectedAttachment = AttachmentUpload & { uri: string };
export function parseSelectedImage(asset: ImagePicker.ImagePickerAsset): SelectedAttachment {
  const base64 = asset.base64;
  if (!base64) throw new Error('Không đọc được ảnh. Hãy chọn lại ảnh JPEG hoặc PNG.');
  const contentType = base64.startsWith('/9j/') ? 'image/jpeg' : base64.startsWith('iVBORw0KGgo') ? 'image/png' : null;
  if (!contentType) throw new Error('Chỉ gửi được ảnh JPEG hoặc PNG.');
  const padding = base64.endsWith('==') ? 2 : base64.endsWith('=') ? 1 : 0;
  const size = Math.floor(base64.length * 3 / 4) - padding;
  if (size > 2 * 1024 * 1024 || (asset.fileSize ?? 0) > 2 * 1024 * 1024) throw new Error('Ảnh phải nhỏ hơn hoặc bằng 2 MiB. Hãy chọn ảnh nhỏ hơn.');
  return { uri: asset.uri, base64, contentType, fileName: contentType === 'image/png' ? 'anh-loi.png' : 'anh-loi.jpg' };
}
export function ImageAttachmentPicker({ value, onChange, consent, onConsent, disabled = false }: { value: SelectedAttachment | null; onChange: (value: SelectedAttachment | null) => void; consent: boolean; onConsent: (value: boolean) => void; disabled?: boolean }) {
  const [error, setError] = useState<unknown>();
  const colors = useAppColors();
  const pick = async (camera: boolean) => {
    setError(undefined);
    try {
      if (camera) {
        const permission = await ImagePicker.requestCameraPermissionsAsync();
        if (!permission.granted) throw new Error('Bạn chưa cho phép chụp ảnh. Có thể chọn ảnh có sẵn.');
      }
      const options: ImagePicker.ImagePickerOptions = { mediaTypes: ['images'], base64: true, quality: 0.7, allowsEditing: true, exif: false };
      const result = camera ? await ImagePicker.launchCameraAsync(options) : await ImagePicker.launchImageLibraryAsync(options);
      if (!result.canceled) { onChange(parseSelectedImage(result.assets[0])); onConsent(false); }
    } catch (err) { setError(err); }
  };
  return <View style={{ gap: 12 }}>
    <ServiceText>Ảnh lỗi (không bắt buộc)</ServiceText>
    <ServiceText>Kiểm tra và bỏ thông tin cá nhân, mật khẩu hoặc tài liệu công ty khỏi ảnh trước khi gửi. Chỉ JPEG/PNG, tối đa 2 MiB.</ServiceText>
    <AppButton title="Chọn ảnh có sẵn" variant="outline" disabled={disabled} onPress={() => { void pick(false); }} />
    <AppButton title="Chụp ảnh màn hình máy tính" variant="outline" disabled={disabled} onPress={() => { void pick(true); }} />
    <ServiceError error={error} />
    {value ? <>
      <Image accessibilityLabel="Ảnh lỗi bạn chuẩn bị gửi cho IT" source={{ uri: value.uri }} style={{ width: '100%', height: 220, resizeMode: 'contain' }} />
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}><Switch accessibilityLabel="Tôi đã xem ảnh và đồng ý gửi cho IT" value={consent} disabled={disabled} onValueChange={onConsent} /><Text style={{ flex: 1, color: colors.ink, fontSize: 16 }}>Tôi đã xem ảnh và đồng ý gửi cho IT.</Text></View>
      <AppButton title="Bỏ ảnh" variant="outline" disabled={disabled} onPress={() => { onChange(null); onConsent(false); }} />
    </> : null}
  </View>;
}
