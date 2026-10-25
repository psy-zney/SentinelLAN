import React from 'react';
import { AppState } from 'react-native';
import { Camera, useCameraPermissions } from 'expo-camera';
import * as ImagePicker from 'expo-image-picker';
import * as Linking from 'expo-linking';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Auth from '../src/features/auth/auth-context';
import { ScannerView } from '../src/features/scan/scanner-view';
import { I18nProvider } from '../src/lib/i18n';
import { ApiError, MobileApiClient } from '../src/lib/api/client';

const code = 'demo-qr-asset-employee-pc-2026';
describe('Scanner authorization and recovery', () => {
  let api: MobileApiClient;
  const replace = jest.fn();
  let user: { role: 'Employee' | 'Admin' };
  async function mount() { return await render(<I18nProvider><ScannerView /></I18nProvider>); }
  async function manual(value: string) {
    await fireEvent.press(screen.getByTestId('manual-entry-button'));
    await fireEvent.changeText(screen.getByTestId('manual-code-input'), value);
    await fireEvent.press(screen.getByTestId('submit-manual-code-button'));
  }
  beforeEach(() => {
    jest.clearAllMocks(); user = { role: 'Employee' }; api = new MobileApiClient();
    jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ replace });
    jest.spyOn(Auth, 'useAuth').mockImplementation(() => ({ status: 'authenticated', user, apiClient: api } as ReturnType<typeof Auth.useAuth>));
    jest.mocked(useCameraPermissions).mockReturnValue([{ granted: true, canAskAgain: true } as never, jest.fn(), jest.fn()]);
    jest.mocked(ImagePicker.launchImageLibraryAsync).mockResolvedValue({ canceled: false, assets: [{ uri: 'file:///qr.png' } as ImagePicker.ImagePickerAsset] });
    jest.mocked(Camera.scanFromURLAsync).mockResolvedValue([]);
  });
  afterEach(() => jest.restoreAllMocks());

  it.each(['Employee', 'Admin'] as const)('routes an authorized %s through the API rather than a QR-provided URL', async (role) => {
    user.role = role;
    const resolve = jest.spyOn(api, 'resolveQr').mockResolvedValue({ authorized: true } as Awaited<ReturnType<MobileApiClient['resolveQr']>>);
    await mount(); await manual(code);
    await waitFor(() => expect(replace).toHaveBeenCalledWith(role === 'Employee' ? '/(tabs)/my-device' : '/(tabs)'));
    expect(resolve).toHaveBeenCalledWith(code);
  });
  it.each([403, 404, 503])('keeps the user on scanner when QR resolution fails with HTTP %s', async (status) => {
    const resolve = jest.spyOn(api, 'resolveQr').mockRejectedValue(new ApiError(status, status === 503 ? 'Server unavailable' : undefined));
    await mount(); await manual(code);
    await waitFor(() => expect(resolve).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByTestId('manual-entry-button')).toBeTruthy());
    expect(replace).not.toHaveBeenCalled();
    if (status === 503) await screen.findByText('Server unavailable');
  });
  it('shows the server assignment explanation and never navigates an unauthorized result', async () => {
    jest.spyOn(api, 'resolveQr').mockResolvedValue({ authorized: false, message: 'This machine belongs to another employee' } as Awaited<ReturnType<MobileApiClient['resolveQr']>>);
    await mount(); await manual(code);
    await screen.findByText('This machine belongs to another employee');
    expect(replace).not.toHaveBeenCalled();
  });
  it('rejects enrollment codes for ordinary employees before any API call', async () => {
    const resolve = jest.spyOn(api, 'resolveQr');
    await mount(); await manual('SL1.invalid');
    await screen.findByText('Mã cài đặt chỉ dành cho Admin phụ trách IT.');
    expect(resolve).not.toHaveBeenCalled();
  });
  it('reports malformed enrollment to Admin and foreign QR input before any API call', async () => {
    user.role = 'Admin'; const resolve = jest.spyOn(api, 'resolveQr');
    await mount(); await manual('SL1.invalid');
    expect(replace).not.toHaveBeenCalled(); expect(resolve).not.toHaveBeenCalled();
    await manual('https://evil.test/device?id=secret');
    expect(resolve).not.toHaveBeenCalled();
  });
  it('deduplicates repeated camera frames while retaining the error feedback', async () => {
    const resolve = jest.spyOn(api, 'resolveQr').mockResolvedValue({ authorized: false, message: 'Not assigned' } as Awaited<ReturnType<MobileApiClient['resolveQr']>>);
    await mount();
    await fireEvent(screen.getByTestId('camera-scanner-view'), 'barcodeScanned', { data: code });
    await screen.findByText('Not assigned');
    await fireEvent(screen.getByTestId('camera-scanner-view'), 'barcodeScanned', { data: code });
    expect(resolve).toHaveBeenCalledTimes(1);
  });
  it('reads QR from an employee-selected image and resolves it through the API', async () => {
    const resolve = jest.spyOn(api, 'resolveQr').mockResolvedValue({ authorized: true } as Awaited<ReturnType<MobileApiClient['resolveQr']>>);
    jest.mocked(Camera.scanFromURLAsync).mockResolvedValue([{ data: code } as never]);
    await mount();
    await fireEvent.press(screen.getAllByRole('button')[1]);
    await waitFor(() => expect(resolve).toHaveBeenCalledWith(code));
    expect(Camera.scanFromURLAsync).toHaveBeenCalledWith('file:///qr.png', ['qr']);
  });
  it.each(['empty', 'decoder', 'cancelled'])('handles an image %s result without pretending to resolve a device', async (mode) => {
    const resolve = jest.spyOn(api, 'resolveQr');
    if (mode === 'decoder') jest.mocked(Camera.scanFromURLAsync).mockRejectedValue(new Error('decoder unavailable'));
    if (mode === 'cancelled') jest.mocked(ImagePicker.launchImageLibraryAsync).mockResolvedValue({ canceled: true, assets: null });
    await mount(); await fireEvent.press(screen.getAllByRole('button')[1]);
    await waitFor(() => expect(ImagePicker.launchImageLibraryAsync).toHaveBeenCalled());
    if (mode !== 'cancelled') await waitFor(() => expect(Camera.scanFromURLAsync).toHaveBeenCalled());
    expect(resolve).not.toHaveBeenCalled();
  });
  it('lets a permanently denied camera permission open settings while keeping manual entry available', async () => {
    jest.mocked(useCameraPermissions).mockReturnValue([{ granted: false, canAskAgain: false } as never, jest.fn(), jest.fn()]);
    await mount(); await fireEvent.press(screen.getAllByRole('button')[0]);
    expect(Linking.openSettings).toHaveBeenCalled();
  });
  it('closes the manual entry modal via the OS close action and pauses camera in background', async () => {
    const listener = jest.spyOn(AppState, 'addEventListener');
    await mount(); await fireEvent.press(screen.getByTestId('manual-entry-button'));
    await fireEvent(screen.getByTestId('manual-code-modal'), 'requestClose');
    expect(screen.queryByTestId('manual-code-input')).toBeNull();
    await act(() => listener.mock.calls[0][1]('background'));
    expect(screen.queryByTestId('camera-scanner-view')).toBeNull();
  });
});
