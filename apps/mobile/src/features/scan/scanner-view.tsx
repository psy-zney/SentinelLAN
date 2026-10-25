import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  Platform,
  AppState,
  AppStateStatus,
} from 'react-native';
import { Camera, CameraView, useCameraPermissions, BarcodeScanningResult } from 'expo-camera';
import * as ImagePicker from 'expo-image-picker';
import * as Linking from 'expo-linking';
import { useRouter } from 'expo-router';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppInput, AppCard } from '../../components/common';
import { spacing, typography, radii } from '../../theme/tokens';
import { parseScannedQrContent } from './qr-parser';
import { useAuth } from '../auth/auth-context';
import { ApiError } from '../../lib/api/client';
import { parseEnrollmentCode, type EnrollmentPreview } from './enrollment-code';

export function ScannerView({ isActiveScreen = true }: { isActiveScreen?: boolean }) {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient, user } = useAuth();

  const [permission, requestPermission] = useCameraPermissions();
  const [manualModalOpen, setManualModalOpen] = useState(false);
  const [manualCode, setManualCode] = useState('');
  const [enrollment, setEnrollment] = useState<EnrollmentPreview | null>(null);
  const [resolving, setResolving] = useState(false);
  const [imageScanning, setImageScanning] = useState(false);
  const [errorNotice, setErrorNotice] = useState<string | null>(null);

  // AppState management to pause camera when in background
  const [appState, setAppState] = useState<AppStateStatus>(
    (AppState.currentState as AppStateStatus) || 'active'
  );


  useEffect(() => {
    const subscription = AppState.addEventListener('change', (nextAppState) => {
      setAppState(nextAppState);
      if (nextAppState !== 'active') setEnrollment(null);
    });
    return () => subscription.remove();
  }, []);


  useEffect(() => { if (!isActiveScreen) setEnrollment(null); }, [isActiveScreen]);
  useEffect(() => { if (!enrollment) return; const timer = setTimeout(() => setEnrollment(null), Math.max(0, Date.parse(enrollment.expiresAt) - Date.now())); return () => clearTimeout(timer); }, [enrollment]);

  // Debounce & deduplication mechanism
  const lastScannedCodeRef = useRef<string | null>(null);
  const lastScannedTimeRef = useRef<number>(0);

  const handleResolveCode = useCallback(
    async (rawScanned: string) => {
      setErrorNotice(null);

      if (rawScanned.trim().startsWith('SL1.')) {
        if (user?.role !== 'Admin') { setErrorNotice('Mã cài đặt chỉ dành cho Admin phụ trách IT.'); return; }
        try { setEnrollment(parseEnrollmentCode(rawScanned.trim(), apiClient.serverOrigin)); }
        catch (error) { setErrorNotice(error instanceof Error ? error.message : 'Mã đăng ký không hợp lệ.'); }
        return;
      }
      // Parse and validate code origin/format
      const parseResult = parseScannedQrContent(rawScanned);
      if (!parseResult.valid || !parseResult.code) {
        setErrorNotice(parseResult.error || t('invalidQrCode'));
        return;
      }

      setResolving(true);
      try {
        const result = await apiClient.resolveQr(parseResult.code);
        if (result.authorized) {
          // Valid assigned equipment -> navigate to My Device tab
          router.replace(user?.role === 'Employee' ? '/(tabs)/my-device' : '/(tabs)');
        } else {
          setErrorNotice(
            result.message ||
              'Mã QR hợp lệ nhưng thiết bị này không được phân công cho tài khoản của bạn.'
          );
        }
      } catch (err: unknown) {
        if (err instanceof ApiError && err.status === 403) {
          setErrorNotice(t('qrNotAssigned'));
        } else if (err instanceof ApiError && err.status === 404) {
          setErrorNotice(t('invalidQrCode'));
        } else {
          setErrorNotice(err instanceof Error ? err.message : t('invalidQrCode'));
        }
      } finally {
        setResolving(false);
      }
    },
    [apiClient, router, t, user?.role]
  );

  const handleBarcodeScanned = useCallback(
    ({ data }: BarcodeScanningResult) => {
      if (enrollment || resolving || !isActiveScreen || appState !== 'active') return;

      const now = Date.now();
      if (lastScannedCodeRef.current === data && now - lastScannedTimeRef.current < 2500) {
        return; // Dedup duplicate frames
      }

      lastScannedCodeRef.current = data;
      lastScannedTimeRef.current = now;

      handleResolveCode(data);
    },
    [enrollment, resolving, isActiveScreen, appState, handleResolveCode]
  );

  const handlePickImage = async () => {
    if (imageScanning || resolving) return;
    try {
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        allowsEditing: false,
        quality: 1,
      });

      if (!result.canceled && result.assets && result.assets.length > 0) {
        setImageScanning(true);
        setErrorNotice(null);
        const scanned = await Camera.scanFromURLAsync(result.assets[0].uri, ['qr']);
        if (scanned.length === 0) {
          setErrorNotice(t('noQrInImage'));
        } else {
          await handleResolveCode(scanned[0].data);
        }
      }
    } catch {
      setErrorNotice(t('imageScanFailed'));
    } finally {
      setImageScanning(false);
    }
  };

  const manualCodeModal = (
    <Modal
      testID="manual-code-modal"
      visible={manualModalOpen}
      animationType="slide"
      transparent
      onRequestClose={() => setManualModalOpen(false)}
    >
      <View style={styles.modalBackdrop}>
        <AppCard style={styles.modalCard}>
          <Text style={[styles.modalTitle, { color: colors.ink }]}>{t('manualCodeOption')}</Text>
          <AppInput
            testID="manual-code-input"
            label={t('manualCodePlaceholder')}
            value={manualCode}
            onChangeText={setManualCode}
            placeholder="QR-..."
            autoCapitalize="none"
          />
          <View style={styles.modalActions}>
            <AppButton
              title={t('cancel')}
              variant="outline"
              onPress={() => setManualModalOpen(false)}
              style={{ flex: 1, marginRight: spacing.sm }}
            />
            <AppButton
              testID="submit-manual-code-button"
              title={t('submitCode')}
              loading={resolving}
              disabled={!manualCode.trim()}
              onPress={() => {
                setManualModalOpen(false);
                void handleResolveCode(manualCode);
              }}
              style={{ flex: 1 }}
            />
          </View>
        </AppCard>
      </View>
    </Modal>
  );

  // State 1: Permission not determined or requested
  if (enrollment) return <View style={[styles.permissionContainer, { backgroundColor: colors.background }]}><AppCard><Text style={[styles.modalTitle, { color: colors.ink }]}>Mã cài Agent cho một máy</Text><Text style={[styles.bodyText, { color: colors.ink }]}>Máy chủ: {enrollment.serverUrl}{'\n'}Hết hạn: {new Date(enrollment.expiresAt).toLocaleString('vi-VN')}{'\n'}Mở bộ cài công ty trên máy được phép và dán mã này. Mã là bí mật, chỉ dùng cho một máy; không gửi cho người khác hoặc dịch vụ QR công cộng.</Text><Text selectable style={[styles.bodyText, { color: colors.ink }]}>{enrollment.connectionCode}</Text><AppButton title="Ẩn mã và quét tiếp" onPress={() => setEnrollment(null)} /></AppCard></View>;

  if (!permission) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.background }]}>
        <Text style={[styles.bodyText, { color: colors.ink }]}>{t('loading')}</Text>
      </View>
    );
  }

  // State 2: Permission not granted yet (Rationale screen before prompt)
  if (!permission.granted) {
    return (
      <View style={[styles.permissionContainer, { backgroundColor: colors.background }]}>
        <AppCard style={styles.permissionCard}>
          <Text style={[styles.permissionIcon]}>📷</Text>
          <Text style={[styles.permissionTitle, { color: colors.ink }]}>
            {t('cameraPermissionTitle')}
          </Text>
          <Text style={[styles.permissionRationale, { color: colors.inkMuted }]}>
            {t('cameraPermissionRationale')}
          </Text>

          {permission.canAskAgain ? (
            <AppButton
              testID="grant-camera-button"
              title={t('grantCameraPermission')}
              onPress={requestPermission}
              style={{ width: '100%', marginBottom: spacing.md }}
            />
          ) : (
            <AppButton
              title={t('openSettings')}
              onPress={() => Linking.openSettings()}
              style={{ width: '100%', marginBottom: spacing.md }}
            />
          )}

          <AppButton
            title={t('manualCodeOption')}
            variant="outline"
            onPress={() => setManualModalOpen(true)}
            style={{ width: '100%', marginBottom: spacing.sm }}
          />

          <AppButton
            title={t('pickImageOption')}
            variant="secondary"
            onPress={handlePickImage}
            loading={imageScanning}
            style={{ width: '100%' }}
          />
          {errorNotice ? <Text style={[styles.errorText, { color: colors.danger }]}>{errorNotice}</Text> : null}
        </AppCard>
        {manualCodeModal}
      </View>
    );
  }



  const isCameraActive = isActiveScreen && appState === 'active';

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      {isCameraActive ? (
        <CameraView
          testID="camera-scanner-view"
          style={StyleSheet.absoluteFill}
          facing="back"
          barcodeScannerSettings={{
            barcodeTypes: ['qr'],
          }}
          onBarcodeScanned={handleBarcodeScanned}
        />
      ) : (
        <View style={[StyleSheet.absoluteFill, { backgroundColor: '#000' }]} />
      )}

      {/* Target scanning reticle */}
      <View style={styles.overlay}>
        <View style={styles.reticleContainer}>
          <View style={[styles.reticleBox, { borderColor: colors.primary }]} />
          <Text style={styles.reticleLabel}>
            {resolving ? t('scanResolving') : t('scanSubtitle')}
          </Text>
        </View>

        {/* Error notification banner */}
        {errorNotice ? (
          <View style={[styles.errorBanner, { backgroundColor: colors.dangerLight }]}>
            <Text style={[styles.errorText, { color: colors.danger }]}>{errorNotice}</Text>
          </View>
        ) : null}

        {/* Bottom utility controls */}
        <View style={styles.bottomControls}>
          <TouchableOpacity
            testID="manual-entry-button"
            accessibilityRole="button"
            accessibilityLabel={t('manualCodeOption')}
            style={[styles.utilityButton, { backgroundColor: colors.surface }]}
            onPress={() => setManualModalOpen(true)}
          >
            <Text style={[styles.utilityText, { color: colors.ink }]}>⌨️ {t('manualCodeOption')}</Text>
          </TouchableOpacity>

          <TouchableOpacity
            accessibilityRole="button"
            accessibilityLabel={t('pickImageOption')}
            disabled={imageScanning || resolving}
            style={[styles.utilityButton, { backgroundColor: colors.surface }]}
            onPress={handlePickImage}
          >
            <Text style={[styles.utilityText, { color: colors.ink }]}>🖼️ {imageScanning ? t('imageScanning') : t('pickImageOption')}</Text>
          </TouchableOpacity>
        </View>
      </View>

      {manualCodeModal}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  permissionContainer: {
    flex: 1,
    padding: spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
  },
  permissionCard: {
    width: '100%',
    alignItems: 'center',
  },
  permissionIcon: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  permissionTitle: {
    ...typography.titleMedium,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  permissionRationale: {
    ...typography.bodyMedium,
    textAlign: 'center',
    marginBottom: spacing.xl,
  },
  overlay: {
    position: 'absolute',
    left: 0,
    right: 0,
    top: 0,
    bottom: 0,
    justifyContent: 'space-between',
    padding: spacing.xl,
    paddingBottom: Platform.OS === 'ios' ? 40 : spacing.xl,
  },
  reticleContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  reticleBox: {
    width: 250,
    height: 250,
    borderWidth: 3,
    borderRadius: radii.lg,
    backgroundColor: 'transparent',
  },
  reticleLabel: {
    marginTop: spacing.md,
    color: '#ffffff',
    backgroundColor: 'rgba(0,0,0,0.65)',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    borderRadius: radii.sm,
    ...typography.caption,
    textAlign: 'center',
  },
  errorBanner: {
    padding: spacing.md,
    borderRadius: radii.md,
    marginBottom: spacing.md,
  },
  errorText: {
    ...typography.bodySmall,
    fontWeight: '600',
    textAlign: 'center',
  },
  bottomControls: {
    gap: spacing.sm,
  },
  utilityButton: {
    minHeight: 44,
    borderRadius: radii.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing.lg,
  },
  utilityText: {
    ...typography.label,
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'center',
    padding: spacing.xl,
  },
  modalCard: {
    width: '100%',
  },
  modalTitle: {
    ...typography.titleSmall,
    marginBottom: spacing.md,
  },
  modalActions: {
    flexDirection: 'row',
    marginTop: spacing.sm,
  },
  bodyText: {
    ...typography.bodyMedium,
  },
});
