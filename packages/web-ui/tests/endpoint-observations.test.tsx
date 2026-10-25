// @vitest-environment jsdom
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { fakeApi, resetApi, renderProviders } from './helpers';
import { EndpointObservationPanel } from '../src/components/endpoint-observation-panel';

vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: vi.fn() }));

vi.mock('@/lib/api-client', async () => { const { fakeApi } = await import('./helpers'); return { ApiClient: class { constructor() { return fakeApi; } } }; });
const capturedAt = '2026-10-23T00:00:00Z';
const observation = {
  inventory: { cpuName: 'Office CPU', ramBytes: 8 * 1024 ** 3, systemDiskBytes: 256 * 1024 ** 3, serialNumber: 'SERIAL123', capturedAt, networkAdapters: [{ macAddress: 'AA:BB:CC:DD:EE:FF', addresses: ['192.168.1.10', 'fe80::1234'] }] },
  security: { antivirusStatus: 'Healthy', firewallDomainStatus: 'Unhealthy', firewallPrivateStatus: 'NotSupported', firewallPublicStatus: 'Unknown', diskEncryptionStatus: 'Healthy', capturedAt }, securityStale: false,
};
beforeEach(resetApi);
describe('Endpoint observations and honest protection status', () => {
  it.each(['vi', 'en'])('reports current protection states distinctly in %s', async (lang) => {
    localStorage.setItem('sentinellan_lang', lang); fakeApi.endpointObservations.mockResolvedValue(observation);
    renderProviders(<EndpointObservationPanel deviceId="authorized-device" technical />);
    await screen.findByText('Office CPU');
    expect(fakeApi.endpointObservations).toHaveBeenCalledWith('authorized-device');
    expect(screen.getByText('8 GiB')).toBeTruthy(); expect(screen.getByText('256 GiB')).toBeTruthy(); expect(screen.getByText('SERIAL123')).toBeTruthy();
    expect(screen.getAllByText(lang === 'vi' ? 'Đang bảo vệ' : 'Protected')).toHaveLength(2);
    expect(screen.getByText(lang === 'vi' ? 'Cần IT kiểm tra' : 'Needs IT attention')).toBeTruthy();
    expect(screen.getByText(lang === 'vi' ? 'Không hỗ trợ trên máy này' : 'Not supported')).toBeTruthy();
    expect(screen.getByText(lang === 'vi' ? 'Chưa xác định' : 'Unknown')).toBeTruthy();
    fireEvent.click(screen.getByText(lang === 'vi' ? 'Địa chỉ mạng LAN' : 'LAN addresses'));
    expect(screen.getByText('AA:BB:CC:DD:EE:FF')).toBeTruthy();
    expect(screen.getByText(/192.168.1.10, fe80::1234/)).toBeTruthy();
  });
  it.each(['vi', 'en'])('does not claim old Healthy or Unhealthy samples describe current protection (%s)', async (lang) => {
    localStorage.setItem('sentinellan_lang', lang); fakeApi.endpointObservations.mockResolvedValue({ ...observation, securityStale: true });
    renderProviders(<EndpointObservationPanel />); await screen.findByText('Office CPU');
    expect(fakeApi.endpointObservations).toHaveBeenCalledWith(undefined);
    expect(screen.queryByText(lang === 'vi' ? 'Đang bảo vệ' : 'Protected')).toBeNull();
    expect(screen.queryByText(lang === 'vi' ? 'Cần IT kiểm tra' : 'Needs IT attention')).toBeNull();
    expect(screen.getAllByText(lang === 'vi' ? 'Chưa xác định' : 'Unknown')).toHaveLength(4);
    expect(screen.getByText(lang === 'vi' ? /Trạng thái cũ không được dùng/ : /Older observations do not establish/)).toBeTruthy();
    expect(screen.queryByText('AA:BB:CC:DD:EE:FF')).toBeNull();
  });
  it.each(['vi', 'en'])('uses explicit unknown values when inventory and security have not been reported (%s)', async (lang) => {
    localStorage.setItem('sentinellan_lang', lang); fakeApi.endpointObservations.mockResolvedValue({ inventory: null, security: null, securityStale: true });
    renderProviders(<EndpointObservationPanel />);
    await screen.findByText(lang === 'vi' ? 'Agent chưa báo cáo cấu hình phần cứng.' : 'No hardware inventory has been reported.');
    expect(screen.getAllByText(lang === 'vi' ? 'Chưa xác định' : 'Unknown')).toHaveLength(5);
    expect(screen.queryByText('Office CPU')).toBeNull();
  });
  it('keeps missing hardware fields unknown and omits empty LAN details even for technical viewers', async () => {
    fakeApi.endpointObservations.mockResolvedValue({ ...observation, inventory: { ...observation.inventory, cpuName: null, ramBytes: null, systemDiskBytes: undefined, serialNumber: null, networkAdapters: [] } });
    renderProviders(<EndpointObservationPanel technical />);
    const region = await screen.findByRole('region', { name: 'Cấu hình và bảo vệ máy' });
    await waitFor(() => expect(within(region).getAllByText('—')).toHaveLength(4));
    expect(screen.queryByText('Địa chỉ mạng LAN')).toBeNull();
  });
  it.each(['vi', 'en'])('shows read failure and updates only after a successful retry (%s)', async (lang) => {
    localStorage.setItem('sentinellan_lang', lang);
    fakeApi.endpointObservations.mockRejectedValueOnce(new Error('offline')).mockResolvedValue(observation);
    renderProviders(<EndpointObservationPanel deviceId="authorized-device" />);
    await screen.findByRole('alert'); expect(screen.queryByText('Office CPU')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: lang === 'vi' ? 'Cập nhật' : 'Refresh' }));
    await screen.findByText('Office CPU'); expect(fakeApi.endpointObservations).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
