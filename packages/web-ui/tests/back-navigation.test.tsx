// @vitest-environment jsdom
import React from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import './helpers';
import { BackNavigation, NavigationHistory, PublicBackNavigation, backDestination } from '../src/components/back-navigation';

const navigation = vi.hoisted(() => ({ pathname: '/devices', back: vi.fn(), replace: vi.fn() }));
vi.mock('next/navigation', () => ({ usePathname: () => navigation.pathname, useRouter: () => navigation }));

beforeEach(() => { vi.clearAllMocks(); navigation.pathname = '/devices'; });

it.each([
  ['/company/devices/abc', false, '/devices'], ['/devices/abc', false, '/devices'],
  ['/devices', false, '/dashboard'], ['/dashboard', false, '/devices'],
  ['/employee/support', true, '/my-device'], ['/employee/my-device', true, '/support'],
  ['/activate', false, '/login'], ['/qr/abc', false, '/login'], ['/login', false, '/'],
])('returns a useful same-portal destination for direct entry at %s', (path, employee, target) => {
  expect(backDestination(path, employee)).toBe(target);
});

it('direct entry never uses unknown browser history that could leave the app', () => {
  navigation.pathname = '/company/devices/abc';
  window.history.replaceState({}, '', '/company/devices/abc');
  render(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
  expect(navigation.replace).toHaveBeenCalledWith('/devices');
  expect(navigation.back).not.toHaveBeenCalled();
});

it('uses browser Back for a known in-app visit and falls back after returning to its first page', () => {
  const view = render(<NavigationHistory><BackNavigation /></NavigationHistory>);
  navigation.pathname = '/devices/abc';
  view.rerender(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: /Back|Quay lại/ }));
  expect(navigation.back).toHaveBeenCalledOnce();
  fireEvent(window, new PopStateEvent('popstate'));
  navigation.pathname = '/devices';
  view.rerender(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: /Back|Quay lại/ }));
  expect(navigation.replace).toHaveBeenCalledWith('/dashboard');
});

it('does not return to login or activation after sign-in', () => {
  navigation.pathname = '/activate';
  const view = render(<NavigationHistory><BackNavigation /></NavigationHistory>);
  navigation.pathname = '/dashboard';
  view.rerender(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: /Back|Quay lại/ }));
  expect(navigation.back).not.toHaveBeenCalled();
  expect(navigation.replace).toHaveBeenCalledWith('/devices');
});

it('does not invent browser history when replacing a direct-entry page with its parent', () => {
  navigation.pathname = '/devices/abc';
  const view = render(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
  navigation.pathname = '/devices';
  view.rerender(<NavigationHistory><BackNavigation /></NavigationHistory>);
  fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
  expect(navigation.back).not.toHaveBeenCalled();
  expect(navigation.replace).toHaveBeenLastCalledWith('/dashboard');
});

it('renders public navigation without duplicating the authenticated shell control', () => {
  const view = render(<PublicBackNavigation />);
  expect(screen.queryByRole('button')).toBeNull();
  navigation.pathname = '/employee/login';
  view.rerender(<PublicBackNavigation />);
  expect(screen.getByRole('button', { name: /Back|Quay lại/ })).toBeTruthy();
});
