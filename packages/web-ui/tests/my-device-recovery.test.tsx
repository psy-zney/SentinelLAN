// @vitest-environment jsdom
import React from 'react';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { fakeApi, renderProviders, resetApi } from './helpers';
import { MyDeviceView } from '../src/components/my-device-view';
import { ApiError } from '../src/lib/api-client';

vi.mock('@/lib/api-client', async () => {
  const { fakeApi } = await import('./helpers');
  return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number) { super('api'); } } };
});
vi.mock('@/components/endpoint-observation-panel', () => ({ EndpointObservationPanel: () => null }));
vi.mock('@/components/software-compliance-panel', () => ({ SoftwareCompliancePanel: () => null }));

const computer = { id: 'assigned', name: 'Employee laptop', isOnline: false, lastSeenAt: null, manufacturer: null, model: null, assetType: null, osVersion: 'Windows 11', agentVersion: '1.0', serialNumber: null, location: null, assignedAt: null, appliedPolicy: null, transparencyManifest: null };
const incident = { id: 'incident', title: 'Fan noise', severity: 'High', status: 'Open', createdAt: '2026-10-23T08:00:00Z' };
beforeEach(() => {
  resetApi();
  fakeApi.myDevice.mockResolvedValue(computer);
  fakeApi.myDeviceTelemetry.mockResolvedValue([]);
  fakeApi.myDeviceIncidents.mockResolvedValue([]);
});

it.each(['vi', 'en'])('recovers from an API outage in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang);
  fakeApi.myDevice.mockRejectedValue(new Error('offline'));
  renderProviders(<MyDeviceView />);
  await screen.findByRole('alert');
  expect(screen.getByRole('alert').textContent).toContain(lang === 'vi' ? 'Chưa thể tải' : 'No assigned device');
  fakeApi.myDevice.mockResolvedValue(computer);
  fireEvent.click(screen.getByRole('button', { name: lang === 'vi' ? 'Thử lại' : 'Retry' }));
  await screen.findByRole('heading', { name: computer.name });
  expect(fakeApi.myDevice.mock.calls.length).toBeGreaterThanOrEqual(2);
  expect(screen.queryByRole('alert')).toBeNull();
});

it.each(['vi', 'en'])('refreshes an unassigned computer after assignment in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang);
  fakeApi.myDevice.mockRejectedValue(new ApiError(404));
  renderProviders(<MyDeviceView />);
  await screen.findByRole('heading', { name: lang === 'vi' ? 'Chưa có máy tính được giao cho bạn' : 'No assigned computer' });
  fakeApi.myDevice.mockResolvedValue(computer);
  fireEvent.click(screen.getByRole('button', { name: lang === 'vi' ? 'Làm mới' : 'Refresh' }));
  await screen.findByRole('heading', { name: computer.name });
});

it('shows the device even when optional telemetry and incident APIs fail', async () => {
  fakeApi.myDeviceTelemetry.mockRejectedValue(new Error('telemetry unavailable'));
  fakeApi.myDeviceIncidents.mockRejectedValue(new Error('incidents unavailable'));
  renderProviders(<MyDeviceView />);
  await screen.findByRole('heading', { name: computer.name });
  expect(screen.getByText('Chưa kết nối')).toBeTruthy();
  expect(screen.queryByRole('alert')).toBeNull();
  expect(screen.getAllByText('—')).toHaveLength(3);
});

it.each(['vi', 'en'])('renders current metrics, privacy details and incident outcomes in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang);
  fakeApi.myDevice.mockResolvedValue({ ...computer, isOnline: true, manufacturer: 'Dell', model: 'Latitude', serialNumber: 'SERIAL-1', location: 'Office 2', assignedAt: '2026-10-01', lastSeenAt: '2026-10-23T08:00:00Z', appliedPolicy: 'Office policy', transparencyManifest: { dataRetentionDays: 30, collectedTelemetry: ['CPU only'], privacyBoundaries: ['No screenshots'] } });
  fakeApi.myDeviceTelemetry.mockResolvedValue([{ cpuPercent: 0, ramPercent: 42, diskPercent: null }]);
  fakeApi.myDeviceIncidents.mockResolvedValue([incident, { ...incident, id: 'closed', title: 'Printer fixed', severity: 'Low', status: 'Closed' }, { ...incident, id: 'custom', title: 'Legacy issue', severity: 'Legacy', status: 'Legacy' }]);
  renderProviders(<MyDeviceView />);
  await screen.findByText('SERIAL-1');
  expect(screen.getByText('0%')).toBeTruthy();
  expect(screen.getByText('42%')).toBeTruthy();
  expect(screen.getByText('CPU only')).toBeTruthy();
  expect(screen.getByText('No screenshots')).toBeTruthy();
  expect(screen.getByText(lang === 'vi' ? 'Đã đóng' : 'Closed')).toBeTruthy();
  expect(screen.getByText(lang === 'vi' ? 'Không làm việc được' : 'High')).toBeTruthy();
  expect(screen.getAllByText('Legacy')).toHaveLength(2);
});

it.each(['vi', 'en'])('retains report data and the idempotency key on retry in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang);
  const alert = vi.spyOn(window, 'alert').mockImplementation(() => {});
  fakeApi.reportMyDeviceIncident.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ id: 'new' });
  fakeApi.myDeviceIncidents.mockResolvedValueOnce([]).mockResolvedValue([incident]);
  renderProviders(<MyDeviceView />);
  await screen.findByRole('heading', { name: computer.name });
  fireEvent.click(screen.getByRole('button', { name: lang === 'vi' ? 'Nhờ IT hỗ trợ' : 'Report Device Issue' }));
  const modal = screen.getByRole('dialog');
  fireEvent.change(within(modal).getByPlaceholderText(lang === 'vi' ? /VD: Máy khởi động chậm/ : /e.g. Broken screen/), { target: { value: '  Fan noise  ' } });
  fireEvent.change(within(modal).getByRole('combobox'), { target: { value: 'High' } });
  const submit = within(modal).getByRole('button', { name: lang === 'vi' ? 'Gửi báo cáo' : 'Submit Report' });
  fireEvent.click(submit);
  await waitFor(() => expect(alert).toHaveBeenCalledOnce());
  const first = fakeApi.reportMyDeviceIncident.mock.calls[0][0];
  expect(first).toMatchObject({ title: 'Fan noise', description: undefined, severity: 'High', idempotencyKey: expect.any(String) });
  fireEvent.click(submit);
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  expect(fakeApi.reportMyDeviceIncident.mock.calls[1][0]).toEqual(first);
  await screen.findByText('Fan noise');
});

it('ignores a pending response after its view has unmounted', async () => {
  let resolve!: (value: typeof computer) => void;
  fakeApi.myDevice.mockReturnValue(new Promise<typeof computer>(done => { resolve = done; }));
  const { unmount } = renderProviders(<MyDeviceView />);
  unmount();
  await act(async () => resolve(computer));
  expect(screen.queryByText(computer.name)).toBeNull();
});
