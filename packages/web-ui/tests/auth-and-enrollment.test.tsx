// @vitest-environment jsdom
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { fakeApi, resetApi, renderProviders } from './helpers';
import { translations } from '../src/lib/i18n';
import { LoginForm } from '../src/components/login-form';
import { ActivateView } from '../src/components/activate-view';
import { EnrollmentPackagesPanel } from '../src/components/enrollment-packages-panel';
import { QrPortalView } from '../src/components/qr-portal-view';
import { AppShell } from '../src/components/app-shell';
import { QrScannerView } from '../src/components/qr-scanner-view';
import { DashboardView } from '../src/components/dashboard-view';
import * as Portal from '../src/lib/portal';

vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: vi.fn() }));

const navigation = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn(), search: new URLSearchParams(), pathname: '/dashboard' }));
const decoder = vi.hoisted(() => ({ constraints: vi.fn(), image: vi.fn(), stop: vi.fn() }));
vi.mock('@zxing/browser', () => ({ BrowserQRCodeReader: class { decodeFromConstraints = decoder.constraints; decodeFromImageUrl = decoder.image; } }));
vi.mock('next/navigation', () => ({ useRouter: () => navigation, usePathname: () => navigation.pathname, useSearchParams: () => navigation.search }));
vi.mock('next/link', () => ({ default: ({ children, href, onClick, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement>) => <a href={String(href)} {...props} onClick={event => { event.preventDefault(); onClick?.(event); }}>{children}</a> }));
vi.mock('@/lib/api-client', async () => { const { fakeApi } = await import('./helpers'); return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number, message = 'API unavailable') { super(message); } } }; });
const t = translations.vi;
beforeEach(() => {
  resetApi(); vi.clearAllMocks(); navigation.search = new URLSearchParams(); navigation.pathname = '/dashboard';
  window.history.replaceState({}, '', '/');
  Object.defineProperty(window, 'matchMedia', { configurable: true, value: vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })) });
});

describe('dashboard summary', () => {
  it('reports a failed dashboard load and retries without inventing device counts', async () => {
    fakeApi.dashboard.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ totalDevices: 2, onlineDevices: 1, offlineDevices: 1, openAlerts: 0, devices: [] });
    renderProviders(<DashboardView />);
    await screen.findByRole('alert'); expect(screen.queryByLabelText('Device summary')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: t.retry }));
    await screen.findByLabelText('Device summary');
    expect(fakeApi.dashboard).toHaveBeenCalledTimes(2);
    expect(screen.getByText(/0 \/ 2/)).toBeTruthy();
  });
  it('shows an empty confirmed dashboard without a pagination warning', async () => {
    fakeApi.dashboard.mockResolvedValue({ totalDevices: 0, onlineDevices: 0, offlineDevices: 0, openAlerts: 0, devices: [] });
    renderProviders(<DashboardView />); await screen.findByLabelText('Device summary');
    expect(screen.queryByText(/0 \/ 0/)).toBeNull();
  });
});

describe('browser sign-in boundaries', () => {
  function fill() {
    expect(document.querySelector('[name=organizationCode]')).toBeNull();
    fireEvent.change(document.querySelector('[name=email]')!, { target: { value: ' employee@example.test ' } });
    fireEvent.change(document.querySelector('[name=password]')!, { target: { value: 'exact password ' } });
  }
  it('trims account identifiers, preserves password bytes and routes a successful Admin session', async () => {
    fakeApi.login.mockResolvedValue({ role: 'Admin' }); renderProviders(<LoginForm />); fill();
    fireEvent.submit(document.querySelector('form')!);
    await waitFor(() => expect(fakeApi.login).toHaveBeenCalledWith('employee@example.test', 'exact password '));
    expect(navigation.push).toHaveBeenCalledWith('/dashboard'); expect(navigation.refresh).toHaveBeenCalled();
  });
  it('keeps failed credentials on the form and permits a subsequent retry', async () => {
    fakeApi.login.mockRejectedValueOnce(new Error('wrong password')).mockResolvedValue({ role: 'Admin' });
    renderProviders(<LoginForm />); fill(); fireEvent.submit(document.querySelector('form')!);
    await screen.findByRole('alert'); expect(navigation.push).not.toHaveBeenCalled();
    fireEvent.click(screen.getByLabelText('Hiện mật khẩu'));
    expect((document.querySelector('[name=password]') as HTMLInputElement).type).toBe('text');
    fireEvent.click(screen.getByLabelText('Ẩn mật khẩu'));
    fireEvent.click(screen.getByLabelText('Chuyển sang tiếng Anh'));
    fireEvent.submit(document.querySelector('form')!);
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/dashboard'));
    expect(fakeApi.login).toHaveBeenCalledTimes(2);
  });
  it('fills development helper accounts without submitting credentials automatically', () => {
    vi.stubEnv('NODE_ENV', 'development');
    renderProviders(<LoginForm />);
    fireEvent.click(screen.getByRole('button', { name: 'Quản trị viên' }));
    expect((document.querySelector('[name=email]') as HTMLInputElement).value).toBe('admin@sentinellan.local');
    fireEvent.click(screen.getByRole('button', { name: 'IT' }));
    expect((document.querySelector('[name=email]') as HTMLInputElement).value).toBe('technician@sentinellan.local');
    expect(fakeApi.login).not.toHaveBeenCalled(); vi.unstubAllEnvs();
  });
});

describe('one-time activation', () => {
  function fill(password: string, confirmation = password) {
    fireEvent.change(screen.getByLabelText(t.newPassword), { target: { value: password } });
    fireEvent.change(screen.getByLabelText(t.confirmNewPassword), { target: { value: confirmation } });
  }
  it('removes fragment credentials from the address bar and activates only after validation', async () => {
    window.history.replaceState({}, '', '/activate#token=private-one-time-token');
    fakeApi.validateActivationToken.mockResolvedValue({ valid: true, displayName: 'Employee', organizationName: 'Office' });
    fakeApi.activateAccount.mockResolvedValue({}); renderProviders(<ActivateView />);
    await screen.findByLabelText(t.newPassword);
    expect(window.location.hash).toBe(''); expect(fakeApi.validateActivationToken).toHaveBeenCalledWith('private-one-time-token');
    fill('long-password-123'); fireEvent.submit(document.querySelector('form')!);
    await screen.findByText(t.activationSuccess);
    expect(fakeApi.activateAccount).toHaveBeenCalledWith({ token: 'private-one-time-token', password: 'long-password-123', confirmPassword: 'long-password-123' });
  });
  it.each(['missing', 'invalid', 'unavailable'])('does not offer password setup for %s activation', async (failure) => {
    if (failure !== 'missing') navigation.search = new URLSearchParams('token=one-time');
    if (failure === 'unavailable') fakeApi.validateActivationToken.mockRejectedValue(new Error('offline'));
    else fakeApi.validateActivationToken.mockResolvedValue({ valid: false });
    renderProviders(<ActivateView />);
    await waitFor(() => expect(screen.queryByText('Đang xác thực liên kết kích hoạt...')).toBeNull());
    expect(screen.queryByLabelText(t.newPassword)).toBeNull(); expect(fakeApi.activateAccount).not.toHaveBeenCalled();
  });
  it('rejects short or mismatched passwords locally and preserves retry after server failure', async () => {
    navigation.search = new URLSearchParams('token=one-time'); fakeApi.validateActivationToken.mockResolvedValue({ valid: true });
    fakeApi.activateAccount.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({});
    renderProviders(<ActivateView />); await screen.findByLabelText(t.newPassword);
    fill('short'); fireEvent.submit(document.querySelector('form')!); expect(fakeApi.activateAccount).not.toHaveBeenCalled();
    fill('long-password-123', 'different-password'); fireEvent.submit(document.querySelector('form')!); expect(fakeApi.activateAccount).not.toHaveBeenCalled();
    fill('long-password-123'); fireEvent.submit(document.querySelector('form')!); await screen.findByText(t.activationFailed);
    fireEvent.submit(document.querySelector('form')!); await screen.findByText(t.activationSuccess);
    expect(fakeApi.activateAccount).toHaveBeenCalledTimes(2);
  });
  it('reports a consumed activation token distinctly in both languages', async () => {
    const { ApiError } = await import('../src/lib/api-client');
    navigation.search = new URLSearchParams('token=one-time');
    fakeApi.validateActivationToken.mockResolvedValue({ valid: true, organizationName: 'Office', email: 'staff@example.test' });
    fakeApi.activateAccount.mockRejectedValue(new ApiError(400));
    renderProviders(<ActivateView />); await screen.findByLabelText(t.newPassword); fill('long-password-123');
    fireEvent.submit(document.querySelector('form')!); await screen.findByText('Token không hợp lệ, đã được dùng hoặc đã hết hạn.');
    fireEvent.click(screen.getByRole('button', { name: 'EN' }));
    fireEvent.submit(document.querySelector('form')!); await screen.findByText('Token invalid, already used, or expired.');
    expect(screen.getByText('Company and account')).toBeTruthy();
  });
});

describe('private enrollment packages', () => {
  it('requires consent, selects distinct per-device credentials and hides them on request', async () => {
    fakeApi.enrollmentPackages.mockResolvedValue([1, 2].map(index => ({ connectionCode: `SL1.secret-${index}`, expiresAt: '2030-01-01T00:00:00Z', fileName: `machine-${index}.ps1`, script: `private-script-${index}` })));
    renderProviders(<EnrollmentPackagesPanel />);
    fireEvent.submit(document.querySelector('form')!); expect(fakeApi.enrollmentPackages).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Số máy'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('Nền tảng'), { target: { value: 'Linux' } });
    fireEvent.change(screen.getByLabelText('Lý do'), { target: { value: 'Authorized deployment' } });
    fireEvent.click(screen.getByRole('checkbox')); fireEvent.submit(document.querySelector('form')!);
    await screen.findByDisplayValue('SL1.secret-1'); expect(fakeApi.enrollmentPackages).toHaveBeenCalledWith(2, 'Linux', 'Authorized deployment');
    expect(screen.getByRole('img', { name: 'QR đăng ký thiết bị, giữ riêng' })).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Gói dành riêng cho máy'), { target: { value: '1' } });
    fireEvent.focus(screen.getByDisplayValue('SL1.secret-2'));
    fireEvent.click(screen.getByText('Ẩn các mã')); expect(screen.queryByDisplayValue('SL1.secret-2')).toBeNull();
    expect(screen.getByRole('alert').textContent).toContain('Đã ẩn mã');
  });
  it('reports issuance failure without exposing fabricated credentials', async () => {
    fakeApi.enrollmentPackages.mockRejectedValue(new Error('Enrollment unavailable'));
    renderProviders(<EnrollmentPackagesPanel />); fireEvent.click(screen.getByRole('checkbox')); fireEvent.submit(document.querySelector('form')!);
    await screen.findByText('Enrollment unavailable'); expect(screen.queryByLabelText('Mã kết nối')).toBeNull();
  });
  it('downloads the selected private script and manifest and revokes temporary URLs', async () => {
    const packages = [{ connectionCode: 'SL1.private-token', expiresAt: '2030-01-01T00:00:00Z', fileName: 'authorized-machine.ps1', script: 'private-script' }];
    fakeApi.enrollmentPackages.mockResolvedValue(packages);
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:private-download') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
    const names: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { names.push(this.download); });
    renderProviders(<EnrollmentPackagesPanel />); fireEvent.click(screen.getByRole('checkbox')); fireEvent.submit(document.querySelector('form')!);
    await screen.findByDisplayValue('SL1.private-token');
    vi.useFakeTimers();
    fireEvent.click(screen.getByRole('button', { name: 'Tải script riêng cho máy 1' }));
    fireEvent.click(screen.getByRole('button', { name: 'Tải toàn bộ lô (JSON riêng tư)' }));
    expect(names).toEqual(['authorized-machine.ps1', 'sentinellan-enrollment-manifest.json']);
    act(() => vi.advanceTimersByTime(1000));
    expect(URL.revokeObjectURL).toHaveBeenCalledTimes(2); vi.useRealTimers();
  });
});

describe('QR portal and protected shell', () => {
  it.each([true, false])('shows only public asset metadata when authenticated resolution is denied (online=%s)', async (online) => {
    fakeApi.resolveQr.mockResolvedValue({ authorized: false });
    fakeApi.resolvePublicQr.mockResolvedValue({ deviceName: 'Public Office PC', assetStatus: 'Active', assetTag: 'SERIAL-PREFIX', isOnline: online });
    renderProviders(<QrPortalView id="asset-code" />);
    await screen.findByText('Public Office PC');
    expect(screen.getByText('SERIAL-PREFIX')).toBeTruthy();
    expect(screen.getByText(online ? 'Đang trực tuyến' : 'Đang ngoại tuyến')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'EN' }));
    expect(screen.getByText(online ? 'Online' : 'Offline')).toBeTruthy();
    expect(document.querySelector('a[href="/login"]')).toBeTruthy();
  });
  it('routes an authenticated QR result without requesting public asset information', async () => {
    fakeApi.resolveQr.mockResolvedValue({ authorized: true, nextRoute: '/devices/device-1' }); renderProviders(<QrPortalView id="asset-code" />);
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/devices/device-1')); expect(fakeApi.resolvePublicQr).not.toHaveBeenCalled();
  });
  it.each(['invalid', 'unavailable'])('shows a missing-asset state for %s public QR lookup', async (failure) => {
    fakeApi.resolveQr.mockRejectedValue(new Error('No session'));
    if (failure === 'invalid') fakeApi.resolvePublicQr.mockResolvedValue({}); else fakeApi.resolvePublicQr.mockRejectedValue(new Error('offline'));
    renderProviders(<QrPortalView id="asset-code" />); await screen.findByText('Không tìm thấy thiết bị');
    expect(navigation.replace).not.toHaveBeenCalled();
  });
  it('does not render protected children when session verification fails', async () => {
    fakeApi.session.mockRejectedValue(new Error('session expired'));
    renderProviders(<AppShell title="Dashboard"><p>Private device data</p></AppShell>);
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/login'));
    expect(screen.queryByText('Private device data')).toBeNull();
  });
  it('renders authorized Admin navigation and logs out after server acknowledgement', async () => {
    fakeApi.session.mockResolvedValue({ role: 'Admin', displayName: 'Company Admin' });
    fakeApi.logout.mockResolvedValue(undefined);
    renderProviders(<AppShell title="Dashboard"><p>Private device data</p></AppShell>);
    await screen.findByText('Private device data');
    fireEvent.click(screen.getByRole('button', { name: t.logout }));
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/login'));
    expect(fakeApi.logout).toHaveBeenCalledTimes(1);
  });
  it('switches language and closes a mobile navigation drawer after choosing a destination', async () => {
    fakeApi.session.mockResolvedValue({ role: 'Admin', displayName: 'Organization Administrator' });
    renderProviders(<AppShell title="Devices" subtitleKey="scanSubtitle"><p>Private device data</p></AppShell>);
    await screen.findByText('Private device data');
    expect(screen.queryByText('Organization Administrator')).toBeNull();
    expect(screen.getByText('Quản trị hệ thống')).toBeTruthy();
    expect(screen.getAllByText('Quản trị nội bộ')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'Mở menu' }));
    expect(screen.getByRole('dialog')).toBeTruthy();
    const link = screen.getByRole('dialog').querySelector('a[href="/devices"]')!;
    fireEvent.click(link);
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    fireEvent.click(screen.getByRole('button', { name: 'Chuyển sang tiếng Anh' }));
    expect(screen.getByRole('button', { name: 'Switch to Vietnamese' })).toBeTruthy();
    expect(screen.getByText('System administrator')).toBeTruthy();
    expect(screen.getAllByText('Internal administration')).toHaveLength(2);
  });
  it('renders Employee navigation only after portal and role authorization permit it', async () => {
    vi.spyOn(Portal, 'portalAllows').mockReturnValue(true);
    fakeApi.session.mockResolvedValue({ role: 'Employee', displayName: 'Office Employee' });
    navigation.pathname = '/employee/my-device/history';
    renderProviders(<AppShell title="Your Assigned Device" allowedRoles={['Employee']}><p>My assigned device</p></AppShell>);
    await screen.findByText('My assigned device');
    expect(document.querySelector('.mobile-bottom-nav')).toBeTruthy(); expect(screen.queryByRole('button', { name: 'Mở menu' })).toBeNull();
    expect(document.querySelector('a[href="/my-device"][aria-current="page"]')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Chuyển sang tiếng Anh' }));
    expect(screen.getAllByText('Employee workspace')).toHaveLength(2);
    expect(screen.queryByText('People & Access')).toBeNull();
  });
  it('redirects authenticated users denied by the view role restriction before showing data', async () => {
    vi.spyOn(Portal, 'portalAllows').mockReturnValue(true);
    fakeApi.session.mockResolvedValue({ role: 'Employee', displayName: 'Employee' });
    renderProviders(<AppShell title="People & Access"><p>Private user accounts</p></AppShell>);
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/my-device'));
    expect(screen.queryByText('Private user accounts')).toBeNull();
  });
});

describe('manual QR scanner boundaries', () => {
  function submit(value: string) {
    fireEvent.change(document.querySelector('#manual-code')!, { target: { value } });
    fireEvent.submit(document.querySelector('form')!);
  }
  it('rejects foreign URLs before resolving an asset', async () => {
    renderProviders(<QrScannerView />); submit('https://evil.test/qr/demo-qr-asset-employee-pc-2026');
    await screen.findByText('Mã nhập không hợp lệ hoặc URL không thuộc hệ thống này.');
    expect(fakeApi.resolveQr).not.toHaveBeenCalled(); expect(navigation.push).not.toHaveBeenCalled();
  });
  it.each([true, false])('uses API authorization to route a manually entered asset (authorized=%s)', async (authorized) => {
    fakeApi.resolveQr.mockResolvedValue({ authorized, nextRoute: authorized ? '/devices/device-1' : undefined });
    renderProviders(<QrScannerView />); submit('demo-qr-asset-employee-pc-2026');
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(authorized ? '/devices/device-1' : '/qr/demo-qr-asset-employee-pc-2026'));
    expect(fakeApi.resolveQr).toHaveBeenCalledWith('demo-qr-asset-employee-pc-2026');
  });
  it('falls back to the public QR page after authorization fails', async () => {
    fakeApi.resolveQr.mockRejectedValue(new Error('No session'));
    renderProviders(<QrScannerView />); submit('demo-qr-asset-employee-pc-2026');
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/qr/demo-qr-asset-employee-pc-2026'));
  });
  it('explains unavailable camera APIs and permits retry without requesting asset data', async () => {
    renderProviders(<QrScannerView />); fireEvent.click(screen.getByRole('button', { name: t.startCamera }));
    await screen.findByText(/getUserMedia is not supported/);
    fireEvent.click(screen.getByRole('button', { name: t.retry }));
    await screen.findByText(/getUserMedia is not supported/); expect(fakeApi.resolveQr).not.toHaveBeenCalled();
  });
  it('cleans up the native camera stream when switching cameras and unmounting', async () => {
    const stop = vi.fn(); const getUserMedia = vi.fn().mockResolvedValue({ getTracks: () => [{ stop }] });
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia } });
    Object.defineProperty(window, 'BarcodeDetector', { configurable: true, value: class { detect = vi.fn().mockResolvedValue([]); } });
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    vi.spyOn(window, 'requestAnimationFrame').mockReturnValue(42);
    vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {});
    const view = renderProviders(<QrScannerView />); fireEvent.click(screen.getByRole('button', { name: t.startCamera }));
    await screen.findByRole('button', { name: t.switchCamera });
    fireEvent.click(screen.getByRole('button', { name: t.switchCamera }));
    await waitFor(() => expect(getUserMedia).toHaveBeenCalledTimes(2));
    expect(getUserMedia.mock.calls[1][0].video.facingMode).toBe('user');
    await screen.findByRole('button', { name: t.switchCamera }); view.unmount();
    expect(stop).toHaveBeenCalledTimes(2); expect(window.cancelAnimationFrame).toHaveBeenCalledWith(42);
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
  });
  it('resolves a QR from a user-selected image and closes the decoded bitmap', async () => {
    const close = vi.fn(); const detect = vi.fn().mockResolvedValue([{ rawValue: 'demo-qr-asset-employee-pc-2026' }]);
    Object.defineProperty(window, 'BarcodeDetector', { configurable: true, value: class { detect = detect; } });
    vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ close }));
    fakeApi.resolveQr.mockResolvedValue({ authorized: true, nextRoute: '/devices/device-1' });
    renderProviders(<QrScannerView />);
    fireEvent.change(document.querySelector('input[type=file]')!, { target: { files: [new File(['image'], 'qr.png', { type: 'image/png' })] } });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/devices/device-1'));
    expect(close).toHaveBeenCalledTimes(1);
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
    vi.unstubAllGlobals();
  });
  it('uses the camera fallback and stops its controls after decoding an asset', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn() } });
    decoder.constraints.mockResolvedValue({ stop: decoder.stop });
    fakeApi.resolveQr.mockResolvedValue({ authorized: false });
    const view = renderProviders(<QrScannerView />);
    fireEvent.click(screen.getByRole('button', { name: t.startCamera }));
    await screen.findByRole('button', { name: t.switchCamera });
    const callback = decoder.constraints.mock.calls[0][2];
    callback({ getText: () => 'demo-qr-asset-employee-pc-2026' });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/qr/demo-qr-asset-employee-pc-2026'));
    expect(decoder.stop).toHaveBeenCalledTimes(1); view.unmount();
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
  });
  it('reports denied camera permission without entering a scanning state', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn() } });
    decoder.constraints.mockRejectedValue(Object.assign(new Error('Permission denied'), { name: 'NotAllowedError' }));
    renderProviders(<QrScannerView />); fireEvent.click(screen.getByRole('button', { name: t.startCamera }));
    await screen.findByText(t.cameraDeniedHelp); expect(screen.queryByRole('button', { name: t.switchCamera })).toBeNull();
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
  });
  it.each([true, false])('releases the uploaded image URL after fallback decoding (success=%s)', async (success) => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:private-qr') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
    if (success) decoder.image.mockResolvedValue({ getText: () => 'demo-qr-asset-employee-pc-2026' });
    else decoder.image.mockRejectedValue(new Error('No barcode'));
    fakeApi.resolveQr.mockResolvedValue({ authorized: false });
    renderProviders(<QrScannerView />);
    fireEvent.change(document.querySelector('input[type=file]')!, { target: { files: [new File(['image'], 'qr.png')] } });
    await waitFor(() => expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:private-qr'));
    if (success) await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/qr/demo-qr-asset-employee-pc-2026'));
    else { expect(fakeApi.resolveQr).not.toHaveBeenCalled(); expect(navigation.push).not.toHaveBeenCalled(); }
  });
  it('ignores a failed native frame, continues scanning and stops once an asset is decoded', async () => {
    const stop = vi.fn();
    const detect = vi.fn().mockRejectedValueOnce(new Error('Frame not ready')).mockResolvedValueOnce([]).mockResolvedValueOnce([{ rawValue: 'demo-qr-asset-employee-pc-2026' }]);
    Object.defineProperty(window, 'BarcodeDetector', { configurable: true, value: class { detect = detect; } });
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop }] }) } });
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'readyState', 'get').mockReturnValue(2);
    const frame = vi.spyOn(window, 'requestAnimationFrame').mockReturnValue(23);
    vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {});
    fakeApi.resolveQr.mockResolvedValue({ authorized: true, nextRoute: '/devices/device-1' });
    renderProviders(<QrScannerView />); fireEvent.click(screen.getByRole('button', { name: t.startCamera }));
    await screen.findByRole('button', { name: t.switchCamera });
    for (let index = 0; index < 3; index++) await act(async () => { frame.mock.calls[index][0](0); });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith('/devices/device-1'));
    expect(detect).toHaveBeenCalledTimes(3); expect(stop).toHaveBeenCalledTimes(1);
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
  });
});
