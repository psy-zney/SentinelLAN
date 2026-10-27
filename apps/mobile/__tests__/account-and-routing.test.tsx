import React from 'react';
import { Alert } from 'react-native';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Router from 'expo-router';
import * as Auth from '../src/features/auth/auth-context';
import * as I18n from '../src/lib/i18n';
import { AccountView } from '../src/features/account/account-view';
import { CompanyUsersView } from '../src/features/company-admin/users-view';
import { OperatorView } from '../src/features/it-operator/operator-view';
import { MobileApiClient } from '../src/lib/api/client';
import TabsLayout from '../src/app/(tabs)/_layout';
import { LoginView } from '../src/features/auth/login-view';
import { ActivateView } from '../src/features/auth/activate-view';

jest.mock('expo-router', () => {
  const React = require('react') as typeof import('react');
  const { Text, View } = require('react-native') as typeof import('react-native');
  const Tabs = Object.assign(({ children }: { children: React.ReactNode }) => <View>{children}</View>, {
    Screen: ({ name, options }: { name: string; options: { href?: string | null; tabBarIcon: (props: { color: string; size: number }) => React.ReactNode } }) =>
      <View testID={`tab-${name}`} accessibilityLabel={options.href === null ? 'hidden' : 'visible'}>{options.tabBarIcon({ color: '#000000', size: 24 })}</View>,
  });
  return {
    useRouter: jest.fn(), useLocalSearchParams: jest.fn(() => ({})), Tabs,
    useFocusEffect: (callback: () => (() => void) | undefined) => React.useEffect(callback, [callback]),
    Redirect: ({ href }: { href: string }) => <Text testID="route-redirect">{href}</Text>,
  };
});

jest.mock('react-native-safe-area-context', () => ({ useSafeAreaInsets: () => ({ top: 0, bottom: 12, left: 0, right: 0 }) }));
jest.mock('lucide-react-native', () => {
  const Icon = () => null;
  return { LayoutDashboard: Icon, QrCode: Icon, Monitor: Icon, Headset: Icon, UserRound: Icon, ChevronRight: Icon, ChevronUp: Icon, Server: Icon, UsersRound: Icon };
});

const id = '00000000-0000-4000-8000-000000000001';
const peerId = '00000000-0000-4000-8000-000000000002';
const queries: QueryClient[] = [];
const router = { push: jest.fn(), replace: jest.fn(), back: jest.fn(), setParams: jest.fn() };

function session(api: MobileApiClient, role: 'Admin' | 'Employee' = 'Admin') {
  const value = {
    status: 'authenticated' as const,
    user: { id, email: 'admin@example.test', displayName: 'Company Admin', role, organizationId: id, organizationCode: 'demo' },
    apiClient: api,
    login: jest.fn(), logout: jest.fn().mockResolvedValue(undefined), logoutAll: jest.fn().mockResolvedValue(undefined),
    retrySession: jest.fn(), dismissSessionExpired: jest.fn(),
  };
  jest.spyOn(Auth, 'useAuth').mockReturnValue(value);
  return value;
}

async function wrap(view: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  queries.push(client);
  return await render(<QueryClientProvider client={client}>{view}</QueryClientProvider>);
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(accept => { resolve = accept; });
  return { promise, resolve };
}

async function confirmAlert() {
  const calls = jest.mocked(Alert.alert).mock.calls;
  const buttons = calls[calls.length - 1]?.[2];
  const confirm = buttons?.find(button => button.style === 'destructive' || button.text === 'Xác nhận');
  expect(confirm?.onPress).toBeDefined();
  await act(async () => { await confirm?.onPress?.(); });
}

beforeEach(() => {
  jest.spyOn(Router, 'useRouter').mockReturnValue(router as unknown as ReturnType<typeof Router.useRouter>);
  jest.spyOn(Alert, 'alert').mockImplementation(() => {});
  router.push.mockClear(); router.replace.mockClear();
});

afterEach(() => {
  queries.splice(0).forEach(client => client.clear());
  jest.useRealTimers();
  jest.restoreAllMocks();
});

test('account shows internal user details without exposing a company code or bootstrap name', async () => {
  const auth = session(new MobileApiClient('https://test.local'));
  auth.user.displayName = 'Organization Administrator';
  jest.spyOn(I18n, 'useI18n').mockReturnValue({ lang: 'vi', setLang: jest.fn(), t: key => key });
  await wrap(<AccountView />);
  expect(screen.getByText('Quản trị hệ thống')).toBeTruthy();
  expect(screen.getByText('Quản trị viên')).toBeTruthy();
  expect(screen.queryByText('Organization Administrator')).toBeNull();
  expect(screen.queryByText('demo')).toBeNull();
  expect(screen.queryByText('organization:')).toBeNull();
});

test('account waits for logout confirmation and completes navigation only after local logout', async () => {
  const auth = session(new MobileApiClient('https://test.local'));
  const pending = deferred<void>();
  auth.logout.mockReturnValue(pending.promise);
  await wrap(<AccountView />);
  expect(screen.getByText('Company Admin')).toBeTruthy();
  await fireEvent.press(screen.getByTestId('logout-button'));
  expect(auth.logout).not.toHaveBeenCalled();
  const confirm = jest.mocked(Alert.alert).mock.calls[0][2]?.find(button => button.style === 'destructive');
  let completion: unknown;
  await act(() => { completion = confirm?.onPress?.(); });
  expect(auth.logout).toHaveBeenCalledTimes(1);
  expect(router.replace).not.toHaveBeenCalled();
  await act(async () => { pending.resolve(); await completion; });
  expect(router.replace).toHaveBeenCalledWith('/login');
});

test('logout-all failure keeps the account visible, offers retry, and navigates after confirmed success', async () => {
  const auth = session(new MobileApiClient('https://test.local'));
  auth.logoutAll.mockRejectedValueOnce(new Error('offline'));
  await wrap(<AccountView />);
  await fireEvent.press(screen.getByTestId('logout-all-button'));
  await confirmAlert();
  expect(Alert.alert).toHaveBeenLastCalledWith('Chưa thu hồi được các phiên', expect.stringContaining('Máy chủ chưa xác nhận'));
  expect(router.replace).not.toHaveBeenCalled();
  expect(screen.getByText('Company Admin')).toBeTruthy();
  await fireEvent.press(screen.getByTestId('logout-all-button'));
  await confirmAlert();
  expect(auth.logoutAll).toHaveBeenCalledTimes(2);
  expect(router.replace).toHaveBeenCalledWith('/login');
});

test('account language and privacy controls select the requested destination', async () => {
  session(new MobileApiClient('https://test.local'), 'Employee');
  const setLang = jest.fn();
  jest.spyOn(I18n, 'useI18n').mockReturnValue({ lang: 'en', setLang, t: key => key });
  await wrap(<AccountView />);
  await fireEvent.press(screen.getByLabelText('Tiếng Việt'));
  await fireEvent.press(screen.getByLabelText('English'));
  expect(setLang.mock.calls).toEqual([['vi'], ['en']]);
  await fireEvent.press(screen.getByText('privacyManifestTitle'));
  expect(router.push).toHaveBeenCalledWith('/privacy-manifest');
});

test('account creation requires reason and confirmation, displays a private invitation and clears it', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  jest.spyOn(api, 'operatorUsers').mockResolvedValue([]);
  const create = jest.spyOn(api, 'createCompanyUser').mockResolvedValue({ activationUrl: '/activate#token=one-use' });
  await wrap(<CompanyUsersView />);
  await fireEvent.press(screen.getByText('Tạo link kích hoạt'));
  expect(Alert.alert).not.toHaveBeenCalled();
  await fireEvent.changeText(screen.getByLabelText('Lý do cấp hoặc đổi quyền truy cập'), 'New office employee');
  await fireEvent.changeText(screen.getByLabelText('Họ tên'), 'Office Employee');
  await fireEvent.changeText(screen.getByLabelText('Email'), 'office@example.test');
  await fireEvent.press(screen.getByText('Quản trị viên'));
  await fireEvent.press(screen.getByText('Nhân viên'));
  await fireEvent.press(screen.getByText('Tạo link kích hoạt'));
  expect(create).not.toHaveBeenCalled();
  await confirmAlert();
  await waitFor(() => expect(screen.getByLabelText('Link kích hoạt').props.value).toBe('/employee/activate#token=one-use'));
  expect(create).toHaveBeenCalledWith({ email: 'office@example.test', displayName: 'Office Employee', role: 'Employee', reason: 'New office employee', confirmed: true });
  expect(screen.getByLabelText('Email').props.value).toBe('');
  await fireEvent.press(screen.getByText('Đã lưu — đóng'));
  expect(screen.queryByLabelText('Link kích hoạt')).toBeNull();
});

test('user state changes preserve reason, recover from failures, and use the next page', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  const peer = { id: peerId, email: 'peer@example.test', displayName: 'Peer Account', role: 'Employee' as const, status: 'Active' as const, createdAt: '2026-10-23T00:00:00Z' };
  const users = jest.spyOn(api, 'operatorUsers').mockImplementation(async offset => offset ? [] : [peer, ...Array.from({ length: 99 }, (_, index) => ({ ...peer, id: `pending-${index}`, displayName: `Pending ${index}`, status: 'PendingActivation' as const }))]);
  const change = jest.spyOn(api, 'setCompanyUserStatus').mockRejectedValueOnce(new Error('Server refused')).mockResolvedValueOnce({ ...peer, status: 'Locked' });
  await wrap(<CompanyUsersView />);
  await waitFor(() => expect(screen.getByText('Peer Account')).toBeTruthy());
  await fireEvent.changeText(screen.getByLabelText('Lý do cấp hoặc đổi quyền truy cập'), 'Lost access badge');
  await fireEvent.press(screen.getByText('Khóa tài khoản'));
  await confirmAlert();
  await waitFor(() => expect(screen.getByText(/Server refused/)).toBeTruthy());
  expect(change).toHaveBeenCalledWith(peerId, 'Locked', 'Lost access badge');
  await fireEvent.press(screen.getByText('Thử lại'));
  await fireEvent.press(screen.getByText('Khóa tài khoản'));
  await confirmAlert();
  await waitFor(() => expect(screen.queryByText(/Server refused/)).toBeNull());
  await fireEvent.press(screen.getByText('Trang sau'));
  await waitFor(() => expect(users).toHaveBeenCalledWith(100));
  expect(screen.getByText('Trang 2')).toBeTruthy();
  await fireEvent.press(screen.getByText('Trang trước'));
  await waitFor(() => expect(users).toHaveBeenCalledWith(0));
});

test('operator loading and failed fetch recover together and route to support or company users', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  const dashboard = jest.spyOn(api, 'operatorDashboard').mockRejectedValueOnce(new Error('Dashboard offline')).mockResolvedValue({ totalDevices: 0, onlineDevices: 0, offlineDevices: 0, openAlerts: 0 });
  const devices = jest.spyOn(api, 'operatorDevices').mockResolvedValue([]);
  await wrap(<OperatorView />);
  expect(screen.getByText('Đang tải tình trạng công ty…')).toBeTruthy();
  await waitFor(() => expect(screen.getByText(/Dashboard offline/)).toBeTruthy());
  await fireEvent.press(screen.getByText('Thử lại'));
  await waitFor(() => expect(screen.getByText('0/0 máy đang kết nối')).toBeTruthy());
  expect(dashboard).toHaveBeenCalledTimes(2);
  expect(devices.mock.calls.length).toBeGreaterThanOrEqual(2);
  expect(screen.getByText('Chưa có Agent đăng ký.')).toBeTruthy();
  await fireEvent.press(screen.getByText('Xử lý yêu cầu từ nhân viên'));
  await fireEvent.press(screen.getByText('Quản lý tài khoản công ty'));
  expect(router.push.mock.calls).toEqual([['/support-requests'], ['/company-users']]);
});

test('enrollment refuses missing reason, handles failure and exposes then clears a confirmed one-use code', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  jest.spyOn(api, 'operatorDashboard').mockResolvedValue({ totalDevices: 0, onlineDevices: 0, offlineDevices: 0, openAlerts: 0 });
  jest.spyOn(api, 'operatorDevices').mockResolvedValue([]);
  const issue = jest.spyOn(api, 'issueEnrollment').mockRejectedValueOnce(new Error('Enrollment offline')).mockResolvedValueOnce({ token: 'one-time-enrollment', expiresAt: '2026-10-23T10:00:00Z' });
  await wrap(<OperatorView />);
  await fireEvent.press(screen.getByRole('button', { name: 'Đăng ký Agent mới' }));
  await fireEvent.press(screen.getByText('Cấp mã dùng một lần, hạn 15 phút'));
  expect(Alert.alert).not.toHaveBeenCalled();
  await fireEvent.changeText(screen.getByLabelText('Lý do cấp mã'), 'New authorized workstation');
  await fireEvent.press(screen.getByText('Cấp mã dùng một lần, hạn 15 phút'));
  await confirmAlert();
  await waitFor(() => expect(screen.getByText(/Enrollment offline/)).toBeTruthy());
  await fireEvent.press(screen.getByText('Cấp mã dùng một lần, hạn 15 phút'));
  await confirmAlert();
  await waitFor(() => expect(screen.getByText(/one-time-enrollment/)).toBeTruthy());
  expect(issue).toHaveBeenLastCalledWith('New authorized workstation');
  await fireEvent.press(screen.getByText('Đã lưu mã — đóng'));
  expect(screen.queryByText(/one-time-enrollment/)).toBeNull();
});

test('employee cannot enter operator screens or start admin queries', async () => {
  const api = new MobileApiClient('https://test.local'); session(api, 'Employee');
  const dashboard = jest.spyOn(api, 'operatorDashboard');
  const devices = jest.spyOn(api, 'operatorDevices');
  await wrap(<OperatorView />);
  expect(screen.getByText('Bạn không có quyền quản trị công ty.')).toBeTruthy();
  expect(dashboard).not.toHaveBeenCalled();
  expect(devices).not.toHaveBeenCalled();
});

test.each(['Admin', 'Employee'] as const)('tabs expose device actions only for employees (%s)', async role => {
  session(new MobileApiClient('https://test.local'), role);
  await wrap(<TabsLayout />);
  const visibility = role === 'Employee' ? 'visible' : 'hidden';
  expect(screen.getByTestId('tab-my-device').props.accessibilityLabel).toBe(visibility);
  expect(screen.getByTestId('tab-incidents').props.accessibilityLabel).toBe(visibility);
  expect(screen.getByTestId('tab-account')).toBeTruthy();
  expect(screen.queryByTestId('route-redirect')).toBeNull();
});

test('unauthenticated tab access redirects before rendering protected tabs', async () => {
  const auth = session(new MobileApiClient('https://test.local'));
  jest.mocked(Auth.useAuth).mockReturnValue({ ...auth, status: 'unauthenticated', user: null });
  await wrap(<TabsLayout />);
  expect(screen.getByTestId('route-redirect').props.children).toBe('/');
  expect(screen.queryByTestId('tab-account')).toBeNull();
});

test('login validates fields, preserves an API failure, and navigates after a successful retry', async () => {
  const auth = session(new MobileApiClient('https://test.local'));
  jest.spyOn(I18n, 'useI18n').mockReturnValue({ lang: 'vi', setLang: jest.fn(), t: key => key });
  auth.login.mockRejectedValueOnce(new Error('invalid credentials')).mockResolvedValueOnce(undefined);
  await wrap(<LoginView />);
  await fireEvent.press(screen.getByTestId('login-submit-button'));
  expect(screen.getByText('Vui lòng điền đầy đủ email và mật khẩu.')).toBeTruthy();
  expect(auth.login).not.toHaveBeenCalled();
  expect(screen.queryByTestId('org-code-input')).toBeNull();
  await fireEvent.changeText(screen.getByTestId('email-input'), ' office@example.test ');
  await fireEvent.changeText(screen.getByTestId('password-input'), 'password unchanged');
  await fireEvent.press(screen.getByTestId('login-submit-button'));
  await waitFor(() => expect(screen.getByText('loginFailed')).toBeTruthy());
  expect(router.replace).not.toHaveBeenCalled();
  await fireEvent.press(screen.getByTestId('login-submit-button'));
  await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/(tabs)'));
  expect(auth.login).toHaveBeenLastCalledWith({ email: 'office@example.test', password: 'password unchanged' });
});

test('activation strips its token, validates passwords, retries server failures and delays login routing until success', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  jest.spyOn(I18n, 'useI18n').mockReturnValue({ lang: 'vi', setLang: jest.fn(), t: key => key });
  jest.mocked(Router.useLocalSearchParams).mockReturnValue({ token: 'private-invitation' });
  const validate = jest.spyOn(api, 'validateActivationToken').mockResolvedValue({ valid: true, displayName: 'Office Employee', organizationName: 'Demo Office' });
  const activate = jest.spyOn(api, 'activateAccount').mockRejectedValueOnce(new Error('Server unavailable')).mockResolvedValueOnce({ message: 'activated' });
  await wrap(<ActivateView />);
  await waitFor(() => expect(screen.getByTestId('activate-submit-button')).toBeTruthy());
  expect(validate).toHaveBeenCalledWith('private-invitation');
  expect(router.setParams).toHaveBeenCalledWith({ token: undefined, link: undefined });
  await fireEvent.press(screen.getByTestId('activate-submit-button'));
  expect(screen.getByText('passwordTooShort')).toBeTruthy();
  await fireEvent.changeText(screen.getByTestId('new-password-input'), 'SafePassword123!');
  await fireEvent.changeText(screen.getByTestId('confirm-password-input'), 'DifferentPassword123!');
  await fireEvent.press(screen.getByTestId('activate-submit-button'));
  expect(screen.getByText('passwordMismatch')).toBeTruthy();
  expect(activate).not.toHaveBeenCalled();
  await fireEvent.changeText(screen.getByTestId('confirm-password-input'), 'SafePassword123!');
  await fireEvent.press(screen.getByTestId('activate-submit-button'));
  await waitFor(() => expect(screen.getByText('Server unavailable')).toBeTruthy());
  jest.useFakeTimers();
  await fireEvent.press(screen.getByTestId('activate-submit-button'));
  await act(async () => {});
  expect(screen.getByText('activationSuccess')).toBeTruthy();
  expect(router.replace).not.toHaveBeenCalled();
  expect(activate).toHaveBeenLastCalledWith({ token: 'private-invitation', password: 'SafePassword123!', confirmPassword: 'SafePassword123!' });
  await act(() => { jest.advanceTimersByTime(2000); });
  expect(router.replace).toHaveBeenCalledWith('/login');
});

test('activation with an invalid link never enables password entry and returns to login', async () => {
  const api = new MobileApiClient('https://test.local'); session(api);
  jest.mocked(Router.useLocalSearchParams).mockReturnValue({ link: 'https://untrusted.example/activate#token=private-invitation' });
  const validate = jest.spyOn(api, 'validateActivationToken');
  await wrap(<ActivateView />);
  await waitFor(() => expect(screen.getByText('Quay lại đăng nhập')).toBeTruthy());
  expect(validate).not.toHaveBeenCalled();
  expect(screen.queryByTestId('new-password-input')).toBeNull();
  await fireEvent.press(screen.getByText('Quay lại đăng nhập'));
  expect(router.replace).toHaveBeenCalledWith('/login');
});
