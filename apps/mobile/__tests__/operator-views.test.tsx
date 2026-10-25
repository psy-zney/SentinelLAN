import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { OperatorView } from '../src/features/it-operator/operator-view';
import { CompanyUsersView } from '../src/features/company-admin/users-view';
import { RequestListView } from '../src/features/self-service/request-list-view';
import { MobileApiClient } from '../src/lib/api/client';
import * as Auth from '../src/features/auth/auth-context';

const id = '00000000-0000-4000-8000-000000000001';
async function wrap(view: React.ReactElement) { return await render(<QueryClientProvider client={new QueryClient({defaultOptions:{queries:{retry:false,gcTime:0}}})}>{view}</QueryClientProvider>); }
function session(role: 'Admin'|'Employee', api: MobileApiClient) {
  jest.spyOn(Auth,'useAuth').mockReturnValue({status:'authenticated',user:{id,email:'it@test.local',displayName:'IT',role,organizationId:id,organizationCode:'test'},apiClient:api,login:jest.fn(),logout:jest.fn(),logoutAll:jest.fn(),retrySession:jest.fn(),dismissSessionExpired:jest.fn()});
}
afterEach(()=>jest.restoreAllMocks());
test('admin loads company devices and account actions without employee requests',async()=>{
  const api=new MobileApiClient('https://test.local');session('Admin',api);
  const push = jest.fn();
  jest.spyOn(require('expo-router'), 'useRouter').mockReturnValue({ push });
  const myDevice=jest.spyOn(api,'myDevice');
  jest.spyOn(api,'operatorDashboard').mockResolvedValue({totalDevices:1,onlineDevices:1,offlineDevices:0,openAlerts:2});
  jest.spyOn(api,'operatorDevices').mockResolvedValue([{id,name:'IT-PC',osVersion:'Windows',agentVersion:'1.0',lastSeenAt:null,isRevoked:false}]);
  await wrap(<OperatorView/>);
  await waitFor(()=>expect(screen.getByText('IT-PC')).toBeTruthy());
  expect(myDevice).not.toHaveBeenCalled();
  expect(screen.queryByText('Quản lý tài khoản công ty')).toBeTruthy();
  expect(screen.queryByText('Cấp mã dùng một lần, hạn 15 phút')).toBeNull();
  await fireEvent.press(screen.getByRole('button', { name: 'Đăng ký Agent mới' }));
  expect(screen.getByText('Cấp mã dùng một lần, hạn 15 phút')).toBeTruthy();
  await fireEvent.press(screen.getByText('Theo dõi VPS'));
  expect(push).toHaveBeenCalledWith('/vps-status');
});
test('employee cannot fetch the admin user directory',async ()=>{
  const api=new MobileApiClient('https://test.local');session('Employee',api);
  const users=jest.spyOn(api,'operatorUsers');await wrap(<CompanyUsersView/>);
  expect(screen.getByText('Chỉ quản trị viên được quản lý tài khoản nội bộ.')).toBeTruthy();
  expect(users).not.toHaveBeenCalled();
});
test('IT queue hides employee-only request creation',async()=>{
  const api=new MobileApiClient('https://test.local');session('Admin',api);
  const requests=jest.spyOn(api,'supportRequests').mockResolvedValue([]);await wrap(<RequestListView/>);
  await waitFor(()=>expect(screen.getByText('Bạn chưa có yêu cầu nào.')).toBeTruthy());
  expect(requests).toHaveBeenCalled();
  expect(screen.getByText('Hàng đợi hỗ trợ công ty')).toBeTruthy();
  expect(screen.queryByText('Gửi yêu cầu mới')).toBeNull();
});
