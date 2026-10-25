import React from 'react';
import { Text, Button } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import Constants from 'expo-constants';
import * as Notifications from 'expo-notifications';
import { AuthProvider, useAuth } from '../src/features/auth/auth-context';
import { MobileApiClient } from '../src/lib/api/client';
import { TokenVault } from '../src/lib/security/secure-store';
import * as Push from '../src/features/self-service/push';

const id = '10000000-0000-4000-8000-000000000001';
const user = { id, email: 'employee@example.test', displayName: 'Employee', role: 'Employee' as const, organizationId: id, organizationCode: 'demo' };
const bootstrap = { minimumAppVersion: '1.0.0', latestAppVersion: '1.0.0', privacyManifestVersion: '1', maintenanceMode: false, supportEmail: 'support@example.test', supportedAuthSchemes: ['Bearer'] };
const session = { accessToken: 'access', expiresIn: 900, refreshToken: 'refresh', refreshTokenExpiresAt: '2030-01-01T00:00:00Z', tokenType: 'Bearer' as const, user };

function Probe() {
  const auth = useAuth();
  const [error, setError] = React.useState('');
  const run = (action: () => Promise<void>) => { void action().catch((failure: Error) => setError(failure.message)); };
  return <><Text testID="status">{auth.status}</Text><Text testID="user">{auth.user?.displayName ?? 'none'}</Text><Text>{error}</Text>
    <Button title="Login" onPress={() => run(() => auth.login({ organizationCode: 'demo', email: user.email, password: 'long-password' }))} />
    <Button title="Logout" onPress={() => run(auth.logout)} />
    <Button title="Logout all" onPress={() => run(auth.logoutAll)} />
    <Button title="Retry" onPress={() => run(auth.retrySession)} />
    <Button title="Dismiss expiry" onPress={auth.dismissSessionExpired} /></>;
}
describe('AuthProvider session lifecycle', () => {
  let query: QueryClient;
  async function mount(client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })) {
    query = client;
    return await render(<QueryClientProvider client={query}><AuthProvider><Probe /></AuthProvider></QueryClientProvider>);
  }
  beforeEach(async () => {
    await TokenVault.wipeAll();
    Constants.expoConfig!.version = '1.0.0';
    jest.spyOn(MobileApiClient.prototype, 'bootstrap').mockResolvedValue(bootstrap);
    jest.spyOn(MobileApiClient.prototype, 'refresh').mockResolvedValue('success');
    jest.spyOn(Push, 'disableEmployeePush').mockResolvedValue();
    jest.spyOn(Push, 'forgetEmployeePush').mockResolvedValue();
  });
  afterEach(async () => { jest.restoreAllMocks(); await TokenVault.wipeAll(); });

  it('sets user only after login resolves and clears cached queries on login/logout', async () => {
    const login = jest.spyOn(MobileApiClient.prototype, 'login').mockResolvedValue(session);
    const logout = jest.spyOn(MobileApiClient.prototype, 'logout').mockResolvedValue();
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('unauthenticated'));
    query.setQueryData(['old-user-data'], 'sensitive old data');
    await fireEvent.press(screen.getByText('Login'));
    await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('authenticated'));
    expect(screen.getByTestId('user').props.children).toBe('Employee');
    expect(query.getQueryData(['old-user-data'])).toBeUndefined();
    expect(login).toHaveBeenCalledWith(expect.objectContaining({ appVersion: '1.0.0' }));
    query.setQueryData(['private'], 'private data');
    await fireEvent.press(screen.getByText('Logout'));
    await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('unauthenticated'));
    expect(logout).toHaveBeenCalledTimes(1);
    expect(Push.disableEmployeePush).toHaveBeenCalledWith(expect.any(MobileApiClient), id);
    expect(query.getQueryData(['private'])).toBeUndefined();
    expect(screen.getByTestId('user').props.children).toBe('none');
  });

  it('does not publish an authenticated state when login fails', async () => {
    jest.spyOn(MobileApiClient.prototype, 'login').mockRejectedValue(new Error('Invalid credentials'));
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('unauthenticated'));
    await fireEvent.press(screen.getByText('Login'));
    await screen.findByText('Invalid credentials');
    expect(screen.getByTestId('status').props.children).toBe('unauthenticated');
    expect(screen.getByTestId('user').props.children).toBe('none');
  });

  it.each(['1.0.1', '2.0.0', '1.0.0.1'])('requires update before refreshing tokens when minimum version is %s', async (minimumAppVersion) => {
    await TokenVault.saveRefreshToken('stored-refresh');
    jest.mocked(MobileApiClient.prototype.bootstrap).mockResolvedValue({ ...bootstrap, minimumAppVersion });
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('update-required'));
    expect(MobileApiClient.prototype.refresh).not.toHaveBeenCalled();
  });

  it('allows newer app versions and restores only the current securely cached user', async () => {
    Constants.expoConfig!.version = '2.0.0';
    await TokenVault.saveRefreshToken('stored-refresh'); await TokenVault.saveCachedUser(user);
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('authenticated'));
    expect(screen.getByTestId('user').props.children).toBe('Employee');
  });

  it('clears cached query data when the stored session is rejected', async () => {
    await TokenVault.saveRefreshToken('stored-refresh');
    jest.mocked(MobileApiClient.prototype.refresh).mockResolvedValue('rejected');
    const client = new QueryClient();
    client.setQueryData(['previous-user'], { name: 'Cached employee' });
    await mount(client);
    await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('unauthenticated'));
    expect(client.getQueryData(['previous-user'])).toBeUndefined();
  });

  it('preserves authenticated state when logout-all revocation fails and allows retry', async () => {
    await TokenVault.saveRefreshToken('stored-refresh'); await TokenVault.saveCachedUser(user);
    const logout = jest.spyOn(MobileApiClient.prototype, 'logoutAll').mockRejectedValueOnce(new Error('Revocation unavailable')).mockResolvedValue();
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('authenticated'));
    await fireEvent.press(screen.getByText('Logout all'));
    await screen.findByText('Revocation unavailable');
    expect(screen.getByTestId('status').props.children).toBe('authenticated');
    await fireEvent.press(screen.getByText('Logout all'));
    await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('unauthenticated'));
    expect(logout).toHaveBeenCalledTimes(2);
  });

  it('clears user and query cache on session expiry before dismissing the notice', async () => {
    let expire: () => void = () => {};
    jest.spyOn(require('../src/lib/api/client'), 'setOnSessionExpired').mockImplementation((callback: unknown) => { expire = callback as () => void; });
    await TokenVault.saveRefreshToken('stored-refresh'); await TokenVault.saveCachedUser(user);
    await mount(); await waitFor(() => expect(screen.getByTestId('status').props.children).toBe('authenticated'));
    query.setQueryData(['private'], 'private data');
    await act(() => expire());
    expect(screen.getByTestId('status').props.children).toBe('session-expired');
    expect(screen.getByTestId('user').props.children).toBe('none');
    expect(query.getQueryData(['private'])).toBeUndefined();
    await waitFor(() => expect(Push.forgetEmployeePush).toHaveBeenCalled());
    expect(Notifications.clearLastNotificationResponseAsync).toHaveBeenCalled();
    await fireEvent.press(screen.getByText('Dismiss expiry'));
    expect(screen.getByTestId('status').props.children).toBe('unauthenticated');
  });
});
