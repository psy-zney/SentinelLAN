// @vitest-environment jsdom
import React from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { auth, device, fakeApi, renderProviders, resetApi } from './helpers';
import { DeviceDetail } from '../src/components/device-detail';
import { SoftwareCompliancePanel, SoftwareRulesPanel } from '../src/components/software-compliance-panel';
import { MonitoringSettingsPanel } from '../src/components/monitoring-settings-panel';
import { ApiError } from '../src/lib/api-client';
import { translations as dictionaries } from '../src/lib/i18n';
const translations = dictionaries.vi;

vi.mock('@/lib/api-client', async () => { const { fakeApi } = await import('./helpers'); return { ApiClient: class { constructor() { return fakeApi; } }, ApiError: class extends Error { constructor(public status: number, message = 'api') { super(message); } } }; });
vi.mock('@/components/app-shell', async () => { const { auth } = await import('./helpers'); return { useCurrentSession: () => auth.session }; });
vi.mock('@/hooks/use-device-updates', () => ({ useDeviceUpdates: () => {} }));
vi.mock('@/components/endpoint-observation-panel', () => ({ EndpointObservationPanel: () => null }));

const asset = { ...device, serialNumber: 'SERIAL', model: 'Laptop', healthScore: { score: 92, grade: 'Good', recommendations: [] }, repairVsReplace: { recommendation: 'Keep & Maintain', analysisSummary: 'Keep this device', replacementCost: 1000, cumulativeMaintenanceCost: 50, maintenanceToCostRatioPercent: 5, estimatedNext3YearsCost: 200 } };
const settings = { cpuThresholdPercent: 90, ramThresholdPercent: 90, diskThresholdPercent: 90, offlineMinutes: 5, sustainedMinutes: 2, maintenancePauseMinutes: 15, preventiveIntervalMonths: 6, warrantyNoticeDays: 30, securityAlertsEnabled: true, revision: 'revision' };
const change = (element: HTMLElement, value: string) => fireEvent.change(element, { target: { value } });
beforeEach(() => {
  resetApi(); fakeApi.getDeviceAssetDetail.mockResolvedValue(asset); fakeApi.getQrLabel.mockResolvedValue(null);
  fakeApi.allUsers.mockResolvedValue([]); fakeApi.monitoringSettings.mockResolvedValue(settings);
});
async function detail() { renderProviders(<DeviceDetail id={device.id} />); await screen.findAllByText('SERIAL'); }

it('hides administrative controls from employees and revoked devices', async () => {
  auth.session = { ...auth.session, role: 'Employee' }; await detail();
  expect(screen.queryByText('Thao tác an toàn trên thiết bị')).toBeNull();
  expect(screen.queryByLabelText('Employee')).toBeNull(); expect(fakeApi.allUsers).not.toHaveBeenCalled();
});
it('keeps revoked credentials read-only', async () => {
  fakeApi.getDeviceAssetDetail.mockResolvedValue({ ...asset, isRevoked: true }); await detail();
  expect((screen.getByLabelText('Employee') as HTMLSelectElement).disabled).toBe(true);
  expect(screen.queryByRole('button', { name: 'Xác nhận thu hồi' })).toBeNull();
});
it.each([403, 500])('preserves command confirmation on HTTP %s and permits a deliberate retry', async status => {
  fakeApi.createCommand.mockRejectedValueOnce(new ApiError(status)); await detail();
  change(screen.getByPlaceholderText('Nhập lý do bắt buộc...'), ' Check health ');
  const button = screen.getByRole('button', { name: 'Gửi thao tác' });
  expect((button as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText(`Tôi xác nhận ban hành lệnh trên ${device.name}`)); fireEvent.click(button);
  await screen.findByText(status === 403 ? 'Bạn không có quyền thực hiện thao tác này.' : 'Thao tác thất bại. Kiểm tra kết nối.');
  fireEvent.click(screen.getByRole('button', { name: 'Gửi thao tác' })); await screen.findByText('Đã gửi lệnh điều khiển an toàn.');
  expect(fakeApi.createCommand).toHaveBeenLastCalledWith({ deviceId: device.id, type: 'CollectTelemetryNow', reason: 'Check health', confirmed: true });
  expect((screen.getByRole('button', { name: 'Gửi thao tác' }) as HTMLButtonElement).disabled).toBe(true);
});
it('requires a reason and acknowledgement before permanent device revocation', async () => {
  await detail(); const button = screen.getByRole('button', { name: 'Xác nhận thu hồi' });
  fireEvent.click(button); expect(fakeApi.revokeDevice).not.toHaveBeenCalled();
  change(screen.getByPlaceholderText('Lý do thu hồi...'), ' Device replaced ');
  fireEvent.click(screen.getByLabelText('Tôi hiểu rằng credential sẽ bị thu hồi vĩnh viễn.')); fireEvent.click(button);
  await screen.findByText('Đã thu hồi thiết bị.'); expect(fakeApi.revokeDevice).toHaveBeenCalledWith(device.id, 'Device replaced', true);
});
it('reuses an incident idempotency key after a network error', async () => {
  fakeApi.createIncident.mockRejectedValueOnce(new Error('offline')); await detail();
  fireEvent.click(screen.getByRole('button', { name: translations.reportIncident }));
  change(screen.getByPlaceholderText('VD: Pin phồng, quạt kêu to...'), ' Fan issue ');
  const dialog = screen.getByRole('dialog'); change(within(dialog).getByRole('combobox'), 'High');
  change(within(dialog).getAllByRole('textbox')[1], ' Fan stopped ');
  fireEvent.click(within(dialog).getByRole('button', { name: translations.save })); await screen.findByText('Không thể tạo báo hỏng.');
  const key = fakeApi.createIncident.mock.calls[0][0].idempotencyKey;
  fireEvent.click(within(dialog).getByRole('button', { name: translations.save })); await screen.findByText('Đã ghi nhận báo hỏng sự cố.');
  expect(fakeApi.createIncident).toHaveBeenLastCalledWith({ deviceId: device.id, title: 'Fan issue', description: 'Fan stopped', severity: 'High', idempotencyKey: key });
});
it('shows failed profile updates without discarding edits', async () => {
  fakeApi.updateAssetProfile.mockRejectedValue(new Error('offline')); await detail();
  fireEvent.click(screen.getByRole('button', { name: new RegExp(translations.editAssetProfile) }));
  const dialog = screen.getByRole('dialog'); const inputs = within(dialog).getAllByRole('textbox');
  inputs.forEach((input, index) => change(input, index === inputs.length - 1 ? 'Annual audit' : `value-${index}`));
  change(within(dialog).getByRole('combobox'), 'Desktop'); change(within(dialog).getByRole('spinbutton'), '1200');
  fireEvent.click(within(dialog).getByRole('checkbox')); fireEvent.click(within(dialog).getByRole('button', { name: translations.save }));
  await screen.findByText('Không thể cập nhật hồ sơ. Kiểm tra quyền hạn.');
  expect(screen.getByRole('dialog')).toBeTruthy(); expect(fakeApi.updateAssetProfile).toHaveBeenCalledWith(device.id, expect.objectContaining({ assetType: 'Desktop', purchaseCost: 1200, locationCampus: 'value-3', confirmed: true }));
});
it('recovers software status and renders update and unknown detection results', async () => {
  fakeApi.softwareStatus.mockRejectedValueOnce(new Error('offline')).mockResolvedValue([{ source: 'Catalog', ruleId: 'a', name: 'Office', status: 'UpdateAvailable', installedVersion: '1', requiredVersion: '2', capturedAt: '2026-10-23T00:00:00Z' }, { source: 'Rule', ruleId: 'b', name: 'Portable', status: 'FutureStatus' }]);
  renderProviders(<SoftwareCompliancePanel deviceId={device.id} />); await screen.findByRole('alert');
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' })); await screen.findByText('Office');
  expect(screen.getByText(/Có bản cập nhật/).textContent).toContain('1 → 2'); expect(screen.getByText(/Chưa xác định/)).toBeTruthy();
  expect(fakeApi.softwareStatus).toHaveBeenLastCalledWith(device.id);
});
it('saves a new software rule only after confirmation and reports server validation failures', async () => {
  fakeApi.saveSoftwareRule.mockRejectedValueOnce(new Error('Invalid MSI identifier')); renderProviders(<SoftwareRulesPanel />);
  fireEvent.click(screen.getByRole('button', { name: 'Thêm quy tắc' }));
  change(screen.getByLabelText('Tên phần mềm'), 'Unapproved MSI'); change(screen.getByLabelText('MSI ProductCode (UUID)'), ' product '); change(screen.getByLabelText('MSI UpgradeCode (UUID)'), ' upgrade '); change(screen.getByLabelText('Lý do'), 'Approved review');
  expect((screen.getByRole('button', { name: 'Lưu' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('Tôi xác nhận quy tắc áp dụng trong cơ quan')); fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
  await screen.findByRole('alert'); expect(screen.getByText('Invalid MSI identifier')).toBeTruthy();
  fireEvent.click(screen.getByLabelText('Đang kiểm tra')); fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
  await waitFor(() => expect(screen.queryByLabelText('Tên phần mềm')).toBeNull());
  expect(fakeApi.saveSoftwareRule).toHaveBeenLastCalledWith(expect.objectContaining({ productCode: 'product', upgradeCode: 'upgrade', isActive: false, confirmed: true }), undefined);
});
it('edits an existing disabled software rule with its revision and can cancel', async () => {
  fakeApi.softwareRules.mockResolvedValue([{ id: 'rule', name: 'Existing', productCode: null, upgradeCode: null, isActive: false, revision: 'revision-2' }]); renderProviders(<SoftwareRulesPanel />);
  fireEvent.click(await screen.findByRole('button', { name: 'Sửa' })); fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
  expect(screen.queryByLabelText('Tên phần mềm')).toBeNull(); fireEvent.click(screen.getByRole('button', { name: 'Sửa' }));
  change(screen.getByLabelText('Lý do'), 'Updated policy'); fireEvent.click(screen.getByLabelText('Tôi xác nhận quy tắc áp dụng trong cơ quan')); fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
  await waitFor(() => expect(fakeApi.saveSoftwareRule).toHaveBeenCalledWith(expect.objectContaining({ revision: 'revision-2', productCode: null, upgradeCode: null }), 'rule'));
});
it.each([new ApiError(409, 'Revision changed'), new Error('offline')])('retains monitoring settings after save errors and retries with explicit consent', async failure => {
  fakeApi.saveMonitoringSettings.mockRejectedValueOnce(failure).mockResolvedValue({ ...settings, cpuThresholdPercent: 80, securityAlertsEnabled: false });
  renderProviders(<MonitoringSettingsPanel />); fireEvent.click(screen.getByText('Cấu hình cảnh báo và bảo trì của đơn vị'));
  const cpu = await screen.findByLabelText('Ngưỡng CPU (%)'); change(cpu, '80');
  change(screen.getByLabelText('Lý do thay đổi'), ' Alert drill '); fireEvent.click(screen.getByLabelText('Tự cảnh báo khi mẫu bảo mật xác nhận có vấn đề'));
  expect((screen.getByRole('button', { name: 'Lưu và kiểm tra lại' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByLabelText('Tôi xác nhận áp dụng cấu hình này cho đơn vị')); fireEvent.click(screen.getByRole('button', { name: 'Lưu và kiểm tra lại' }));
  await screen.findByText(failure instanceof ApiError ? 'Revision changed' : 'Chưa lưu được cấu hình. Vui lòng thử lại.');
  fireEvent.click(screen.getByRole('button', { name: 'Lưu và kiểm tra lại' })); await screen.findByText(/Đã lưu. Hệ thống/);
  expect(fakeApi.saveMonitoringSettings).toHaveBeenLastCalledWith(expect.objectContaining({ cpuThresholdPercent: 80, securityAlertsEnabled: false }), 'Alert drill', true);
  expect((screen.getByRole('button', { name: 'Lưu và kiểm tra lại' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Tải lại cấu hình' })); await waitFor(() => expect((screen.getByLabelText('Ngưỡng CPU (%)') as HTMLInputElement).value).toBe('90'));
});
it('retries failed monitoring reads', async () => {
  fakeApi.monitoringSettings.mockRejectedValueOnce(new Error('offline')); renderProviders(<MonitoringSettingsPanel />);
  fireEvent.click(screen.getByText('Cấu hình cảnh báo và bảo trì của đơn vị')); await screen.findByRole('alert');
  fireEvent.click(screen.getByRole('button', { name: 'Thử lại' })); await screen.findByLabelText('Ngưỡng CPU (%)');
});
it.each(['vi', 'en'])('reports work-order errors and accepts corrected maintenance data in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang); const t = dictionaries[lang as 'vi' | 'en'];
  fakeApi.createWorkOrder.mockRejectedValueOnce(new Error('offline')); await detail();
  fireEvent.click(screen.getByRole('button', { name: t.createWorkOrder }));
  const dialog = screen.getByRole('dialog'); change(within(dialog).getAllByRole('textbox')[0], ' Clean fan ');
  change(within(dialog).getAllByRole('textbox')[1], ' Inspection needed ');
  expect(within(dialog).queryByRole('option', { name: /Inspection/ })).toBeNull();
  expect(within(dialog).queryByRole('option', { name: 'Urgent' })).toBeNull();
  change(within(dialog).getAllByRole('combobox')[0], 'Preventive'); change(within(dialog).getAllByRole('combobox')[1], 'Critical');
  fireEvent.submit(dialog.querySelector('form')!); await screen.findByText(lang === 'vi' ? 'Không thể tạo phiếu bảo trì.' : 'Failed to create work order.');
  fireEvent.submit(dialog.querySelector('form')!); await screen.findByText(lang === 'vi' ? 'Đã tạo phiếu bảo trì (Work Order).' : 'Work order created.');
  expect(fakeApi.createWorkOrder).toHaveBeenLastCalledWith({ deviceId: device.id, title: 'Clean fan', type: 'Preventive', priority: 'Critical', notes: 'Inspection needed' });
});
it.each(['vi', 'en'])('completes maintenance with itemized costs and resolves an incident in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang); const t = dictionaries[lang as 'vi' | 'en'];
  fakeApi.getWorkOrders.mockResolvedValue([{ id: 'wo', workOrderNumber: 'WO-1', title: 'Repair fan', type: 'Corrective', priority: 'High', status: 'InProgress', totalCost: 0 }]);
  fakeApi.getIncidents.mockResolvedValue([{ id: 'incident', title: 'Broken fan', description: 'No airflow', severity: 'Critical', status: 'Open', createdAt: '2026-10-23T00:00:00Z' }]);
  fakeApi.completeWorkOrder.mockRejectedValueOnce(new Error('offline')); await detail();
  fireEvent.click(screen.getByRole('button', { name: name => name.startsWith(t.tabCmms) }));
  fireEvent.click(screen.getByRole('button', { name: new RegExp(t.completeWorkOrder) }));
  const dialog = screen.getByRole('dialog'); within(dialog).getAllByRole('spinbutton').forEach((input, index) => change(input, String(index + 2)));
  change(within(dialog).getByRole('textbox'), ' Fan replaced '); fireEvent.submit(dialog.querySelector('form')!);
  await screen.findByText(lang === 'vi' ? 'Không thể hoàn thành phiếu bảo trì.' : 'Failed to complete work order.');
  fireEvent.submit(dialog.querySelector('form')!); await screen.findByText(lang === 'vi' ? 'Đã hoàn thành phiếu bảo trì.' : 'Work order marked completed.');
  expect(fakeApi.completeWorkOrder).toHaveBeenLastCalledWith('wo', { laborHours: 2, partsCost: 3, laborCost: 4, notes: 'Fan replaced' });
  fireEvent.click(screen.getByRole('button', { name: lang === 'vi' ? /Đã sửa xong/ : /Resolve/ }));
  await waitFor(() => expect(fakeApi.updateIncidentStatus).toHaveBeenCalledWith('incident', { status: 'Resolved', resolutionNotes: 'Đã khắc phục hoàn tất.' }));
});
it.each(['vi', 'en'])('issues and prints a fresh QR label, then handles revocation errors in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang); const t = dictionaries[lang as 'vi' | 'en'];
  fakeApi.generateQrLabel.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ code: 'opaque-qr-code' });
  fakeApi.revokeQrLabel.mockRejectedValueOnce(new Error('offline')); window.print = vi.fn(); await detail();
  fireEvent.click(screen.getByRole('button', { name: t.generateQr }));
  let dialog = screen.getByRole('dialog'); change(within(dialog).getByRole('textbox'), ' Replace damaged label ');
  fireEvent.submit(dialog.querySelector('form')!); await screen.findByText(lang === 'vi' ? 'Không thể xoay vòng tem QR.' : 'Failed to rotate QR label.');
  fireEvent.submit(dialog.querySelector('form')!); await screen.findByText(lang === 'vi' ? 'Đã phát hành / xoay vòng tem QR mới thành công!' : 'New QR label issued successfully!');
  expect(fakeApi.generateQrLabel).toHaveBeenLastCalledWith(device.id, 'Replace damaged label');
  fireEvent.click(screen.getByRole('button', { name: t.printQr })); expect(window.print).toHaveBeenCalledOnce();
  fireEvent.click(screen.getByRole('button', { name: new RegExp(t.revokeQr) })); dialog = screen.getByRole('dialog');
  expect((within(dialog).getByRole('button', { name: t.confirm }) as HTMLButtonElement).disabled).toBe(true);
  change(within(dialog).getByRole('textbox'), ' Lost sticker '); fireEvent.click(within(dialog).getByRole('button', { name: t.confirm }));
  await screen.findByText(lang === 'vi' ? 'Không thể thu hồi tem QR.' : 'Failed to revoke QR label.');
  fireEvent.click(within(dialog).getByRole('button', { name: t.confirm })); await screen.findByText(lang === 'vi' ? 'Đã thu hồi tem QR thành công.' : 'QR label revoked successfully.');
  expect(fakeApi.revokeQrLabel).toHaveBeenLastCalledWith(device.id, 'Lost sticker');
});
it.each(['vi', 'en'])('renders lifecycle provenance, degraded health and replacement analysis in %s', async lang => {
  localStorage.setItem('sentinellan_lang', lang); const t = dictionaries[lang as 'vi' | 'en'];
  fakeApi.getDeviceAssetDetail.mockResolvedValue({ ...asset, isOnline: false, model: null, serialNumber: null, healthScore: { score: 40, grade: 'Poor', recommendations: ['Replace fan'] }, specificationsJson: 'legacy hardware record', purchaseDate: '2024-01-01', purchaseCost: 500, warrantyExpiresAt: '2027-01-01', vendorName: 'Vendor', assignedUserName: 'Staff', locationCampus: 'Campus', repairVsReplace: { ...asset.repairVsReplace, recommendation: 'Replace Immediately', maintenanceToCostRatioPercent: 70 } });
  fakeApi.getDeviceTimeline.mockResolvedValue(['Enrolled', 'Purchased', 'IncidentReported', 'WorkOrderCompleted', 'Command', 'LoanReturned', 'Other'].map((eventType, index) => ({ id: String(index), eventType, title: `Event ${index}`, description: `Lifecycle ${index}`, timestamp: '2026-10-23T00:00:00Z', severity: ['critical', 'warning', 'success', 'info'][index % 4], actor: index % 2 ? null : 'IT Admin' })));
  renderProviders(<DeviceDetail id={device.id} />); await screen.findByText('Replace fan');
  expect(screen.getByText('Offline')).toBeTruthy(); expect(screen.getByText('legacy hardware record')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: name => name.includes(t.tabTimeline) })); await screen.findByText('Event 6');
  expect(screen.getAllByText(/IT Admin/)).toHaveLength(4);
  fireEvent.click(screen.getByRole('button', { name: t.tabTco })); expect(screen.getByText('Keep this device')).toBeTruthy();
});
it('saves monitoring changes in English and evaluates zero sustained minutes', async () => {
  localStorage.setItem('sentinellan_lang', 'en'); fakeApi.saveMonitoringSettings.mockResolvedValue(settings);
  renderProviders(<MonitoringSettingsPanel />); fireEvent.click(screen.getByText('Organization monitoring and maintenance settings'));
  await screen.findByLabelText('CPU threshold (%)');
  const labels = ['CPU threshold (%)', 'RAM threshold (%)', 'Disk threshold (%)', 'Offline after (minutes)', 'Sustained CPU/RAM (minutes)', 'Approved maintenance pause (minutes)', 'Preventive interval (months)', 'Warranty notice (days)'];
  labels.forEach((label, index) => change(screen.getByLabelText(label), String([80, 81, 82, 3, 0, 10, 12, 60][index])));
  change(screen.getByLabelText('Reason for change'), 'Scheduled drill'); fireEvent.click(screen.getByLabelText('I confirm these settings for this organization'));
  fireEvent.click(screen.getByRole('button', { name: 'Save and recheck' })); await screen.findByText(/Saved. Existing observations/);
  expect(fakeApi.saveMonitoringSettings).toHaveBeenCalledWith(expect.objectContaining({ sustainedMinutes: 0, offlineMinutes: 3, warrantyNoticeDays: 60 }), 'Scheduled drill', true);
});


