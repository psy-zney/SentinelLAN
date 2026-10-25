import React from 'react';
import { StyleSheet } from 'react-native';
import { fireEvent, render, screen } from '@testing-library/react-native';
import { BackNavigation } from '../src/components/back-navigation';
import * as Auth from '../src/features/auth/auth-context';

const mockRouter = { canGoBack: jest.fn(), back: jest.fn(), replace: jest.fn() };
jest.mock('expo-router', () => ({ useRouter: () => mockRouter }));
jest.mock('../src/lib/i18n', () => ({ useI18n: () => ({ lang: 'vi' }) }));
jest.mock('../src/components/common', () => ({ useAppColors: () => ({ primary: '#176b58' }) }));

beforeEach(() => { jest.clearAllMocks(); });
afterEach(() => jest.restoreAllMocks());

it('offers a labelled touch target and returns to the previous screen', async () => {
  jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated' } as ReturnType<typeof Auth.useAuth>);
  mockRouter.canGoBack.mockReturnValue(true);
  await render(<BackNavigation />);
  const button = screen.getByRole('button', { name: 'Quay lại' });
  await fireEvent.press(button);
  expect(mockRouter.back).toHaveBeenCalledTimes(1);
  expect(mockRouter.replace).not.toHaveBeenCalled();
  expect(StyleSheet.flatten(button.props.style).minHeight).toBeGreaterThanOrEqual(48);
});

it.each([['authenticated', '/(tabs)'], ['unauthenticated', '/login']] as const)(
  'deep links without history return to the %s destination', async (status, href) => {
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status } as ReturnType<typeof Auth.useAuth>);
    mockRouter.canGoBack.mockReturnValue(false);
    await render(<BackNavigation />);
    await fireEvent.press(screen.getByRole('button', { name: 'Quay lại' }));
    expect(mockRouter.replace).toHaveBeenCalledWith(href);
    expect(mockRouter.back).not.toHaveBeenCalled();
  });
