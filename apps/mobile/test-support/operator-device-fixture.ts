import type { z } from 'zod';
import type { OperatorDeviceDetailSchema } from '../src/lib/validation/operator';
import type { TelemetrySnapshot } from '../src/lib/validation/schemas';

export const sampleDeviceId = '00000000-0000-4000-8000-000000000001';

export function operatorDeviceFixture(): z.infer<typeof OperatorDeviceDetailSchema> {
  return {
    id: sampleDeviceId, name: '[MẪU] KT-PC-01', osVersion: 'Windows 11 Pro 24H2',
    agentVersion: 'sample-only', lastSeenAt: null, isOnline: false, isRevoked: false,
    assignedUserName: '[MẪU] Nguyễn Minh Anh', serialNumber: 'SAMPLE-KT-PC-01',
    manufacturer: 'Dell', model: 'OptiPlex 7010', assetType: 'Workstation',
    locationCampus: 'Dữ liệu giả lập', locationBuilding: 'Văn phòng mẫu',
    locationFloor: 'Tầng 2', locationRoom: 'Kế toán', assetStatus: 'Active',
    specificationsJson: JSON.stringify({ cpu: 'Intel Core i5-13400', ram: '16 GB DDR4', storage: '512 GB NVMe SSD', sampleData: true, sampleInstalledApplications: ['Firefox ESR', '7-Zip'] }),
    purchaseDate: '2025-10-01T00:00:00Z', purchaseCost: 14500000,
    warrantyExpiresAt: '2027-10-01T00:00:00Z', vendorName: '[MẪU] Nhà cung cấp thiết bị',
  };
}

export function deviceTelemetryFixture(): TelemetrySnapshot {
  return { id: 'sample-measurement', deviceId: sampleDeviceId, cpuPercent: 24, ramPercent: 48, diskPercent: 60, createdAt: new Date().toISOString(), collectedAt: new Date().toISOString() };
}
