import React, { createContext, useContext, useState, useEffect } from 'react';
import * as SecureStore from 'expo-secure-store';

export type Language = 'vi' | 'en';

const translations = {
  vi: {
    // Nav tabs
    tabOverview: 'Tổng quan',
    tabScan: 'Quét QR',
    tabMyDevice: 'Máy của tôi',
    tabIncidents: 'Sự cố',
    tabAccount: 'Tài khoản',

    // Common
    appName: 'SentinelLAN Employee',
    loading: 'Đang tải...',
    retry: 'Thử lại',
    cancel: 'Hủy',
    confirm: 'Xác nhận',
    error: 'Đã xảy ra lỗi',
    success: 'Thành công',
    offlineBanner: 'Không có kết nối mạng. Dữ liệu đang hiển thị từ bộ nhớ phiên.',

    // Auth
    loginTitle: 'Đăng nhập Nhân viên',
    loginSubtitle: 'Truy cập cổng giám sát an toàn SentinelLAN trên máy tính của bạn',
    orgCodeLabel: 'Mã tổ chức',
    orgCodePlaceholder: 'vd: sentinel-corp',
    emailLabel: 'Email công ty',
    emailPlaceholder: 'ten.nhanvien@congty.com',
    passwordLabel: 'Mật khẩu',
    passwordPlaceholder: 'Nhập mật khẩu...',
    loginButton: 'Đăng nhập',
    loggingIn: 'Đang xác thực...',
    loginFailed: 'Thông tin đăng nhập hoặc tổ chức không hợp lệ.',
    sessionExpired: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',

    // Activation
    activationTitle: 'Kích hoạt tài khoản',
    activationSubtitle: 'Thiết lập mật khẩu bảo mật cho tài khoản SentinelLAN mới của bạn.',
    activationTokenInvalid: 'Liên kết kích hoạt không hợp lệ, đã được dùng hoặc đã hết hạn.',
    newPasswordLabel: 'Mật khẩu mới (tối thiểu 12 ký tự)',
    confirmPasswordLabel: 'Xác nhận mật khẩu mới',
    passwordMismatch: 'Mật khẩu xác nhận không khớp.',
    passwordTooShort: 'Mật khẩu phải có độ dài từ 12 đến 128 ký tự.',
    activateButton: 'Kích hoạt tài khoản',
    activating: 'Đang kích hoạt...',
    activationSuccess: 'Kích hoạt thành công! Đang chuyển đến màn hình đăng nhập...',

    // Scan
    scanTitle: 'Quét tem QR thiết bị',
    scanSubtitle: 'Hướng camera về phía mã QR dán trên máy tính SentinelLAN của bạn',
    cameraPermissionTitle: 'Yêu cầu quyền truy cập Camera',
    cameraPermissionRationale:
      'SentinelLAN chỉ sử dụng camera để quét mã QR nhận diện thiết bị công ty được bàn giao. Chúng tôi không chụp ảnh, không quay video và không lưu trữ bất kỳ hình ảnh nào từ camera.',
    grantCameraPermission: 'Bật quyền camera',
    openSettings: 'Mở Cài đặt hệ thống',
    manualCodeOption: 'Hoặc nhập mã thiết bị thủ công',
    manualCodePlaceholder: 'Nhập mã QR hoặc tiền tố (vd: QR-A1B2C3D4)',
    submitCode: 'Xác thực mã',
    pickImageOption: 'Chọn ảnh QR từ thư viện',
    invalidQrCode: 'Mã QR không đúng định dạng SentinelLAN hợp lệ hoặc không thuộc hệ thống được phép.',
    scanResolving: 'Đang kiểm tra thiết bị...',

    // My Device
    myDeviceTitle: 'Máy tính của tôi',
    noDeviceAssignedTitle: 'Chưa có máy tính được gán',
    noDeviceAssignedDesc: 'Tài khoản của bạn hiện chưa được liên kết với máy tính nào. Vui lòng quét tem QR trên máy tính được cấp hoặc liên hệ Quản trị viên IT.',
    deviceStatusOnline: 'Đang hoạt động (Online)',
    deviceStatusOffline: 'Ngoại tuyến (Offline)',
    lastSeen: 'Lần cuối trực tuyến',
    cpuUsage: 'CPU',
    ramUsage: 'RAM',
    diskUsage: 'Ổ đĩa',
    systemSpecs: 'Thông số hệ thống',
    osVersion: 'Hệ điều hành',
    agentVersion: 'Phiên bản Agent',
    serialNumber: 'Số seri',
    assetTag: 'Mã tài sản',
    location: 'Vị trí',
    appliedPolicy: 'Chính sách bảo mật',
    privacyManifestLink: 'Xem cam kết quyền riêng tư (Privacy Manifest)',

    // Privacy Manifest
    privacyManifestTitle: 'Cam kết minh bạch dữ liệu',
    privacyManifestSubtitle: 'SentinelLAN chỉ thu thập telemetry kỹ thuật tối thiểu nhằm bảo vệ thiết bị công ty.',
    collectedDataTitle: 'Dữ liệu kỹ thuật ĐƯỢC thu thập:',
    prohibitedDataTitle: 'Dữ liệu NGHIÊM CẤM thu thập:',
    agentPermissionsTitle: 'Quyền hạn của Windows Agent:',
    dataRetentionTitle: 'Thời hạn lưu trữ dữ liệu:',
    days: 'ngày',

    // Incidents
    incidentsTitle: 'Lịch sử sự cố',
    reportIncidentButton: 'Báo sự cố mới',
    noIncidents: 'Chưa có sự cố nào được ghi nhận cho máy tính của bạn.',
    incidentTitleLabel: 'Tiêu đề sự cố',
    incidentTitlePlaceholder: 'vd: Máy tính không nhận mạng LAN, màn hình xanh...',
    incidentDescLabel: 'Mô tả chi tiết',
    incidentDescPlaceholder: 'Mô tả hiện tượng và thời điểm phát sinh...',
    severityLabel: 'Mức độ nghiêm trọng',
    severityLow: 'Thấp',
    severityMedium: 'Trung bình',
    severityHigh: 'Cao',
    severityCritical: 'Khẩn cấp',
    statusOpen: 'Mới mở',
    statusInProgress: 'Đang xử lý',
    statusResolved: 'Đã giải quyết',
    statusClosed: 'Đã đóng',
    reportSubmitting: 'Đang gửi sự cố...',
    reportSuccess: 'Sự cố đã được ghi nhận thành công.',
    resolutionNotesTitle: 'Ghi chú xử lý từ kỹ thuật viên:',

    // Account
    accountTitle: 'Tài khoản & Thiết bị',
    organization: 'Tổ chức',
    userRole: 'Vai trò',
    appVersion: 'Phiên bản ứng dụng',
    switchLanguage: 'Ngôn ngữ',
    logoutButton: 'Đăng xuất thiết bị này',
    logoutAllButton: 'Thu hồi toàn bộ phiên đăng nhập',
    logoutConfirmTitle: 'Xác nhận đăng xuất',
    logoutConfirmMsg: 'Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng trên thiết bị này?',
    logoutAllConfirmMsg: 'Hành động này sẽ đăng xuất tất cả các phiên di động của tài khoản này.',
    loggingOut: 'Đang đăng xuất...',
  },
  en: {
    // Nav tabs
    tabOverview: 'Overview',
    tabScan: 'Scan QR',
    tabMyDevice: 'My Device',
    tabIncidents: 'Incidents',
    tabAccount: 'Account',

    // Common
    appName: 'SentinelLAN Employee',
    loading: 'Loading...',
    retry: 'Retry',
    cancel: 'Cancel',
    confirm: 'Confirm',
    error: 'An error occurred',
    success: 'Success',
    offlineBanner: 'No network connection. Showing data from current session memory.',

    // Auth
    loginTitle: 'Employee Sign In',
    loginSubtitle: 'Access SentinelLAN security telemetry portal for your managed PC',
    orgCodeLabel: 'Organization Code',
    orgCodePlaceholder: 'e.g. sentinel-corp',
    emailLabel: 'Work Email',
    emailPlaceholder: 'user@company.com',
    passwordLabel: 'Password',
    passwordPlaceholder: 'Enter your password...',
    loginButton: 'Sign In',
    loggingIn: 'Authenticating...',
    loginFailed: 'Invalid credentials or organization code.',
    sessionExpired: 'Your session has expired. Please sign in again.',

    // Activation
    activationTitle: 'Activate Account',
    activationSubtitle: 'Set a secure password for your new SentinelLAN account.',
    activationTokenInvalid: 'Activation link is invalid, already used, or expired.',
    newPasswordLabel: 'New Password (min 12 characters)',
    confirmPasswordLabel: 'Confirm New Password',
    passwordMismatch: 'Passwords do not match.',
    passwordTooShort: 'Password must be between 12 and 128 characters.',
    activateButton: 'Activate Account',
    activating: 'Activating...',
    activationSuccess: 'Account activated successfully! Redirecting to login...',

    // Scan
    scanTitle: 'Scan Equipment QR Label',
    scanSubtitle: 'Point your camera at the SentinelLAN asset QR code on your computer',
    cameraPermissionTitle: 'Camera Permission Required',
    cameraPermissionRationale:
      'SentinelLAN uses the camera solely to scan QR asset labels attached to company computers. No photos or videos are captured, stored, or transmitted.',
    grantCameraPermission: 'Grant Camera Permission',
    openSettings: 'Open System Settings',
    manualCodeOption: 'Or enter code manually',
    manualCodePlaceholder: 'Enter QR code or prefix (e.g. QR-A1B2C3D4)',
    submitCode: 'Verify Code',
    pickImageOption: 'Select QR Image from Library',
    invalidQrCode: 'QR code does not match a valid SentinelLAN canonical asset URL or format.',
    scanResolving: 'Resolving device...',

    // My Device
    myDeviceTitle: 'My Computer',
    noDeviceAssignedTitle: 'No Device Assigned',
    noDeviceAssignedDesc: 'Your account is not linked to any managed computer yet. Please scan the QR label on your assigned PC or contact your IT Administrator.',
    deviceStatusOnline: 'Online',
    deviceStatusOffline: 'Offline',
    lastSeen: 'Last Seen',
    cpuUsage: 'CPU',
    ramUsage: 'RAM',
    diskUsage: 'Disk',
    systemSpecs: 'System Specifications',
    osVersion: 'Operating System',
    agentVersion: 'Agent Version',
    serialNumber: 'Serial Number',
    assetTag: 'Asset Tag',
    location: 'Location',
    appliedPolicy: 'Applied Policy',
    privacyManifestLink: 'View Privacy & Transparency Manifest',

    // Privacy Manifest
    privacyManifestTitle: 'Privacy & Transparency Manifest',
    privacyManifestSubtitle: 'SentinelLAN collects only least-privileged technical telemetry to protect company endpoints.',
    collectedDataTitle: 'Technical Data COLLECTED:',
    prohibitedDataTitle: 'Data STRICTLY PROHIBITED from collection:',
    agentPermissionsTitle: 'Windows Agent Permissions:',
    dataRetentionTitle: 'Data Retention Period:',
    days: 'days',

    // Incidents
    incidentsTitle: 'Reported Incidents',
    reportIncidentButton: 'Report New Incident',
    noIncidents: 'No incidents recorded for your computer.',
    incidentTitleLabel: 'Incident Title',
    incidentTitlePlaceholder: 'e.g. LAN cable disconnected, system freezing...',
    incidentDescLabel: 'Detailed Description',
    incidentDescPlaceholder: 'Describe the symptoms and time observed...',
    severityLabel: 'Severity',
    severityLow: 'Low',
    severityMedium: 'Medium',
    severityHigh: 'High',
    severityCritical: 'Critical',
    statusOpen: 'Open',
    statusInProgress: 'In Progress',
    statusResolved: 'Resolved',
    statusClosed: 'Closed',
    reportSubmitting: 'Submitting incident...',
    reportSuccess: 'Incident has been successfully reported.',
    resolutionNotesTitle: 'Resolution notes from technician:',

    // Account
    accountTitle: 'Account & Device',
    organization: 'Organization',
    userRole: 'Role',
    appVersion: 'App Version',
    switchLanguage: 'Language',
    logoutButton: 'Sign Out from This Device',
    logoutAllButton: 'Revoke All Sessions',
    logoutConfirmTitle: 'Sign Out Confirmation',
    logoutConfirmMsg: 'Are you sure you want to sign out from this mobile device?',
    logoutAllConfirmMsg: 'This action will revoke all mobile refresh sessions for this account.',
    loggingOut: 'Signing out...',
  },
};

type TranslationKey = keyof typeof translations.vi;

interface I18nContextType {
  lang: Language;
  setLang: (lang: Language) => void;
  t: (key: TranslationKey) => string;
}

const I18nContext = createContext<I18nContextType>({
  lang: 'vi',
  setLang: () => {},
  t: (key) => translations.vi[key] ?? key,
});

const LANGUAGE_KEY = 'sentinellan_pref_lang';

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Language>('vi');

  useEffect(() => {
    SecureStore.getItemAsync(LANGUAGE_KEY)
      .then((val) => {
        if (val === 'vi' || val === 'en') {
          setLangState(val);
        }
      })
      .catch(() => {});
  }, []);

  const setLang = (newLang: Language) => {
    setLangState(newLang);
    SecureStore.setItemAsync(LANGUAGE_KEY, newLang).catch(() => {});
  };

  const t = (key: TranslationKey): string => {
    return translations[lang]?.[key] ?? translations.vi[key] ?? key;
  };

  return <I18nContext.Provider value={{ lang, setLang, t }}>{children}</I18nContext.Provider>;
}

export const useI18n = () => useContext(I18nContext);
