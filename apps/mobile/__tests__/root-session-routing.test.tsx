import React from 'react';
import { Alert, Text } from 'react-native';
import { act, fireEvent, render, screen } from '@testing-library/react-native';
import * as Auth from '../src/features/auth/auth-context';
import { I18nProvider } from '../src/lib/i18n';
import SplashScreen from '../src/app/index';
import RootLayout from '../src/app/_layout';
import { MobileApiClient } from '../src/lib/api/client';

jest.mock('../src/features/self-service/push-observer', () => ({ EmployeePushObserver: () => null }));

describe('Root session navigation boundaries', () => {
  const replace = jest.fn(), retrySession = jest.fn(), dismissSessionExpired = jest.fn();
  let status: Auth.AuthStatus;
  let role: 'Admin' | 'Employee';
  let segments: string[];
  beforeEach(() => {
    jest.clearAllMocks(); status = 'authenticated'; role = 'Employee'; segments = ['(tabs)'];
    jest.spyOn(Auth, 'useAuth').mockImplementation(() => ({ status, user: { id: 'test-user', email: 'employee@example.test', displayName: 'Employee', organizationId: 'test-org', organizationCode: 'demo', role }, retrySession, dismissSessionExpired, login: jest.fn(), logout: jest.fn(), logoutAll: jest.fn(), apiClient: new MobileApiClient() }));
    jest.spyOn(Auth, 'AuthProvider').mockImplementation(({ children }) => <>{children}</>);
    const router = require('expo-router');
    jest.spyOn(router, 'useRouter').mockReturnValue({ replace });
    router.useSegments = () => segments;
    router.Stack.Screen = ({ name }: { name: string }) => <Text>{`route:${name}`}</Text>;
    router.Redirect = ({ href }: { href: string }) => <Text>{`redirect:${href}`}</Text>;
  });
  afterEach(() => jest.restoreAllMocks());
  it.each([['authenticated', '/(tabs)'], ['unauthenticated', '/login']] as const)('routes %s only to its session destination', async (session, destination) => {
    status = session; await render(<I18nProvider><SplashScreen /></I18nProvider>);
    expect(replace).toHaveBeenCalledWith(destination);
  });
  it('requires acknowledgement before dismissing an expired session', async () => {
    status = 'session-expired'; const alert = jest.spyOn(Alert, 'alert');
    await render(<I18nProvider><SplashScreen /></I18nProvider>);
    expect(replace).not.toHaveBeenCalled(); expect(dismissSessionExpired).not.toHaveBeenCalled();
    await act(() => alert.mock.calls[0][2]?.[0].onPress?.());
    expect(dismissSessionExpired).toHaveBeenCalledTimes(1); expect(replace).toHaveBeenCalledWith('/login');
  });
  it('offers session retry while offline without navigating to protected screens', async () => {
    status = 'offline'; await render(<I18nProvider><SplashScreen /></I18nProvider>);
    await fireEvent.press(screen.getByRole('button')); expect(retrySession).toHaveBeenCalledTimes(1); expect(replace).not.toHaveBeenCalled();
  });
  it('shows the update requirement without proceeding into an old application', async () => {
    status = 'update-required'; await render(<I18nProvider><SplashScreen /></I18nProvider>);
    expect(screen.getByText('Yêu cầu cập nhật ứng dụng')).toBeTruthy();
    await fireEvent.press(screen.getByText('Đóng ứng dụng')); expect(replace).not.toHaveBeenCalled();
  });
  it('keeps bootstrapping sessions on the waiting screen', async () => {
    status = 'bootstrapping'; await render(<I18nProvider><SplashScreen /></I18nProvider>);
    expect(screen.getByText('Secure Endpoint Governance')).toBeTruthy(); expect(replace).not.toHaveBeenCalled();
  });
  it.each(['my-device', 'incidents', 'incident-create', 'incident-detail', 'request-create', 'app-catalog', 'self-service-help'])('blocks Admin from employee-only route %s', async (route) => {
    role = 'Admin'; segments = [route]; await render(<RootLayout />);
    expect(screen.getByText('redirect:/(tabs)')).toBeTruthy(); expect(screen.queryByText('route:company-users')).toBeNull();
  });
  it.each(['company-users', 'vps-status', 'device-detail'])('blocks an Employee from the Admin route %s', async route => {
    segments = [route]; await render(<RootLayout />); expect(screen.getByText('redirect:/(tabs)')).toBeTruthy();
  });
  it.each(['Admin', 'Employee'] as const)('keeps the allowed %s navigation stack available', async (userRole) => {
    role = userRole; segments = [userRole === 'Admin' ? 'company-users' : 'my-device']; await render(<RootLayout />);
    expect(screen.queryByText('redirect:/(tabs)')).toBeNull(); expect(screen.getByText('route:request-detail')).toBeTruthy();
  });
});
