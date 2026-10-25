import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Router from 'expo-router';
import * as Auth from '../src/features/auth/auth-context';
import { MyDeviceView } from '../src/features/my-device/my-device-view';
import { IncidentListView } from '../src/features/incidents/incident-list-view';
import { IncidentDetailView } from '../src/features/incidents/incident-detail-view';
import { MobileApiClient } from '../src/lib/api/client';
import { I18nProvider } from '../src/lib/i18n';
import type { IncidentDto, MyDeviceDto } from '../src/lib/validation/schemas';

const id = '10000000-0000-4000-8000-000000000001';
const incident: IncidentDto = { id, deviceId: id, deviceName: 'Office PC', title: 'Printer stopped', description: 'Cannot print invoices',
  severity: 'High', status: 'Resolved', reportedByUserId: id, createdAt: '2026-10-23T00:00:00Z', resolutionNotes: 'Replaced cable', resolvedAt: '2026-10-23T01:00:00Z' };
const device = { device: { id, name: 'Office PC', isOnline: true, lastSeen: '2026-10-23T00:00:00Z', osVersion: 'Windows 11', agentVersion: '1.0' },
  appliedPolicy: 'Office policy', latestTelemetry: { cpuPercent: 12.4, ramPercent: 45.6, diskPercent: 78.9 }, serialNumber: 'SERIAL123', manufacturer: 'Example Manufacturer', model: 'Example Model', location: 'Room A', assignedAt: '2026-10-22T00:00:00Z' } as MyDeviceDto;

describe('Employee device and incident journeys', () => {
  let api: MobileApiClient;
  const push = jest.fn();
  const back = jest.fn();
  async function mount(ui: React.ReactElement) {
    const query = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    return await render(<QueryClientProvider client={query}><I18nProvider>{ui}</I18nProvider></QueryClientProvider>);
  }
  beforeEach(() => {
    api = new MobileApiClient();
    push.mockClear(); back.mockClear();
    jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push, back });
    jest.mocked(Router.useLocalSearchParams).mockReturnValue({});
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated', user: null, apiClient: api, login: jest.fn(), logout: jest.fn(), logoutAll: jest.fn(), retrySession: jest.fn(), dismissSessionExpired: jest.fn() });
  });
  afterEach(() => jest.restoreAllMocks());

  it('shows technical telemetry and routes reporting and privacy from the assigned device', async () => {
    const load = jest.spyOn(api, 'myDevice').mockResolvedValue(device);
    await mount(<MyDeviceView />);
    await screen.findByText('Office PC');
    for (const text of ['12%', '46%', '79%', 'SERIAL123', 'Example Manufacturer', 'Example Model', 'Office policy', 'Room A']) expect(screen.getByText(text)).toBeTruthy();
    for (const button of screen.getAllByRole('button')) await fireEvent.press(button);
    expect(push).toHaveBeenCalledWith('/incident-create');
    expect(push).toHaveBeenCalledWith('/privacy-manifest');
    await fireEvent(screen.getByTestId('device-refresh'), 'refresh');
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  });

  it('does not invent telemetry or system information for an offline unreported device', async () => {
    jest.spyOn(api, 'myDevice').mockResolvedValue({ device: { id, name: 'Offline PC', isOnline: false }, latestTelemetry: null, appliedPolicy: null } as MyDeviceDto);
    await mount(<MyDeviceView />);
    await screen.findByText('Offline PC');
    expect(screen.getAllByText('--')).toHaveLength(3);
    expect(screen.queryByText('SERIAL123')).toBeNull();
  });

  it('offers enrollment navigation when no device is assigned and permits refresh', async () => {
    const load = jest.spyOn(api, 'myDevice').mockResolvedValue(null);
    await mount(<MyDeviceView />);
    await waitFor(() => expect(screen.getAllByRole('button')).toHaveLength(1));
    await fireEvent.press(screen.getByRole('button'));
    expect(push).toHaveBeenCalledWith('/(tabs)/scan');
    await fireEvent(screen.getByTestId('device-refresh'), 'refresh');
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  });

  it('recovers a failed device request only after retry', async () => {
    const load = jest.spyOn(api, 'myDevice').mockRejectedValueOnce(new Error('Device service unavailable')).mockResolvedValue(device);
    await mount(<MyDeviceView />);
    await screen.findByText('Device service unavailable');
    expect(screen.queryByText('12%')).toBeNull();
    await fireEvent.press(screen.getByRole('button'));
    await screen.findByText('Office PC');
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('lists incidents, routes to the selected incident and refreshes the list', async () => {
    const incidents = [incident, ...(['Medium', 'Low', 'Critical'] as const).map((severity, index) => ({ ...incident, id: String(index), title: `Problem ${index}`, severity, status: ['InProgress', 'Closed', 'Open'][index], description: null }))];
    const load = jest.spyOn(api, 'myDeviceIncidents').mockResolvedValue(incidents);
    await mount(<IncidentListView />);
    await screen.findByText('Printer stopped');
    await fireEvent.press(screen.getByLabelText('Sự cố: Printer stopped'));
    expect(push).toHaveBeenCalledWith({ pathname: '/incident-detail', params: { incidentJson: JSON.stringify(incident) } });
    await fireEvent.press(screen.getAllByRole('button')[0]);
    expect(push).toHaveBeenCalledWith('/incident-create');
    await fireEvent(screen.getByTestId('incidents-refresh'), 'refresh');
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  });

  it('retries incident loading and renders an empty list rather than stale incidents', async () => {
    const load = jest.spyOn(api, 'myDeviceIncidents').mockRejectedValueOnce(new Error('Incident service unavailable')).mockResolvedValue([]);
    await mount(<IncidentListView />);
    await screen.findByText('Incident service unavailable');
    await fireEvent.press(screen.getAllByRole('button')[1]);
    await waitFor(() => expect(screen.queryByText('Incident service unavailable')).toBeNull());
    expect(load).toHaveBeenCalledTimes(2);
    expect(screen.queryByText('Printer stopped')).toBeNull();
  });

  it.each([undefined, '{invalid'])('shows a safe missing-incident state for invalid navigation data', async (incidentJson) => {
    jest.mocked(Router.useLocalSearchParams).mockReturnValue(incidentJson ? { incidentJson } : {});
    await mount(<IncidentDetailView />);
    expect(screen.getByText('Không tìm thấy thông tin sự cố')).toBeTruthy();
    await fireEvent.press(screen.getByText('Quay lại'));
    expect(back).toHaveBeenCalledTimes(1);
  });

  it('shows the reported issue and resolution without requesting additional telemetry', async () => {
    jest.mocked(Router.useLocalSearchParams).mockReturnValue({ incidentJson: JSON.stringify(incident) });
    await mount(<IncidentDetailView />);
    expect(screen.getByText('Cannot print invoices')).toBeTruthy();
    expect(screen.getByText('Replaced cable')).toBeTruthy();
    await fireEvent.press(screen.getByText('Quay lại danh sách'));
    expect(back).toHaveBeenCalledTimes(1);
  });

  it('shows a missing description without presenting a resolution for an open incident', async () => {
    jest.mocked(Router.useLocalSearchParams).mockReturnValue({ incidentJson: JSON.stringify({ ...incident, description: null, resolutionNotes: null, resolvedAt: null }) });
    await mount(<IncidentDetailView />);
    expect(screen.getByText('Không có mô tả chi tiết')).toBeTruthy();
    expect(screen.queryByText('Replaced cable')).toBeNull();
  });
});
