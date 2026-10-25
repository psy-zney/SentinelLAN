// @vitest-environment jsdom
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { fakeApi, resetApi, renderProviders, account, policy } from './helpers';
import { translations } from '../src/lib/i18n';
import { UsersView } from '../src/components/users-view';
import { PoliciesView } from '../src/components/policies-view';
import { CommandsView } from '../src/components/commands-view';
import { AlertsView } from '../src/components/alerts-view';
import { AuditView } from '../src/components/audit-view';
import { DepartmentPanel } from '../src/components/department-panel';

vi.mock('@/lib/api-client', async () => { const { fakeApi } = await import('./helpers'); return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number, message = 'API unavailable') { super(message); } } }; });
vi.mock('@/components/app-shell', async () => { const { auth } = await import('./helpers'); return { useCurrentSession: () => auth.session }; });
vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: () => {} }));
const t = translations.vi;
const inventoryHarness = vi.hoisted(() => ({ lightweightTable: false }));
vi.mock('@/components/device-table', async importOriginal => {
  const actual = await importOriginal<typeof import('../src/components/device-table')>();
  return { DeviceTable: (props: React.ComponentProps<typeof actual.DeviceTable>) => inventoryHarness.lightweightTable
    ? <div>{props.initialDevices.map(device => <span key={device.id}>{device.name}</span>)}</div>
    : <actual.DeviceTable {...props} /> };
});
beforeEach(() => { resetApi(); inventoryHarness.lightweightTable = false; });

async function change(label: string | RegExp, value: string) { fireEvent.change(screen.getByLabelText(label), { target: { value } }); }

describe('administrator account workflows', () => {
  it('shows an internal staff directory without organization metadata and identifies the signed-in administrator', async () => {
    fakeApi.organization.mockRejectedValue(new Error('Organization metadata is not part of staff management'));
    fakeApi.users.mockResolvedValue([{ ...account, id: 'admin', role: 'Admin', displayName: 'Organization Administrator' }]);
    renderProviders(<UsersView />);
    await screen.findByText(account.email);
    expect(fakeApi.organization).not.toHaveBeenCalled();
    expect(screen.queryByLabelText('Organization identity')).toBeNull();
    expect(screen.queryByText('Mã công ty')).toBeNull();
    expect(screen.queryByText('Khởi tạo ngày')).toBeNull();
    expect(screen.queryByText('Organization Administrator')).toBeNull();
    expect(screen.getByText('Quản trị hệ thống')).toBeTruthy();
    expect(screen.getByText('Đang đăng nhập')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Khóa' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: '+ Tạo tài khoản' }));
    const modal = screen.getByRole('dialog');
    expect(within(modal).getByRole('option', { name: 'Nhân viên' })).toBeTruthy();
    expect(within(modal).getByRole('option', { name: 'Quản trị viên' })).toBeTruthy();
  });

  it('creates a confirmed invitation without showing a password and returns its one-use link', async () => {
    fakeApi.createUser.mockResolvedValue({ activationUrl: '/activate#token=one-use-token', activationToken: 'one-use-token', user: account });
    renderProviders(<UsersView />);
    await screen.findByText(account.email);
    await userEvent.click(screen.getByRole('button', { name: '+ Tạo tài khoản' }));
    const modal = screen.getByRole('dialog');
    await change(t.userDisplayName, 'New Employee'); await change(t.userEmail, 'new@example.test');
    await change('Lý do tạo tài khoản', 'New employee onboarding');
    expect((within(modal).getByRole('button', { name: t.save }) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(within(modal).getByRole('checkbox'));
    await userEvent.click(within(modal).getByRole('button', { name: t.save }));
    await waitFor(() => expect(fakeApi.createUser).toHaveBeenCalledWith(expect.objectContaining({ email: 'new@example.test', confirmed: true, role: 'Employee' })));
    await screen.findByDisplayValue(/one-use-token/);
    expect(document.querySelector('input[type=password]')).toBeNull();
  });
  it('retries a directory failure and paginates accounts on the server', async () => {
    fakeApi.users.mockRejectedValueOnce(new Error('offline')).mockResolvedValue(Array.from({ length: 100 }, (_, i) => ({...account, id: String(i), email: `staff${i}@example.test`})));
    renderProviders(<UsersView />);
    await screen.findByRole('alert'); await userEvent.click(screen.getByRole('button', {name:t.retry}));
    await screen.findByText('staff0@example.test'); await userEvent.click(screen.getByRole('button', {name:'Trang sau'}));
    await waitFor(() => expect(fakeApi.users).toHaveBeenCalledWith(100,100));
  });
  it('keeps account status changes behind an audit reason and explicit confirmation', async () => {
    renderProviders(<UsersView />); await screen.findByText(account.email);
    await userEvent.click(screen.getByRole('button', {name:'Khóa'}));
    const modal = screen.getByRole('dialog');
    const input = within(modal).getByRole('textbox'); fireEvent.change(input, {target:{value:'Security incident'}});
    const check = within(modal).getByRole('checkbox'); await userEvent.click(check);
    const submit = modal.querySelector('button[type=submit]') as HTMLButtonElement; await userEvent.click(submit);
    await waitFor(()=>expect(fakeApi.setUserStatus).toHaveBeenCalledWith(account.id,'Locked','Security incident',true));
  });
});

describe('policy and organization changes', () => {
  it('creates a policy, edits it and assigns it to an authorized device', async () => {
    renderProviders(<PoliciesView />); await screen.findByText(policy.name);
    await userEvent.click(screen.getByRole('button',{name:t.createPolicyBtn}));
    await change(t.policyNameLabel,'New policy');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.createPolicy).toHaveBeenCalledWith({name:'New policy',idleTimeoutMinutes:15,usbMode:'Blocked'}));
    await userEvent.click(screen.getByRole('button',{name:'Sửa'}));
    await change(t.policyNameLabel,'Updated policy'); await change(t.idleTimeoutLabel,'30');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.updatePolicy).toHaveBeenCalledWith(policy.id,expect.objectContaining({name:'Updated policy',idleTimeoutMinutes:30})));
    await userEvent.click(screen.getByRole('button',{name:t.assignPolicyBtn}));
    await screen.findByLabelText(t.selectDevicePrompt);
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.confirm}));
    await waitFor(()=>expect(fakeApi.assignPolicy).toHaveBeenCalledWith(policy.id,'device-1'));
  });
  it('reports policy API errors and allows a failed creation to be retried', async () => {
    fakeApi.createPolicy.mockRejectedValueOnce(new Error('offline'));
    renderProviders(<PoliciesView />); await screen.findByText(policy.name);
    await userEvent.click(screen.getByRole('button',{name:t.createPolicyBtn})); await change(t.policyNameLabel,'Retry policy');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await screen.findByText('Failed to create policy.');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.createPolicy).toHaveBeenCalledTimes(2));
  });
  it('requires organization confirmation and sends the revision when editing or assigning membership', async () => {
    renderProviders(<DepartmentPanel />);
    await userEvent.click(screen.getByText('Phòng ban và chính sách mặc định'));
    await screen.findByRole('option',{name:'Finance (1 máy)'});
    await change('Tên phòng ban','Sales'); await change('Lý do thay đổi','Company restructure');
    expect((screen.getByRole('button',{name:'Lưu phòng ban'}) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(screen.getByRole('checkbox'));
    await userEvent.click(screen.getByRole('button',{name:'Lưu phòng ban'}));
    await waitFor(()=>expect(fakeApi.saveDepartment).toHaveBeenCalledWith(expect.objectContaining({name:'Sales',policyId:null}),'Company restructure',undefined));
    await userEvent.click(screen.getByText('Nhân viên'));
    await userEvent.selectOptions(screen.getByLabelText(`Phòng ban của ${account.displayName}`),'dept');
    await waitFor(()=>expect(fakeApi.assignDepartment).toHaveBeenCalledWith('users',account.id,'dept','revision','Company restructure'));
  });
});

describe('operational commands, alerts and audit', () => {
  it.each([['ShowNotification','cmd-notif','Maintenance soon','Maintenance soon'],['RestartService','cmd-svc','nginx','nginx'],['IsolateNetwork','cmd-isolation-seconds','45','45']])('confirms %s command parameters and the bounded validity window', async (kind, field, value, parameter) => {
    renderProviders(<CommandsView />); await screen.findByRole('button',{name:t.dispatchCommandBtn});
    await userEvent.click(screen.getByRole('button',{name:t.dispatchCommandBtn}));
    await screen.findByLabelText(t.commandTypeLabel); fireEvent.change(document.getElementById('cmd-type')!,{target:{value:kind}});
    fireEvent.change(document.getElementById(field)!,{target:{value}});
    await change(t.commandReasonLabel,'Approved maintenance');
    const submit = screen.getByRole('button',{name:t.commandSignAndDispatch}); expect((submit as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(document.getElementById('cmd-confirm')!); await userEvent.click(submit);
    await waitFor(()=>expect(fakeApi.createCommand).toHaveBeenCalledWith('device-1',kind,'Approved maintenance',true,300,parameter));
  });
  it('filters and acknowledges an alert, preserving errors and allowing resolution', async () => {
    fakeApi.alerts.mockResolvedValue([{id:'alert',message:'CPU hot',severity:'Critical',isOpen:true,createdAt:'2026-01-01T00:00:00Z'},{id:'done',message:'Old alert',severity:'Info',isOpen:false,createdAt:'2026-01-01T00:00:00Z'}]);
    fakeApi.acknowledgeAlert.mockRejectedValueOnce(new Error('offline'));
    renderProviders(<AlertsView />); await screen.findByText('CPU hot');
    expect(screen.queryByText('Old alert')).toBeNull();
    await userEvent.click(screen.getByRole('button',{name:t.ackBtn})); await screen.findByText('Failed to acknowledge alert.');
    await userEvent.click(screen.getByRole('button',{name:t.ackBtn})); await waitFor(()=>expect(fakeApi.acknowledgeAlert).toHaveBeenCalledTimes(2));
    await userEvent.click(screen.getByRole('button',{name:t.resolveBtn})); await waitFor(()=>expect(fakeApi.resolveAlert).toHaveBeenCalledWith('alert'));
    await userEvent.click(screen.getByRole('button',{name:`${t.all} (2)`})); await screen.findByText('Old alert');
  });
  it('searches audit reasons and requests an action filter from the server', async () => {
    fakeApi.auditLogs.mockResolvedValue([{id:'audit',actorName:'Admin',action:'CommandCreated',reason:'Approved maintenance',outcome:'Success',createdAt:'2026-01-01T00:00:00Z'}]);
    renderProviders(<AuditView />); await screen.findByText('Approved maintenance');
    fireEvent.change(screen.getByRole('textbox'),{target:{value:'not present'}}); await screen.findByText(t.noAuditLogs);
    fireEvent.change(screen.getByRole('textbox'),{target:{value:''}});
    await userEvent.selectOptions(screen.getByRole('combobox'),'Policy');
    await waitFor(()=>expect(fakeApi.auditLogs).toHaveBeenCalledWith(undefined,'Policy'));
  });
});
import { DevicesView } from '../src/components/devices-view';
import { DeviceTable } from '../src/components/device-table';
import { DeviceDetail } from '../src/components/device-detail';
import { EnrollmentPackagesPanel } from '../src/components/enrollment-packages-panel';
import { device } from './helpers';

describe('device inventory and enrollment', () => {
  it('paginates and searches server inventory, resetting filters to page one', async () => {
    inventoryHarness.lightweightTable = true;
    fakeApi.devicePage.mockResolvedValueOnce(Array.from({length:100},(_,i)=>({...device,id:String(i),name:`Workstation ${i}`}))).mockResolvedValue([{...device,name:'Finance workstation'}]);
    renderProviders(<DevicesView />); await screen.findByText('Workstation 0');
    fireEvent.click(screen.getByText('Trang sau'));
    await waitFor(()=>expect(fakeApi.devicePage).toHaveBeenCalledWith(100,100,'','all'));
    fireEvent.change(screen.getByLabelText('Tìm thiết bị'), { target: { value: 'Finance' } });
    await waitFor(()=>expect(fakeApi.devicePage).toHaveBeenCalledWith(0,100,'Finance','all'));
    fireEvent.change(screen.getByLabelText(t.filter), { target: { value: 'offline' } });
    await waitFor(()=>expect(fakeApi.devicePage).toHaveBeenCalledWith(0,100,'Finance','offline'));
  });
  it('requires consent before exposing a one-use enrollment code and then hides it on close', async () => {
    fakeApi.createEnrollmentToken.mockResolvedValue({connectionCode:'sentinellan-private-code',expiresAt:'2026-01-01T00:15:00Z'});
    renderProviders(<DevicesView />); await screen.findByText(device.name);
    await userEvent.click(screen.getByRole('button',{name:'+ Tạo mã kết nối thiết bị'}));
    const modal=screen.getByRole('dialog');
    fireEvent.change(within(modal).getByLabelText('Lý do'),{target:{value:'Authorized office setup'}});
    expect((within(modal).getByRole('button',{name:t.save}) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(within(modal).getByRole('checkbox')); await userEvent.click(within(modal).getByRole('button',{name:t.save}));
    await screen.findByDisplayValue('sentinellan-private-code');
    expect(fakeApi.createEnrollmentToken).toHaveBeenCalledWith({validForMinutes:15,reason:'Authorized office setup',confirmed:true});
    await userEvent.click(within(modal).getByRole('button',{name:'Đóng'})); expect(screen.queryByDisplayValue('sentinellan-private-code')).toBeNull();
  });
  it('dispatches bulk telemetry only after selecting a device and explicit confirmation', async () => {
    fakeApi.bulkCommands.mockResolvedValue([{deviceId:device.id,outcome:'Queued'}]);
    renderProviders(<DeviceTable initialDevices={[device]} />);
    expect((screen.getByRole('button',{name:'Gửi lệnh'}) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(screen.getByLabelText(`Chọn ${device.name}`)); await change('Lý do gửi hàng loạt','Routine diagnosis');
    await userEvent.click(screen.getByLabelText('Tôi xác nhận gửi đến các máy đã chọn')); await userEvent.click(screen.getByRole('button',{name:'Gửi lệnh'}));
    await waitFor(()=>expect(fakeApi.bulkCommands).toHaveBeenCalledWith([device.id],'CollectTelemetryNow','Routine diagnosis'));
    await screen.findByText(/Office PC: Đã xếp hàng/);
    expect((screen.getByLabelText(`Chọn ${device.name}`) as HTMLInputElement).checked).toBe(false);
  });
  it('issues a bounded installation batch and removes secrets from the page on request', async () => {
    fakeApi.enrollmentPackages.mockResolvedValue([{fileName:'setup.ps1',script:'authorized setup',connectionCode:'private-install-token',expiresAt:'2026-01-01T00:15:00Z'}]);
    renderProviders(<EnrollmentPackagesPanel />); await userEvent.click(screen.getByText('Cài Agent nhanh / cấp lô tối đa 50 máy'));
    await change('Số máy','2'); await change('Lý do','New office machines');
    await userEvent.click(screen.getByRole('checkbox')); await userEvent.click(screen.getByRole('button',{name:'Cấp gói cài 15 phút'}));
    await screen.findByDisplayValue('private-install-token'); expect(fakeApi.enrollmentPackages).toHaveBeenCalledWith(2,'Windows','New office machines');
    await userEvent.click(screen.getByRole('button',{name:'Ẩn các mã'})); expect(screen.queryByDisplayValue('private-install-token')).toBeNull();
  });
  it('shows unavailable asset data and recovers through the retry control', async () => {
    fakeApi.getDeviceAssetDetail.mockRejectedValue(new Error('offline'));
    renderProviders(<DeviceDetail id={device.id} />);
    await screen.findByRole('alert'); expect(screen.getByText('Không tìm thấy hồ sơ thiết bị')).toBeTruthy();
    await userEvent.click(screen.getByRole('button',{name:t.retry})); await waitFor(()=>expect(fakeApi.getDeviceAssetDetail).toHaveBeenCalledTimes(2));
  });
});
const asset = {...device, serialNumber:'SERIAL-1', manufacturer:'Office Vendor',model:'Workstation',healthScore:{score:70,grade:'Fair',recommendations:['Clean ventilation']},repairVsReplace:{recommendation:'Keep & Maintain',analysisSummary:'Maintenance is economical',replacementCost:1200,cumulativeMaintenanceCost:100,maintenanceToCostRatioPercent:10,estimatedNext3YearsCost:300},specificationsJson:'{"cpu":"i7"}',purchaseCost:1000};
function assetFixtures() { fakeApi.getDeviceAssetDetail.mockResolvedValue(asset); fakeApi.allUsers.mockResolvedValue([account]); fakeApi.getQrLabel.mockResolvedValue(null); }
describe('asset lifecycle operations',()=>{
  it('loads technical data, records audited assignment and displays lifecycle analysis tabs',async()=>{
    assetFixtures(); renderProviders(<DeviceDetail id={device.id}/>);
    await screen.findAllByText('SERIAL-1');
    await userEvent.selectOptions(screen.getByLabelText('Employee'),account.id);
    await change('Lý do thay đổi','Issued to new employee');
    await userEvent.click(screen.getByLabelText('Tôi xác nhận giao thiết bị cho người đã chọn.'));
    const assignment=screen.getByLabelText('Employee').closest('.panel')!;
    await userEvent.click(within(assignment as HTMLElement).getByRole('button'));
    await waitFor(()=>expect(fakeApi.assignDevice).toHaveBeenCalledWith(device.id,account.id,'Issued to new employee',true));
    const tabs=Array.from(document.querySelectorAll('.tabs-bar button'));
    for(const tab of tabs) await userEvent.click(tab);
    await screen.findByText('Maintenance is economical');
  });
  it('keeps profile edits gated by reason and confirmation and submits normalized values',async()=>{
    assetFixtures(); renderProviders(<DeviceDetail id={device.id}/>); await screen.findAllByText('SERIAL-1');
    await userEvent.click(screen.getByRole('button',{name:new RegExp(t.editAssetProfile)}));
    const modal=screen.getByRole('dialog');const inputs=within(modal).getAllByRole('textbox');
    fireEvent.change(inputs[0],{target:{value:'UPDATED-SERIAL'}}); fireEvent.change(inputs[inputs.length-1],{target:{value:'Annual inventory'}});
    expect((within(modal).getByRole('button',{name:t.save}) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(within(modal).getByRole('checkbox')); await userEvent.click(within(modal).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.updateAssetProfile).toHaveBeenCalledWith(device.id,expect.objectContaining({serialNumber:'UPDATED-SERIAL',reason:'Annual inventory',confirmed:true})));
  });
  it('reports an incident with an idempotency key then creates a maintenance work order',async()=>{
    assetFixtures(); renderProviders(<DeviceDetail id={device.id}/>); await screen.findAllByText('SERIAL-1');
    await userEvent.click(screen.getByRole('button',{name:t.reportIncident}));
    await userEvent.type(screen.getByPlaceholderText('VD: Pin phồng, quạt kêu to...'),'Fan noise');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.createIncident).toHaveBeenCalledWith(expect.objectContaining({deviceId:device.id,title:'Fan noise',idempotencyKey:expect.any(String)})));
    await userEvent.click(screen.getByRole('button',{name:t.createWorkOrder}));
    await userEvent.type(screen.getByPlaceholderText('VD: Thay keo tản nhiệt, vệ sinh định kỳ...'),'Clean fan');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:'Tạo phiếu'}));
    await waitFor(()=>expect(fakeApi.createWorkOrder).toHaveBeenCalledWith(expect.objectContaining({deviceId:device.id,title:'Clean fan'})));
  });
});
it('clears inventory selection when changing pages so hidden devices cannot receive the next bulk action',async()=>{
  fakeApi.devicePage.mockImplementation(async(offset:number)=>offset===0?Array.from({length:100},(_,i)=>({...device,id:`page-one-${i}`,name:`First page ${i}`})):[{...device,id:'second-page',name:'Second page PC'}]);
  renderProviders(<DevicesView/>); await screen.findByText('First page 0');
  await userEvent.click(screen.getByLabelText('Chọn First page 0'));
  expect(within(screen.getByRole('region',{name:'Thiết bị sẽ nhận lệnh'})).getByText('First page 0')).toBeTruthy();
  await userEvent.click(screen.getByRole('button',{name:'Trang sau'})); await screen.findByText('Second page PC');
  expect(screen.queryByRole('region',{name:'Thiết bị sẽ nhận lệnh'})).toBeNull();
  expect((screen.getByRole('button',{name:'Gửi lệnh'}) as HTMLButtonElement).disabled).toBe(true);
  await userEvent.click(screen.getByLabelText('Chọn Second page PC'));
  expect(screen.getByRole('region',{name:'Thiết bị sẽ nhận lệnh'}).textContent).not.toContain('First page 0');
});

describe('operational failure handling',()=>{
  it.each([[PoliciesView,'policies'],[CommandsView,'commands'],[AlertsView,'alerts'],[AuditView,'auditLogs']] as const)('recovers a failed list request through retry',async(Component,method)=>{
    fakeApi[method].mockRejectedValueOnce(new Error('offline')).mockResolvedValue([]);
    renderProviders(<Component/>); await screen.findByRole('alert'); await userEvent.click(screen.getByRole('button',{name:t.retry}));
    await waitFor(()=>expect(fakeApi[method]).toHaveBeenCalledTimes(2)); await waitFor(()=>expect(screen.queryByRole('alert')).toBeNull());
  });
  it('keeps a failed command dispatch open and retains operator input for retry',async()=>{
    fakeApi.createCommand.mockRejectedValueOnce(new Error('offline'));
    renderProviders(<CommandsView/>); await screen.findByRole('button',{name:t.dispatchCommandBtn});
    await userEvent.click(screen.getByRole('button',{name:t.dispatchCommandBtn})); await screen.findByLabelText(t.commandReasonLabel);
    await change(t.commandReasonLabel,'Authorized retry'); await userEvent.click(document.getElementById('cmd-confirm')!);
    await userEvent.click(screen.getByRole('button',{name:t.commandSignAndDispatch})); await screen.findByText('Failed to dispatch command.');
    expect((screen.getByLabelText(t.commandReasonLabel) as HTMLInputElement).value).toBe('Authorized retry');
    await userEvent.click(screen.getByRole('button',{name:t.commandSignAndDispatch})); await waitFor(()=>expect(fakeApi.createCommand).toHaveBeenCalledTimes(2));
  });
  it('shows that no device is available instead of offering an invalid command form',async()=>{
    fakeApi.devices.mockResolvedValue([]); renderProviders(<CommandsView/>); await screen.findByRole('button',{name:t.dispatchCommandBtn});
    await userEvent.click(screen.getByRole('button',{name:t.dispatchCommandBtn})); await screen.findByText('Không có thiết bị nào sẵn sàng nhận lệnh.');
    expect(screen.queryByLabelText(t.commandReasonLabel)).toBeNull();
  });
});
describe('invitations and quick support actions',()=>{
  it('reissues an expired invitation, exposes the replacement once and then revokes invitations explicitly',async()=>{
    fakeApi.users.mockResolvedValue([{...account,status:'PendingActivation'}]);
    fakeApi.reissueActivationToken.mockResolvedValue({activationUrl:'/activate#token=new-invite'});
    renderProviders(<UsersView/>); await screen.findByText(account.email);
    await userEvent.click(screen.getByRole('button',{name:t.reissueInvite}));
    await userEvent.type(screen.getByPlaceholderText('VD: Nhân viên chưa nhận được link kích hoạt...'),'Previous invitation expired');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.confirm}));
    await waitFor(()=>expect(fakeApi.reissueActivationToken).toHaveBeenCalledWith(account.id,'Previous invitation expired',true));
    await screen.findByDisplayValue(/new-invite/); await userEvent.click(screen.getByRole('button',{name:'Đã sao chép & Đóng'}));
    await userEvent.click(screen.getByRole('button',{name:new RegExp(t.revokeInvite)}));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.confirm}));
    await waitFor(()=>expect(fakeApi.revokeActivationToken).toHaveBeenCalledWith(account.id,'Admin revoked invite',true));
  });
  it('retains failed invitation input and displays a conflict response without creating a second account',async()=>{
    fakeApi.createUser.mockRejectedValue(new Error('offline'));
    renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Tạo tài khoản'}));
    await change(t.userDisplayName,'New colleague');await change(t.userEmail,'new@example.test');await change('Lý do tạo tài khoản','Onboarding');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox'));await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
    await waitFor(()=>expect(fakeApi.createUser).toHaveBeenCalledTimes(1));await screen.findByRole('alert');
    expect((screen.getByLabelText(t.userEmail) as HTMLInputElement).value).toBe('new@example.test');
  });
  it('sends a quick Windows notification and reports the result without silently closing the dialog',async()=>{
    renderProviders(<DeviceTable initialDevices={[device]}/>);await userEvent.click(screen.getByRole('button',{name:'Thao tác'}));
    await userEvent.type(screen.getByPlaceholderText('Nhập thông báo gửi đến nhân viên...'),'Maintenance starts at 18:00');
    await userEvent.type(screen.getByPlaceholderText('VD: Kiểm tra phản hồi bảo mật định kỳ...'),'Authorized maintenance');
    await userEvent.click(screen.getByRole('button',{name:'Gửi thao tác'}));
    await waitFor(()=>expect(fakeApi.createCommand).toHaveBeenCalledWith(expect.objectContaining({deviceId:device.id,type:'ShowNotification',reason:'Authorized maintenance'})));
    expect(screen.getByRole('dialog')).toBeTruthy();
  });
  it('creates an organization-wide warning after choosing its severity and message',async()=>{
    renderProviders(<AlertsView/>);await screen.findByRole('button',{name:t.triggerAlertBtn});
    await userEvent.click(screen.getByRole('button',{name:t.triggerAlertBtn}));await screen.findByLabelText(t.alertDeviceLabel);
    await userEvent.selectOptions(screen.getByLabelText(t.alertDeviceLabel),'');await userEvent.selectOptions(screen.getByLabelText(t.alertSeverityLabel),'Warning');
    fireEvent.change(within(screen.getByRole('dialog')).getByRole('textbox'),{target:{value:'Maintenance notice'}});
    const form=screen.getByRole('dialog').querySelector('form')!;fireEvent.submit(form);
    await waitFor(()=>expect(fakeApi.createAlert).toHaveBeenCalledWith({deviceId:null,severity:'Warning',message:'Maintenance notice'}));
  });
});
describe('alternate locales and failed changes',()=>{
  it.each([UsersView,PoliciesView,CommandsView,AlertsView,AuditView,DevicesView])('renders operational controls in English with empty lists',async Component=>{
    localStorage.setItem('sentinellan_lang','en');fakeApi.users.mockResolvedValue([]);fakeApi.policies.mockResolvedValue([]);fakeApi.devicePage.mockResolvedValue([]);
    renderProviders(<Component/>);await waitFor(()=>expect(document.querySelector('[role=status]')).toBeNull());
    expect(screen.getAllByRole('button').length).toBeGreaterThan(0);
  });
  it('does not show a successful quick command when the server rejects it',async()=>{
    fakeApi.createCommand.mockRejectedValue(new Error('denied'));renderProviders(<DeviceTable initialDevices={[{...device,isOnline:false,lastSeenAt:null,assignedUserId:null}]}/>);
    await userEvent.click(screen.getByRole('button',{name:'Thao tác'}));await userEvent.selectOptions(within(screen.getByRole('dialog')).getByRole('combobox'),'RefreshPolicy');
    await userEvent.type(screen.getByPlaceholderText('VD: Kiểm tra phản hồi bảo mật định kỳ...'),'Policy refresh');await userEvent.click(screen.getByRole('button',{name:'Gửi thao tác'}));
    await screen.findByText('Lỗi gửi lệnh điều khiển. Vui lòng kiểm tra quyền.');await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:t.cancel})[0]);expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('retries a quick incident with the same idempotency key and closes after success',async()=>{
    fakeApi.createIncident.mockRejectedValueOnce(new Error('offline'));renderProviders(<DeviceTable initialDevices={[device]}/>);
    await userEvent.click(screen.getByTitle(t.reportIncident));await userEvent.type(screen.getByPlaceholderText('VD: Quạt kêu to, màn hình nhấp nháy, pin chai...'),'Fan failure');
    const modal=screen.getByRole('dialog');await userEvent.selectOptions(within(modal).getByRole('combobox'),'High');
    await userEvent.type(screen.getByPlaceholderText('Mô tả thêm hiện tượng, mã lỗi, hoặc thời điểm phát sinh...'),'Details');
    fireEvent.submit(modal.querySelector('form')!);await screen.findByText('Lỗi tạo phiếu báo hỏng.');
    fireEvent.submit(modal.querySelector('form')!);await screen.findByText('Đã tiếp nhận báo hỏng thành công!');
    expect(fakeApi.createIncident.mock.calls[0][0].idempotencyKey).toBe(fakeApi.createIncident.mock.calls[1][0].idempotencyKey);
    await waitFor(()=>expect(screen.queryByRole('dialog')).toBeNull(),{timeout:2500});
  });
  it('shows a bulk rejection and prevents revoked machines from being selected',async()=>{
    fakeApi.bulkCommands.mockRejectedValue(new Error('Permission denied'));renderProviders(<DeviceTable initialDevices={[device,{...device,id:'revoked',name:'Revoked PC',isRevoked:true}]}/>);
    expect((screen.getByLabelText('Chọn Revoked PC') as HTMLInputElement).disabled).toBe(true);
    await userEvent.click(screen.getByLabelText(`Chọn ${device.name}`));await change('Lý do gửi hàng loạt','Routine collection');await userEvent.selectOptions(screen.getByLabelText('Lệnh cho các máy đã chọn'),'RefreshPolicy');
    await userEvent.click(screen.getByLabelText('Tôi xác nhận gửi đến các máy đã chọn'));await userEvent.click(screen.getByRole('button',{name:'Gửi lệnh'}));await screen.findByText('Permission denied');
  });
  it.each(['createAlert','resolveAlert'])('shows a failed %s action without hiding current records',async method=>{
    fakeApi.alerts.mockResolvedValue([{id:'alert',message:'Resource warning',severity:'Warning',isOpen:true,acknowledgedAt:'2026-01-01',createdAt:'2026-01-01'}]);fakeApi[method].mockRejectedValue(new Error('offline'));
    renderProviders(<AlertsView/>);await screen.findByText('Resource warning');
    if(method==='resolveAlert'){await userEvent.click(screen.getByRole('button',{name:t.resolveBtn}));await screen.findByText('Failed to resolve alert.');}
    else {await userEvent.click(screen.getByRole('button',{name:t.triggerAlertBtn}));await screen.findByLabelText(t.alertMessageLabel);await change(t.alertMessageLabel,'Office alert');fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findByText('Failed to trigger alert.');}
    expect(screen.getByText('Resource warning')).toBeTruthy();
  });
  it('keeps a failed policy edit open and reports failed device assignment',async()=>{
    fakeApi.updatePolicy.mockRejectedValue(new Error('denied'));fakeApi.assignPolicy.mockRejectedValue(new Error('offline'));
    renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getByRole('button',{name:'Sửa'}));await userEvent.selectOptions(within(screen.getByRole('dialog')).getByLabelText(t.usbModeLabel),'ReadOnly');
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));await screen.findByText('Không thể cập nhật chính sách.');await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:t.cancel})[0]);
    await userEvent.click(screen.getByRole('button',{name:t.assignPolicyBtn}));await screen.findByLabelText(t.selectDevicePrompt);await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.confirm}));await screen.findByText('Failed to assign policy to device.');
  });
  it('shows no assignable device instead of submitting an empty assignment',async()=>{
    fakeApi.devices.mockResolvedValue([]);renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getByRole('button',{name:t.assignPolicyBtn}));await screen.findByText('Không tìm thấy thiết bị nào để gán.');expect(fakeApi.assignPolicy).not.toHaveBeenCalled();
  });
});
import { ApiError } from '../src/lib/api-client';
it.each([409,403])('reports account creation permission/conflict %s without clearing the form',async status=>{
  fakeApi.createUser.mockRejectedValue(new ApiError(status));renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Tạo tài khoản'}));
  await change(t.userDisplayName,'Test employee');await change(t.userEmail,'test@example.test');await change('Lý do tạo tài khoản','Authorized onboarding');await userEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox'));await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.save}));
  await screen.findAllByText(status===409?'Email này đã có tài khoản trong hệ thống.':'Bạn không có quyền tạo tài khoản.');
});
it.each(['reissueActivationToken','revokeActivationToken'])('keeps invitation %s failure visible for retry',async method=>{
  fakeApi.users.mockResolvedValue([{...account,status:'PendingActivation'}]);fakeApi[method].mockRejectedValue(new Error('offline'));renderProviders(<UsersView/>);await screen.findByText(account.email);
  if(method==='reissueActivationToken'){await userEvent.click(screen.getByRole('button',{name:t.reissueInvite}));await userEvent.type(screen.getByPlaceholderText('VD: Nhân viên chưa nhận được link kích hoạt...'),'Expired invitation');}
  else await userEvent.click(screen.getByRole('button',{name:new RegExp(t.revokeInvite)}));
  await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:t.confirm}));await screen.findByText(method==='reissueActivationToken'?'Không thể cấp lại liên kết kích hoạt.':'Không thể thu hồi liên kết kích hoạt.');
});
it.each([false,true])('handles locked account transition with server conflict=%s',async conflict=>{
  fakeApi.users.mockResolvedValue([{...account,status:'Locked'}]);if(conflict)fakeApi.setUserStatus.mockRejectedValue(new ApiError(409));renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'Mở khóa'}));
  const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByRole('textbox'),{target:{value:'Verified employee identity'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);
  await screen.findAllByText(conflict?'Không thể khóa chính mình hoặc Admin cuối cùng.':'Đã mở khóa tài khoản; người dùng cần đăng nhập lại.');
});
it('reports failed department mutations and preserves existing membership',async()=>{
  fakeApi.saveDepartment.mockRejectedValue(new Error('Revision conflict'));renderProviders(<DepartmentPanel/>);await userEvent.click(screen.getByText('Phòng ban và chính sách mặc định'));await screen.findByRole('option',{name:'Finance (1 máy)'});
  await userEvent.selectOptions(screen.getByLabelText('Phòng ban'),'dept');await change('Tên phòng ban','Updated finance');await change('Lý do thay đổi','Restructure finance');await userEvent.selectOptions(screen.getByLabelText('Chính sách mặc định'),policy.id);await userEvent.click(screen.getByRole('checkbox'));await userEvent.click(screen.getByRole('button',{name:'Lưu phòng ban'}));await screen.findByText('Revision conflict');
});
it.each([403,503,500])('reports enrollment denial/unconfigured server %s without exposing a token',async status=>{
  fakeApi.createEnrollmentToken.mockRejectedValue(new ApiError(status));renderProviders(<DevicesView/>);await screen.findByText(device.name);await userEvent.click(screen.getByRole('button',{name:'+ Tạo mã kết nối thiết bị'}));const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByLabelText('Lý do'),{target:{value:'Authorized setup'}});fireEvent.change(within(modal).getByLabelText('Thời hạn (phút)'),{target:{value:'30'}});await userEvent.click(within(modal).getByRole('checkbox'));await userEvent.click(within(modal).getByRole('button',{name:t.save}));await within(modal).findByRole('alert');expect(within(modal).queryByLabelText('Mã kết nối')).toBeNull();
});
it('edits all policy creation values and supports cancelling draft creation',async()=>{
  renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getByRole('button',{name:t.createPolicyBtn}));const modal=screen.getByRole('dialog');
  await change(t.policyNameLabel,'Portable workstations');fireEvent.change(within(modal).getByLabelText(t.idleTimeoutLabel),{target:{value:'10'}});await userEvent.selectOptions(within(modal).getByLabelText(t.usbModeLabel),'FullAccess');fireEvent.submit(modal.querySelector('form')!);
  await waitFor(()=>expect(fakeApi.createPolicy).toHaveBeenCalledWith({name:'Portable workstations',idleTimeoutMinutes:10,usbMode:'FullAccess'}));
  await userEvent.click(screen.getByRole('button',{name:t.createPolicyBtn}));await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:t.cancel})[0]);expect(screen.queryByRole('dialog')).toBeNull();
});
it('displays persisted command outcomes without claiming a simulation was real execution',async()=>{
  fakeApi.commands.mockResolvedValue(['ExecutionUnconfirmed','Succeeded','Failed','Delivered','Pending','Expired'].map((status,i)=>({id:String(i),deviceName:device.name,type:i===1?'SimulateLock':'CollectTelemetryNow',status,issuedAt:'2026-01-01',expiresAt:'2026-01-02',reason:'Authorized check',succeeded:status==='Succeeded'?true:status==='Failed'?false:null})));
  renderProviders(<CommandsView/>);await screen.findByText('Đã mô phỏng');expect(screen.getByText('Chưa xác nhận kết quả')).toBeTruthy();expect(screen.getByText('Expired')).toBeTruthy();
});
it('filters resolved audit outcomes and permits refreshing after local search',async()=>{
  fakeApi.auditLogs.mockResolvedValue(['Failed','Pending','Custom','Resolved'].map((outcome,i)=>({id:String(i),actorName:null,deviceName:null,action:'Policy',reason:`Reason ${i}`,outcome,createdAt:'2026-01-01'})));
  renderProviders(<AuditView/>);await screen.findByText('Custom');await userEvent.type(screen.getByRole('textbox'),'Reason 1');expect(screen.queryByText('Reason 0')).toBeNull();await userEvent.click(screen.getByRole('button',{name:t.refresh}));await waitFor(()=>expect(fakeApi.auditLogs).toHaveBeenCalledTimes(2));
});
it('copies only the invitation displayed after a confirmed Admin account creation',async()=>{
  const clipboard=vi.fn().mockResolvedValue(undefined);Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:clipboard}});
  fakeApi.createUser.mockResolvedValue({activationToken:'admin-token',activationUrl:'https://office.example/company/activate#token=admin-token',user:account});renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Tạo tài khoản'}));
  await change(t.userDisplayName,'Office Admin');await change(t.userEmail,'admin2@example.test');await userEvent.selectOptions(screen.getByLabelText(t.userRole),'Admin');await change('Lý do tạo tài khoản','Authorized administrator');await userEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox'));fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);
  const field=await screen.findByDisplayValue(/admin-token/);await userEvent.click(field);await userEvent.click(screen.getByRole('button',{name:t.copyLink}));await waitFor(()=>expect(clipboard).toHaveBeenCalledWith('https://office.example/company/activate#token=admin-token'));
});
it.each([true,false])('copies an enrollment code or gives a manual fallback when clipboard success=%s',async success=>{
  Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:success?vi.fn().mockResolvedValue(undefined):vi.fn().mockRejectedValue(new Error('denied'))}});
  fakeApi.createEnrollmentToken.mockResolvedValue({connectionCode:'one-use-code',expiresAt:'2026-01-01'});renderProviders(<DevicesView/>);await screen.findByText(device.name);await userEvent.click(screen.getByRole('button',{name:'+ Tạo mã kết nối thiết bị'}));let modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByLabelText('Lý do'),{target:{value:'New desktop'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);await screen.findByDisplayValue('one-use-code');modal=screen.getByRole('dialog');await userEvent.click(within(modal).getByRole('button',{name:'Sao chép mã'}));await screen.findByText(success?'Đã sao chép mã kết nối.':'Hãy chọn và sao chép mã trong ô bên dưới.');
});
it('cancels account creation and status changes without sending mutations',async()=>{
  renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Tạo tài khoản'}));await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:t.cancel})[0]);expect(fakeApi.createUser).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button',{name:'Khóa'}));await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:t.cancel})[0]);expect(fakeApi.setUserStatus).not.toHaveBeenCalled();
});
it('reports device-list failure when opening assignment rather than dispatching an empty policy',async()=>{
  fakeApi.devices.mockRejectedValue(new Error('offline'));renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getByRole('button',{name:t.assignPolicyBtn}));await screen.findByText('Failed to load device list.');expect(fakeApi.assignPolicy).not.toHaveBeenCalled();
});
it('retains a failed account status form and offers a generic recoverable message',async()=>{
  fakeApi.setUserStatus.mockRejectedValue(new Error('offline'));renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'Khóa'}));const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByRole('textbox'),{target:{value:'Approved security check'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);await screen.findAllByText('Không thể đổi trạng thái tài khoản.');expect(screen.getByRole('dialog')).toBeTruthy();
});
describe('English administrator workflows',()=>{
  beforeEach(()=>localStorage.setItem('sentinellan_lang','en'));
  it('edits policy defaults and safely cancels assignment and creation dialogs',async()=>{
    fakeApi.policies.mockResolvedValue([{...policy,usbMode:'Legacy'},{...policy,id:'read',name:'Read only',usbMode:'ReadOnly'},{...policy,id:'full',name:'Full access',usbMode:'FullAccess'}]);
    renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getAllByRole('button',{name:'Edit'})[0]);const modal=screen.getByRole('dialog');
    fireEvent.change(within(modal).getByLabelText(translations.en.policyNameLabel),{target:{value:'Updated profile'}});fireEvent.change(within(modal).getByLabelText(translations.en.idleTimeoutLabel),{target:{value:'20'}});await userEvent.selectOptions(within(modal).getByLabelText(translations.en.usbModeLabel),'Blocked');fireEvent.submit(modal.querySelector('form')!);await screen.findByText('Policy updated successfully.');
    await userEvent.click(screen.getAllByRole('button',{name:translations.en.assignPolicyBtn})[0]);await screen.findByLabelText(translations.en.selectDevicePrompt);fireEvent.change(screen.getByLabelText(translations.en.selectDevicePrompt),{target:{value:'device-1'}});await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.cancel}));
    await userEvent.click(screen.getByRole('button',{name:translations.en.createPolicyBtn}));await userEvent.click(within(screen.getByRole('dialog')).getByLabelText('Close'));expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('shows an English policy error and an empty assignment directory without sending a mutation',async()=>{
    fakeApi.updatePolicy.mockRejectedValue(new Error('conflict'));fakeApi.devices.mockResolvedValue([]);renderProviders(<PoliciesView/>);await screen.findByText(policy.name);await userEvent.click(screen.getByRole('button',{name:'Edit'}));fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findByText('Failed to update policy.');await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:translations.en.cancel})[0]);await userEvent.click(screen.getByRole('button',{name:translations.en.assignPolicyBtn}));await screen.findByText('No devices found to assign.');
  });
  it('creates an English account without an invitation then cancels a second draft',async()=>{
    fakeApi.createUser.mockResolvedValue({user:account});renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Create account'}));await change(translations.en.userDisplayName,'English employee');await change(translations.en.userEmail,'en@example.test');await change('Operational Reason','New hire');await userEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox'));fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findByText('Account created.');await userEvent.click(screen.getByRole('button',{name:'+ Create account'}));await userEvent.click(within(screen.getByRole('dialog')).getAllByRole('button',{name:translations.en.cancel})[0]);expect(screen.queryByRole('dialog')).toBeNull();
  });
  it.each([409,403,500])('displays actionable English create-account failure %s',async status=>{
    fakeApi.createUser.mockRejectedValue(new ApiError(status));renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'+ Create account'}));await change(translations.en.userDisplayName,'English employee');await change(translations.en.userEmail,'en@example.test');await change('Operational Reason','New hire');await userEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox'));fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findAllByText(status===409?'An internal account already uses this email.':status===403?'You are not allowed to create accounts.':'Account could not be created. Check the form and your permissions.');
  });
  it.each([false,true])('unlocks or locks an account with English audit outcome locked=%s',async locked=>{
    fakeApi.users.mockResolvedValue([{...account,status:locked?'Locked':'Active',role:'Admin'}]);renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:locked?'Unlock':'Lock'}));const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByRole('textbox'),{target:{value:'Verified authority'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);await screen.findByText(locked?'Account unlocked; the user must sign in again.':'Account locked and sessions revoked.');
  });
  it.each([409,500])('keeps English status error %s visible',async status=>{
    fakeApi.setUserStatus.mockRejectedValue(new ApiError(status));renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:'Lock'}));const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByRole('textbox'),{target:{value:'Verified authority'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);await screen.findAllByText(status===409?'You cannot lock yourself or the last active admin.':'Could not change account status.');
  });
});
describe('English invitation and endpoint recovery',()=>{
  beforeEach(()=>localStorage.setItem('sentinellan_lang','en'));
  it.each(['reissueActivationToken','revokeActivationToken'])('reports %s rejection in English and permits cancelling',async method=>{
    fakeApi.users.mockResolvedValue([{...account,status:'PendingActivation'}]);fakeApi[method].mockRejectedValue(new Error('offline'));renderProviders(<UsersView/>);await screen.findByText(account.email);
    if(method==='reissueActivationToken'){await userEvent.click(screen.getByRole('button',{name:translations.en.reissueInvite}));await userEvent.type(screen.getByPlaceholderText('e.g. Employee requested new activation link...'),'Employee request');}else await userEvent.click(screen.getByRole('button',{name:new RegExp(translations.en.revokeInvite)}));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.confirm}));await screen.findByText(method==='reissueActivationToken'?'Failed to reissue activation link.':'Failed to revoke activation link.');await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.cancel}));expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('reissues an absolute invitation URL and cancels a subsequent revoke',async()=>{
    fakeApi.users.mockResolvedValue([{...account,status:'PendingActivation'}]);fakeApi.reissueActivationToken.mockResolvedValue({activationUrl:'https://office.example/activate#token=reissued',userEmail:account.email});renderProviders(<UsersView/>);await screen.findByText(account.email);await userEvent.click(screen.getByRole('button',{name:translations.en.reissueInvite}));await userEvent.type(screen.getByPlaceholderText('e.g. Employee requested new activation link...'),'Expired invitation');fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findByDisplayValue('https://office.example/activate#token=reissued');await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:'×'}));await userEvent.click(screen.getByRole('button',{name:new RegExp(translations.en.revokeInvite)}));await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.cancel}));expect(fakeApi.revokeActivationToken).not.toHaveBeenCalled();
  });
  it('shows English quick-command success and then closes its result automatically',async()=>{
    renderProviders(<DeviceTable initialDevices={[{...device,isOnline:false,lastSeenAt:null,assignedUserId:'employee'}]}/>);await userEvent.click(screen.getByRole('button',{name:'Action'}));const modal=screen.getByRole('dialog');await userEvent.selectOptions(within(modal).getByRole('combobox'),'SimulateLock');await userEvent.type(screen.getByPlaceholderText('e.g. Periodic security inspection...'),'Authorized simulation');await userEvent.click(screen.getByRole('button',{name:'Sign & Dispatch'}));await screen.findByText('Command dispatched successfully!');await waitFor(()=>expect(screen.queryByRole('dialog')).toBeNull(),{timeout:2500});
  });
  it('shows quick-command and incident rejections without losing the target in English',async()=>{
    fakeApi.createCommand.mockRejectedValue(new Error('denied'));fakeApi.createIncident.mockRejectedValue(new Error('offline'));renderProviders(<DeviceTable initialDevices={[device]}/>);await userEvent.click(screen.getByRole('button',{name:'Action'}));await userEvent.type(screen.getByPlaceholderText('e.g. Periodic security inspection...'),'Authorized action');await userEvent.click(screen.getByRole('button',{name:'Sign & Dispatch'}));await screen.findByText('Failed to dispatch command. Check permissions.');await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.cancel}));await userEvent.click(screen.getByTitle(translations.en.reportIncident));await userEvent.type(screen.getByPlaceholderText('e.g. Fan rattling, screen flickering...'),'Noisy fan');await userEvent.type(screen.getByPlaceholderText('Provide additional context, error codes...'),'Error code 123');await userEvent.selectOptions(within(screen.getByRole('dialog')).getByRole('combobox'),'Critical');fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);await screen.findByText('Failed to register incident.');await userEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:translations.en.cancel}));
  });
  it.each([403,503,500])('reports English enrollment %s with a recoverable modal',async status=>{
    fakeApi.createEnrollmentToken.mockRejectedValue(new ApiError(status));renderProviders(<DevicesView/>);await screen.findByText(device.name);await userEvent.click(screen.getByRole('button',{name:'+ Issue enrollment token'}));const modal=screen.getByRole('dialog');fireEvent.change(within(modal).getByLabelText('Reason'),{target:{value:'Approved setup'}});await userEvent.click(within(modal).getByRole('checkbox'));fireEvent.submit(modal.querySelector('form')!);await within(modal).findByRole('alert');await userEvent.click(within(modal).getByText(translations.en.cancel,{selector:'button'}));expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('mutation guards and inventory edge outcomes', () => {
  it('refuses a policy with a whitespace-only name even if the form is submitted programmatically', async () => {
    renderProviders(<PoliciesView />); await screen.findByText(policy.name);
    fireEvent.click(screen.getByRole('button', { name: t.createPolicyBtn })); await change(t.policyNameLabel, '   ');
    fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.createPolicy).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByLabelText('Close'));
    fireEvent.click(screen.getByRole('button', { name: 'Sửa' })); await change(t.policyNameLabel, '   ');
    fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.updatePolicy).not.toHaveBeenCalled();
  });
  it('will not create an unconfirmed account or change status without its audit confirmation', async () => {
    renderProviders(<UsersView />); await screen.findByText(account.email);
    fireEvent.click(screen.getByRole('button', { name: '+ Tạo tài khoản' }));
    fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.createUser).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getAllByRole('button', { name: t.cancel })[0]);
    fireEvent.click(screen.getByRole('button', { name: 'Khóa' }));
    fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.setUserStatus).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByLabelText(t.cancel)); expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('refuses an empty reissue reason and lets operators close either invitation dialog', async () => {
    fakeApi.users.mockResolvedValue([{ ...account, status: 'PendingActivation' }]); renderProviders(<UsersView />); await screen.findByText(account.email);
    fireEvent.click(screen.getByRole('button', { name: t.reissueInvite })); fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);
    expect(fakeApi.reissueActivationToken).not.toHaveBeenCalled(); fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '×' }));
    fireEvent.click(screen.getByRole('button', { name: new RegExp(t.revokeInvite) })); fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '×' }));
    expect(fakeApi.revokeActivationToken).not.toHaveBeenCalled();
  });
  it('blocks empty quick-action and incident payloads and permits closing each dialog', async () => {
    renderProviders(<DeviceTable initialDevices={[device]} />); fireEvent.click(screen.getByRole('button', { name: 'Thao tác' }));
    fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.createCommand).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '×' }));
    fireEvent.click(screen.getByTitle(t.reportIncident)); fireEvent.submit(screen.getByRole('dialog').querySelector('form')!); expect(fakeApi.createIncident).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '×' })); expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('identifies unavailable bulk outcomes by device identifier and reports non-Error failures safely', async () => {
    fakeApi.bulkCommands.mockResolvedValueOnce([{ deviceId: 'missing-device', outcome: 'Unavailable' }]).mockRejectedValueOnce('network unavailable');
    renderProviders(<DeviceTable initialDevices={[{ ...device, assignedUserId: 'employee' }]} />);
    const check = screen.getByLabelText(`Chọn ${device.name}`); fireEvent.click(check); fireEvent.click(check); fireEvent.click(check);
    fireEvent.change(screen.getByLabelText('Lệnh cho các máy đã chọn'), { target: { value: 'RefreshPolicy' } });
    fireEvent.change(screen.getByLabelText('Lý do gửi hàng loạt'), { target: { value: 'Approved policy update' } });
    fireEvent.submit(screen.getByLabelText('Lý do gửi hàng loạt').closest('form')!); expect(fakeApi.bulkCommands).not.toHaveBeenCalled();
    fireEvent.click(screen.getByLabelText('Tôi xác nhận gửi đến các máy đã chọn')); fireEvent.click(screen.getByRole('button', { name: 'Gửi lệnh' }));
    await screen.findByText('missing-device: Không khả dụng'); fireEvent.click(check); fireEvent.click(screen.getByRole('button', { name: 'Gửi lệnh' }));
    await screen.findByText('Không thể gửi lệnh'); expect(fakeApi.bulkCommands).toHaveBeenLastCalledWith([device.id], 'RefreshPolicy', 'Approved policy update');
  });
  it('creates an invitation from the fallback activation token when the server omits its URL', async () => {
    fakeApi.createUser.mockResolvedValue({ activationToken: 'token/with spaces', user: account }); renderProviders(<UsersView />); await screen.findByText(account.email);
    fireEvent.click(screen.getByRole('button', { name: '+ Tạo tài khoản' })); await change(t.userDisplayName, 'New staff'); await change(t.userEmail, 'new@example.test'); await change('Lý do tạo tài khoản', 'Approved hire');
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox')); fireEvent.submit(screen.getByRole('dialog').querySelector('form')!);
    await screen.findByDisplayValue(/token%2Fwith%20spaces/);
  });
});

describe('English command safety and persisted outcomes', () => {
  beforeEach(() => localStorage.setItem('sentinellan_lang', 'en'));
  it('shows an offline target, refuses an unconfirmed submission and dispatches a bounded non-parameter command', async () => {
    fakeApi.devices.mockResolvedValue([{ ...device, isOnline: false }, { ...device, id: 'device-2', name: 'Second PC' }]);
    renderProviders(<CommandsView />); await screen.findByRole('button', { name: translations.en.dispatchCommandBtn });
    fireEvent.click(screen.getByRole('button', { name: translations.en.dispatchCommandBtn })); await screen.findByLabelText(translations.en.commandTarget);
    const form = screen.getByRole('dialog').querySelector('form')!; fireEvent.submit(form); expect(fakeApi.createCommand).not.toHaveBeenCalled();
    change(translations.en.commandTarget, 'device-2'); change(translations.en.commandTypeLabel, 'CollectTelemetryNow'); change(translations.en.commandReasonLabel, 'Verified incident'); change('Validity Window (Seconds)', '90');
    fireEvent.click(document.getElementById('cmd-confirm')!); fireEvent.submit(form);
    await waitFor(() => expect(fakeApi.createCommand).toHaveBeenCalledWith('device-2', 'CollectTelemetryNow', 'Verified incident', true, 90, undefined));
  });
  it('keeps an English dispatch dialog recoverable after the device directory fails', async () => {
    fakeApi.devices.mockRejectedValue(new Error('directory unavailable')); renderProviders(<CommandsView />); await screen.findByRole('button', { name: translations.en.dispatchCommandBtn });
    fireEvent.click(screen.getByRole('button', { name: translations.en.dispatchCommandBtn })); await screen.findByText('No registered devices available.');
    fireEvent.click(within(screen.getByRole('dialog')).getByLabelText('Close')); expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('distinguishes real success, simulation and unconfirmed delivery in English history', async () => {
    const base = { deviceName: device.name, reason: 'Audited action', issuedAt: '2026-01-01', parameter: 'Office notification', type: 'ShowNotification' };
    fakeApi.commands.mockResolvedValue([{ ...base, id: 'real-success', status: 'Succeeded', succeeded: true }, { ...base, id: 'simulation', type: 'SimulateLock', status: 'Succeeded', succeeded: true }, { ...base, id: 'unknown-execution', status: 'ExecutionUnconfirmed' }, { ...base, id: 'custom-status', status: 'Cancelled' }]);
    renderProviders(<CommandsView />); await screen.findByText('Execution unconfirmed'); expect(screen.getByText('Simulated')).toBeTruthy(); expect(screen.getByText('Delivered; verify before retrying.')).toBeTruthy(); expect(screen.getByText('Cancelled')).toBeTruthy();
  });
  it.each(['ShowNotification', 'RestartService', 'IsolateNetwork'])('renders English %s parameters and cancels without executing', async kind => {
    renderProviders(<CommandsView />); await screen.findByRole('button', { name: translations.en.dispatchCommandBtn }); fireEvent.click(screen.getByRole('button', { name: translations.en.dispatchCommandBtn })); await screen.findByLabelText(translations.en.commandTypeLabel);
    change(translations.en.commandTypeLabel, kind); expect(screen.getByRole('dialog').querySelector('form')).toBeTruthy();
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: translations.en.cancel })); expect(fakeApi.createCommand).not.toHaveBeenCalled();
  });
});
