import React from 'react';
import { View, Text, ScrollView, StyleSheet } from 'react-native';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppCard } from '../../components/common';
import { spacing, typography } from '../../theme/tokens';

export function PrivacyManifestView() {
  const { t } = useI18n();
  const colors = useAppColors();

  const collectedItems = [
    'Trạng thái trực tuyến / ngoại tuyến (Online / Offline heartbeat)',
    'Tỷ lệ sử dụng phần cứng cơ bản: CPU (%), RAM (%), Ổ đĩa (%)',
    'Phiên bản hệ điều hành và phiên bản SentinelLAN Agent do Agent báo cáo',
    'Tên chính sách được gán; không đồng nghĩa đã được Agent thực thi',
    'Lịch sử báo cáo sự cố phần cứng/mạng do chính người dùng gửi',
  ];

  const prohibitedItems = [
    'Định vị vị trí địa lý / GPS',
    'Ghi âm micro hoặc quay video / chụp ảnh nền từ camera',
    'Nhật ký cuộc gọi, tin nhắn SMS, danh bạ điện thoại',
    'Nội dung bộ nhớ tạm (Clipboard)',
    'Lịch sử duyệt web hoặc lưu lượng truy cập mạng (Network payload)',
    'Tập tin cá nhân, tài liệu hoặc ảnh trên máy tính và điện thoại',
    'Mã nhận dạng quảng cáo (Advertising ID) hoặc fingerprinting để theo dõi người dùng',
  ];

  const agentPermissions = [
    'Chạy dưới tài khoản dịch vụ quyền hạn tối thiểu trên máy được quản lý',
    'Gửi telemetry kỹ thuật outbound tới API có xác thực; Production yêu cầu HTTPS',
    'Không mở port inbound, không chấp nhận shell từ xa tùy ý',
    'Lệnh khóa máy và cô lập mạng trong phiên bản này chỉ trả kết quả mô phỏng, không đổi hệ điều hành',
  ];

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: colors.background }]}
      contentContainerStyle={styles.content}
    >
      <Text style={[styles.title, { color: colors.ink }]}>{t('privacyManifestTitle')}</Text>
      <Text style={[styles.subtitle, { color: colors.inkMuted }]}>
        {t('privacyManifestSubtitle')}
      </Text>

      {/* Collected Data Card */}
      <AppCard style={{ borderColor: colors.success }}>
        <Text style={[styles.sectionHeader, { color: colors.success }]}>
          ✓ {t('collectedDataTitle')}
        </Text>
        {collectedItems.map((item, index) => (
          <View key={index} style={styles.bulletRow}>
            <Text style={[styles.bullet, { color: colors.success }]}>•</Text>
            <Text style={[styles.bulletText, { color: colors.ink }]}>{item}</Text>
          </View>
        ))}
      </AppCard>

      {/* Prohibited Data Card */}
      <AppCard style={{ borderColor: colors.danger }}>
        <Text style={[styles.sectionHeader, { color: colors.danger }]}>
          🚫 {t('prohibitedDataTitle')}
        </Text>
        {prohibitedItems.map((item, index) => (
          <View key={index} style={styles.bulletRow}>
            <Text style={[styles.bullet, { color: colors.danger }]}>•</Text>
            <Text style={[styles.bulletText, { color: colors.ink }]}>{item}</Text>
          </View>
        ))}
      </AppCard>

      {/* Agent Permissions Card */}
      <AppCard>
        <Text style={[styles.sectionHeader, { color: colors.primary }]}>
          🛡️ {t('agentPermissionsTitle')}
        </Text>
        {agentPermissions.map((item, index) => (
          <View key={index} style={styles.bulletRow}>
            <Text style={[styles.bullet, { color: colors.primary }]}>•</Text>
            <Text style={[styles.bulletText, { color: colors.ink }]}>{item}</Text>
          </View>
        ))}
      </AppCard>

      {/* Retention Card */}
      <AppCard>
        <Text style={[styles.sectionHeader, { color: colors.ink }]}>
          ⏱️ {t('dataRetentionTitle')}
        </Text>
        <Text style={[styles.bulletText, { color: colors.inkMuted }]}>
          Telemetry snapshot kỹ thuật tự động hết hạn và bị xóa định kỳ sau 90 {t('days')}. Nhật ký
          kiểm toán (Audit Log) lưu trữ tối thiểu thông tin định danh sự kiện và không ghi lại mật
          khẩu, token hoặc nội dung nhạy cảm.
        </Text>
      </AppCard>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  content: {
    padding: spacing.lg,
    paddingBottom: spacing.xxxl,
  },
  title: {
    ...typography.titleLarge,
    marginBottom: spacing.xs,
  },
  subtitle: {
    ...typography.bodyMedium,
    marginBottom: spacing.lg,
    lineHeight: 22,
  },
  sectionHeader: {
    ...typography.titleSmall,
    marginBottom: spacing.md,
  },
  bulletRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    marginBottom: spacing.sm,
  },
  bullet: {
    fontSize: 18,
    lineHeight: 22,
    marginRight: spacing.sm,
  },
  bulletText: {
    flex: 1,
    ...typography.bodyMedium,
    lineHeight: 22,
  },
});
