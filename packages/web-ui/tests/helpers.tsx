import React, { type ReactElement } from 'react';
import { afterEach, vi, type Mock } from 'vitest';
import { cleanup, render } from '@testing-library/react';
import { I18nProvider } from '../src/lib/i18n';

export const fakeApi: Record<string, Mock> = new Proxy({} as Record<string, Mock>, {
  get(target, key: string) { return target[key] ??= vi.fn().mockResolvedValue([]); }
});
export const auth = { session: { userId: 'admin', organizationId: 'org', role: 'Admin', displayName: 'Admin' } };
export const device = { id: 'device-1', assignedUserId: null, name: 'Office PC', osVersion: 'Windows 11', agentVersion: '1.0', isOnline: true, isRevoked: false, createdAt: '2026-01-01T00:00:00Z', lastSeenAt: '2026-01-01T00:00:00Z' };
export const policy = { id: 'policy-1', name: 'Office policy', idleTimeoutMinutes: 15, usbMode: 'Blocked', assignedDeviceCount: 1, createdAt: '2026-01-01T00:00:00Z' };
export const account = { id: 'employee-1', email: 'staff@example.test', displayName: 'Office Staff', role: 'Employee', status: 'Active', createdAt: '2026-01-01T00:00:00Z' };
export function resetApi() {
  for (const key of Object.keys(fakeApi)) fakeApi[key].mockReset().mockResolvedValue([]);
  auth.session = { userId: 'admin', organizationId: 'org', role: 'Admin', displayName: 'Admin' };
  fakeApi.users.mockResolvedValue([account]);
  fakeApi.organization.mockResolvedValue({ name: 'Office', code: 'OFFICE', createdAt: '2026-01-01T00:00:00Z' });
  fakeApi.devices.mockResolvedValue([device]); fakeApi.devicePage.mockResolvedValue([device]);
  fakeApi.policies.mockResolvedValue([policy]);
  fakeApi.departments.mockResolvedValue({ departments: [{ id: 'dept', name: 'Finance', revision: 'revision', deviceIds: [device.id], policyId: policy.id }], users: [{id: account.id,name:account.displayName,departmentId:null}], devices: [{id:device.id,name:device.name,departmentId:null}] });
}
export function renderProviders(element: ReactElement) { return render(<I18nProvider>{element}</I18nProvider>); }
afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); });
