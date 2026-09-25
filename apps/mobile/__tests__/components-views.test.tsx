import React from 'react';
import { render, fireEvent, waitFor, screen } from '@testing-library/react-native';
import { Text } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppButton, AppInput, AppBadge, OfflineBanner } from '../src/components/common';
import { ScannerView } from '../src/features/scan/scanner-view';
import { PrivacyManifestView } from '../src/features/my-device/privacy-manifest-view';
import { I18nProvider } from '../src/lib/i18n';
import { AuthProvider, useAuth } from '../src/features/auth/auth-context';
import { TokenVault } from '../src/lib/security/secure-store';
import { MobileApiClient } from '../src/lib/api/client';

function AuthStatusProbe() {
  const { status } = useAuth();
  return <Text testID="auth-status">{status}</Text>;
}

async function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <AuthProvider><AuthStatusProbe />{ui}</AuthProvider>
      </I18nProvider>
    </QueryClientProvider>
  );
  await waitFor(() => expect(screen.getByTestId('auth-status').props.children).not.toBe('bootstrapping'));
  return { ...result, queryClient };
}

describe('Component & View Smoke Tests', () => {
  describe('Offline session restoration', () => {
    afterEach(async () => { await TokenVault.wipeAll(); });

    it('preserves the session while offline and restores it after retry', async () => {
      await TokenVault.saveRefreshToken('test-refresh-token');
      global.fetch = jest.fn().mockRejectedValue(new TypeError('Network unavailable'));

      function RetryProbe() {
        const { retrySession } = useAuth();
        return <AppButton title="Retry session" onPress={() => { void retrySession(); }} />;
      }

      await renderWithProviders(<RetryProbe />);
      expect(screen.getByTestId('auth-status').props.children).toBe('offline');
      expect(await TokenVault.getRefreshToken()).toBe('test-refresh-token');

      global.fetch = jest.fn().mockImplementation(async (url: string) => ({
        ok: true,
        status: 200,
        json: async () => url.endsWith('/mobile/bootstrap')
          ? {
              minimumAppVersion: '1.0.0', latestAppVersion: '1.0.0',
              privacyManifestVersion: '1', maintenanceMode: false,
              supportEmail: 'support@example.test', supportedAuthSchemes: ['Bearer'],
            }
          : {
              accessToken: 'new-access-token', expiresIn: 900,
              refreshToken: 'rotated-refresh-token',
              refreshTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
              tokenType: 'Bearer',
            },
      }));

      fireEvent.press(screen.getByText('Retry session'));
      await waitFor(() => expect(screen.getByTestId('auth-status').props.children).toBe('authenticated'));
      expect(await TokenVault.getRefreshToken()).toBe('rotated-refresh-token');
    });
  });

  it('clears data from the previous account when signing out', async () => {
    function LogoutProbe() {
      const { logout } = useAuth();
      return <AppButton title="Sign out" onPress={() => { void logout(); }} />;
    }

    const { queryClient } = await renderWithProviders(<LogoutProbe />);
    queryClient.setQueryData(['my-device'], { hostname: 'previous-account-device' });
    jest.spyOn(MobileApiClient.prototype, 'logout').mockResolvedValueOnce();

    fireEvent.press(screen.getByText('Sign out'));
    await waitFor(() => expect(queryClient.getQueryData(['my-device'])).toBeUndefined());
    expect(screen.getByTestId('auth-status').props.children).toBe('unauthenticated');
  });

  describe('Common UI Components', () => {
    it('renders AppButton and handles press', () => {
      const onPressMock = jest.fn();
      render(<AppButton title="Test Button" onPress={onPressMock} />);

      const button = screen.getByText('Test Button');
      expect(button).toBeTruthy();
      fireEvent.press(button);
      expect(onPressMock).toHaveBeenCalledTimes(1);
    });

    it('renders AppInput with label and error', () => {
      render(
        <AppInput
          label="Email Address"
          placeholder="user@test.local"
          error="Email is required"
        />
      );

      expect(screen.getByText('Email Address')).toBeTruthy();
      expect(screen.getByPlaceholderText('user@test.local')).toBeTruthy();
      expect(screen.getByText('Email is required')).toBeTruthy();
    });

    it('renders AppBadge with text', () => {
      render(<AppBadge text="Online" variant="success" />);
      expect(screen.getByText('Online')).toBeTruthy();
    });

    it('renders OfflineBanner with alert role', () => {
      render(<OfflineBanner message="Offline mode active" />);
      expect(screen.getByText('⚠️ Offline mode active')).toBeTruthy();
    });
  });

  describe('ScannerView UX', () => {
    afterEach(() => {
      const { useCameraPermissions } = require('expo-camera');
      (useCameraPermissions as jest.Mock).mockReturnValue([
        { granted: true, canAskAgain: true, status: 'granted' },
        jest.fn(async () => ({ granted: true })),
      ]);
    });

    it('renders camera view and manual entry controls when permission is granted', async () => {
      await renderWithProviders(<ScannerView />);

      await waitFor(() => {
        expect(screen.getByTestId('camera-scanner-view')).toBeTruthy();
      });

      const manualBtn = screen.getByTestId('manual-entry-button');
      expect(manualBtn).toBeTruthy();
      fireEvent.press(manualBtn);

      await waitFor(() => {
        expect(screen.getByTestId('manual-code-input')).toBeTruthy();
        expect(screen.getByTestId('submit-manual-code-button')).toBeTruthy();
      });
    });

    it('renders camera permission rationale when permission is not granted', async () => {
      const { useCameraPermissions } = require('expo-camera');
      (useCameraPermissions as jest.Mock).mockReturnValue([
        { granted: false, canAskAgain: true, status: 'denied' },
        jest.fn(),
      ]);

      await renderWithProviders(<ScannerView />);

      await waitFor(() => {
        expect(screen.getByText(/Yêu cầu quyền truy cập Camera/i)).toBeTruthy();
        expect(screen.getByTestId('grant-camera-button')).toBeTruthy();
      });

      fireEvent.press(screen.getByText('Hoặc nhập mã thiết bị thủ công'));
      expect(screen.getByTestId('manual-code-input')).toBeTruthy();
    });

    it('decodes a selected image locally and reports when it has no QR code', async () => {
      const { Camera } = require('expo-camera');
      (Camera.scanFromURLAsync as jest.Mock).mockResolvedValueOnce([]);
      await renderWithProviders(<ScannerView />);

      fireEvent.press(screen.getByText(/Chọn ảnh QR từ thư viện/));

      await waitFor(() => {
        expect(Camera.scanFromURLAsync).toHaveBeenCalledWith('file://mock/qr-image.png', ['qr']);
        expect(screen.getByText(/Không tìm thấy mã QR trong ảnh/i)).toBeTruthy();
      });
    });
  });

  describe('PrivacyManifestView', () => {
    it('renders transparency headings and prohibited data boundaries', async () => {
      await renderWithProviders(<PrivacyManifestView />);

      await waitFor(() => {
        expect(screen.getByText(/Cam kết minh bạch dữ liệu/i)).toBeTruthy();
      });
      expect(screen.getByText(/Dữ liệu kỹ thuật ĐƯỢC thu thập/i)).toBeTruthy();
      expect(screen.getByText(/Dữ liệu NGHIÊM CẤM thu thập/i)).toBeTruthy();
      expect(screen.getByText(/Định vị vị trí địa lý \/ GPS/i)).toBeTruthy();
    });
  });

  describe('LoginView Interactions', () => {
    it('renders login form and responds to user input and submit', async () => {
      const { LoginView } = require('../src/features/auth/login-view');
      await renderWithProviders(<LoginView />);

      const orgInput = screen.getByTestId('org-code-input');
      const emailInput = screen.getByTestId('email-input');
      const passInput = screen.getByTestId('password-input');
      const submitBtn = screen.getByTestId('login-submit-button');

      expect(orgInput).toBeTruthy();
      expect(emailInput).toBeTruthy();
      expect(passInput).toBeTruthy();
      expect(submitBtn).toBeTruthy();

      fireEvent.changeText(orgInput, 'sentinel-corp');
      fireEvent.changeText(emailInput, 'user@sentinellan.local');
      fireEvent.changeText(passInput, 'SecurePass123!');

      fireEvent.press(submitBtn);

      await waitFor(() => {
        expect(screen.getByTestId('login-submit-button')).toBeTruthy();
      });
    });
  });

  it('keeps an activation token in memory after removing it from the route', async () => {
    const { useLocalSearchParams } = require('expo-router');
    (useLocalSearchParams as jest.Mock)
      .mockReturnValueOnce({ token: 'fresh-activation-token-123456' })
      .mockReturnValue({});
    const validate = jest.spyOn(MobileApiClient.prototype, 'validateActivationToken')
      .mockResolvedValueOnce({ valid: true, email: 'employee@example.test' });
    const { ActivateView } = require('../src/features/auth/activate-view');

    await renderWithProviders(<ActivateView />);

    await waitFor(() => expect(screen.getByTestId('new-password-input')).toBeTruthy());
    expect(validate).toHaveBeenCalledWith('fresh-activation-token-123456');
    expect(validate).toHaveBeenCalledTimes(1);
    validate.mockRestore();
  });

  describe('IncidentCreateView Interactions', () => {
    it('renders incident creation form with inputs and submit action', async () => {
      const { IncidentCreateView } = require('../src/features/incidents/incident-create-view');
      await renderWithProviders(<IncidentCreateView />);

      const titleInput = screen.getByTestId('incident-title-input');
      expect(titleInput).toBeTruthy();

      fireEvent.changeText(titleInput, 'Mất kết nối mạng LAN đột ngột');

      const submitBtn = screen.getByTestId('submit-incident-button');
      expect(submitBtn).toBeTruthy();
    });
  });

  describe('AccountView Interactions', () => {
    it('renders account settings, privacy links, and logout actions', async () => {
      const { AccountView } = require('../src/features/account/account-view');
      await renderWithProviders(<AccountView />);

      await waitFor(() => {
        expect(screen.getByTestId('logout-button')).toBeTruthy();
      });

      expect(screen.getByTestId('logout-all-button')).toBeTruthy();
      expect(screen.getByText(/Cam kết minh bạch dữ liệu/i)).toBeTruthy();
      expect(screen.getByText(/Phiên bản ứng dụng/i)).toBeTruthy();
    });
  });
});
