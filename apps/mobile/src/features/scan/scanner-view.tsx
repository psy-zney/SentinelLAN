import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert,
  Modal,
  Platform,
  AppState,
  AppStateStatus,
} from 'react-native';
import { CameraView, useCameraPermissions, BarcodeScanningResult } from 'expo-camera';
import * as ImagePicker from 'expo-image-picker';
import * as Linking from 'expo-linking';
import { useRouter } from 'expo-router';
import { useI18n } from '../../lib/i18n';
import { useAppColors, AppButton, AppInput, AppCard } from '../../components/common';
import { spacing, typography, radii } from '../../theme/tokens';
import { parseScannedQrContent } from './qr-parser';
import { useAuth } from '../auth/auth-context';

export function ScannerView({ isActiveScreen = true }: { isActiveScreen?: boolean }) {
  const { t } = useI18n();
  const colors = useAppColors();
  const router = useRouter();
  const { apiClient } = useAuth();

  const [permission, requestPermission] = useCameraPermissions();
  const [manualModalOpen, setManualModalOpen] = useState(false);
  const [manualCode, setManualCode] = useState('');
  const [resolving, setResolving] = useState(false);
  const [errorNotice, setErrorNotice] = useState<string | null>(null);

  // AppState management to pause camera when in background
  const [appState, setAppState] = useState<AppStateStatus>(
    (AppState.currentState as AppStateStatus) || 'active'
  );


  useEffect(() => {
    const subscription = AppState.addEventListener('change', (nextAppState) => {
      setAppState(nextAppState);
    });
    return () => subscription.remove();
  }, []);


  // Debounce & deduplication mechanism
  const lastScannedCodeRef = useRef<string | null>(null);
  const lastScannedTimeRef = useRef<number>(0);

  const handleResolveCode = useCallback(
    async (rawScanned: string) => {
      setErrorNotice(null);

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
          router.replace('/(tabs)/my-device');
        } else {
          setErrorNotice(
            result.message ||
              'Mã QR hợp lệ nhưng thiết bị này không được phân công cho tài khoản của bạn.'
          );
        }
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : t('invalidQrCode');
        setErrorNotice(msg);
      } finally {
        setResolving(false);
      }
    },
    [apiClient, router, t]
  );

  const handleBarcodeScanned = useCallback(
    ({ data }: BarcodeScanningResult) => {
      if (resolving || !isActiveScreen || appState !== 'active') return;

      const now = Date.now();
      if (lastScannedCodeRef.current === data && now - lastScannedTimeRef.current < 2500) {
        return; // Dedup duplicate frames
      }

      lastScannedCodeRef.current = data;
      lastScannedTimeRef.current = now;

      handleResolveCode(data);
    },
    [resolving, isActiveScreen, appState, handleResolveCode]
  );

  // Scoped Image Picker fallback
  const handlePickImage = async () => {
    try {
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        allowsEditing: false,
        quality: 1,
      });

      if (!result.canceled && result.assets && result.assets.length > 0) {
        // Safe UX disclosure for offline/in-repo New Architecture environment:
        // Local QR decoding from static image requires OpenCV/ZXing native libraries not bundled in standard Expo Client.
        Alert.alert(
          'Chọn ảnh QR',
          'Đã chọn ảnh thành công. Với môi trường demo hiện tại, vui lòng sử dụng Camera quét trực tiếp hoặc Nhập mã tem thiết bị để phân giải nhanh nhất.',
          [{ text: 'Đồng ý', onPress: () => setManualModalOpen(true) }]
        );
      }
    } catch {
      setErrorNotice('Không thể mở thư viện ảnh');
    }
  };

  // State 1: Permission not determined or requested
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
            style={{ width: '100%' }}
          />
        </AppCard>
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
            style={[styles.utilityButton, { backgroundColor: colors.surface }]}
            onPress={handlePickImage}
          >
            <Text style={[styles.utilityText, { color: colors.ink }]}>🖼️ {t('pickImageOption')}</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Manual Code Input Modal */}
      <Modal
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
              autoCapitalize="characters"
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
                  handleResolveCode(manualCode);
                }}
                style={{ flex: 1 }}
              />
            </View>
          </AppCard>
        </View>
      </Modal>
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
