import React from 'react';
import { render, fireEvent, waitFor, screen } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppButton, AppInput, AppBadge, OfflineBanner } from '../src/components/common';
import { ScannerView } from '../src/features/scan/scanner-view';
import { PrivacyManifestView } from '../src/features/my-device/privacy-manifest-view';
import { I18nProvider } from '../src/lib/i18n';
import { AuthProvider } from '../src/features/auth/auth-context';

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <AuthProvider>{ui}</AuthProvider>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('Component & View Smoke Tests', () => {
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
    it('renders camera view and manual entry controls when permission is granted', async () => {
      renderWithProviders(<ScannerView />);

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
      (useCameraPermissions as jest.Mock).mockReturnValueOnce([
        { granted: false, canAskAgain: true, status: 'denied' },
        jest.fn(),
      ]);

      renderWithProviders(<ScannerView />);

      await waitFor(() => {
        expect(screen.getByText(/Yêu cầu quyền truy cập Camera/i)).toBeTruthy();
        expect(screen.getByTestId('grant-camera-button')).toBeTruthy();
      });
    });
  });

  describe('PrivacyManifestView', () => {
    it('renders transparency headings and prohibited data boundaries', async () => {
      renderWithProviders(<PrivacyManifestView />);

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
      renderWithProviders(<LoginView />);

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

  describe('IncidentCreateView Interactions', () => {
    it('renders incident creation form with inputs and submit action', async () => {
      const { IncidentCreateView } = require('../src/features/incidents/incident-create-view');
      renderWithProviders(<IncidentCreateView />);

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
      renderWithProviders(<AccountView />);

      await waitFor(() => {
        expect(screen.getByTestId('logout-button')).toBeTruthy();
      });

      expect(screen.getByTestId('logout-all-button')).toBeTruthy();
      expect(screen.getByText(/Cam kết minh bạch dữ liệu/i)).toBeTruthy();
      expect(screen.getByText(/Phiên bản ứng dụng/i)).toBeTruthy();
    });
  });
});



