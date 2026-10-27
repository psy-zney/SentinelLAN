import React from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import * as Router from 'expo-router';
import * as Auth from '../src/features/auth/auth-context';
import { MobileApiClient } from '../src/lib/api/client';
import type { SupportRequest } from '../src/lib/validation/self-service';
import IncidentsTab from '../src/app/(tabs)/incidents';
import { RequestCreateView } from '../src/features/self-service/request-create-view';
import { SelfServiceHomeView } from '../src/features/self-service/home-view';
import { cacheCreatedRequest } from '../src/features/self-service/request-cache';

const id = '10000000-0000-4000-8000-000000000001';
const request: SupportRequest = { id, deviceId: id, deviceName: 'Office PC', userId: id, userName: 'Employee', kind: 'Incident', category: 'Printer', title: 'Không in được', description: '', canWork: true, status: 'Open', assignedTechnicianId: null, assignedTechnicianName: null, catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: null, createdAt: '2026-10-27T08:00:00Z', updatedAt: '2026-10-27T08:00:00Z', approvalExpiresAt: null };

describe('Support data freshness across navigation', () => {
  let api: MobileApiClient;
  let client: QueryClient;
  const replace = jest.fn();
  beforeEach(() => {
    api = new MobileApiClient();
    client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 30000, gcTime: Infinity } } });
    replace.mockClear();
    jest.spyOn(Auth, 'useAuth').mockReturnValue({ status: 'authenticated', user: { id, role: 'Employee', displayName: 'An' }, apiClient: api } as ReturnType<typeof Auth.useAuth>);
    jest.spyOn(Router, 'useRouter').mockReturnValue({ replace, push: jest.fn() } as unknown as ReturnType<typeof Router.useRouter>);
    jest.mocked(Router.useLocalSearchParams).mockReturnValue({});
  });
  afterEach(async () => { await cleanup(); client.clear(); jest.restoreAllMocks(); jest.useRealTimers(); });
  const mount = (view: React.ReactElement) => render(<QueryClientProvider client={client}>{view}</QueryClientProvider>);

  it('shows new support reports in the report tab even if its cached list is still fresh', async () => {
    client.setQueryData(['support-requests', 0], []);
    const load = jest.spyOn(api, 'supportRequests').mockResolvedValue([request]);
    const legacy = jest.spyOn(api, 'myDeviceIncidents');
    await mount(<IncidentsTab />);
    await screen.findByText(request.title);
    expect(load).toHaveBeenCalledWith(0);
    expect(legacy).not.toHaveBeenCalled();
    await fireEvent(screen.getByTestId('service-refresh'), 'refresh');
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  });

  it('refreshes a cached home summary immediately after returning from request creation', async () => {
    client.setQueryData(['support-requests', 0], []);
    await cacheCreatedRequest(client, request);
    jest.spyOn(api, 'supportRequests').mockResolvedValue([request]);
    jest.spyOn(api, 'myDevice').mockResolvedValue(null);
    await mount(<SelfServiceHomeView />);
    await screen.findByText('Tiến độ hỗ trợ');
    expect(screen.getByLabelText('Đang xử lý: 1, Chờ bạn: 0, Đã xử lý: 0, Chưa duyệt: 0')).toBeTruthy();
  });

  it('polls while visible, pauses in the background and refreshes immediately on resume', async () => {
    jest.useFakeTimers();
    let change: (state: AppStateStatus) => void = () => {};
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_event, listener) => {
      change = listener;
      return { remove: jest.fn() };
    });
    const load = jest.spyOn(api, 'supportRequests').mockResolvedValue([request]);
    await mount(<IncidentsTab />);
    await act(async () => { await jest.advanceTimersByTimeAsync(50); });
    expect(load).toHaveBeenCalledTimes(1);
    await act(async () => { await jest.advanceTimersByTimeAsync(5000); });
    expect(load).toHaveBeenCalledTimes(2);
    await act(async () => { change('background'); });
    await act(async () => { await jest.advanceTimersByTimeAsync(15000); });
    expect(load).toHaveBeenCalledTimes(2);
    load.mockResolvedValue([{ ...request, status: 'Resolved' }]);
    await act(async () => { change('active'); });
    await act(async () => { await jest.advanceTimersByTimeAsync(50); });
    expect(load).toHaveBeenCalledTimes(3);
    expect(screen.getByText('IT đã xử lý')).toBeTruthy();
  });

  it('stores the received report even when an optional image upload fails, without creating a duplicate on retry', async () => {
    const create = jest.spyOn(api, 'createSupportRequest').mockResolvedValue(request);
    const upload = jest.spyOn(api, 'uploadSupportAttachment').mockRejectedValue(new Error('Upload offline'));
    const ImagePicker = require('expo-image-picker');
    ImagePicker.launchImageLibraryAsync.mockResolvedValueOnce({ canceled: false, assets: [{ uri: 'file:///error.png', fileName: 'error.png', mimeType: 'image/png', base64: 'iVBORw0KGgoAAAA=' }] });
    await mount(<RequestCreateView />);
    await fireEvent.press(screen.getByText('Chọn ảnh có sẵn'));
    await fireEvent(screen.getByLabelText('Tôi đã xem ảnh và đồng ý gửi cho IT'), 'valueChange', true);
    await fireEvent.press(screen.getByTestId('submit-incident-button'));
    await screen.findByText(/Yêu cầu đã gửi nhưng ảnh chưa gửi được/);
    expect(client.getQueryData(['support-request', id])).toEqual(request);
    expect(client.getQueryData(['support-requests', 0])).toEqual([request]);
    await fireEvent.press(screen.getByTestId('submit-incident-button'));
    await waitFor(() => expect(upload).toHaveBeenCalledTimes(2));
    expect(create).toHaveBeenCalledTimes(1);
    expect(replace).not.toHaveBeenCalled();
  });
});
