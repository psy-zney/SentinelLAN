export type SoftwareStatus = { ruleId: string; name: string; source: string; status: string; installedVersion: string | null; requiredVersion: string | null; capturedAt: string | null };
export type SoftwareRule = { id: string; name: string; productCode: string | null; upgradeCode: string | null; isActive: boolean; revision: string };
export type SaveSoftwareRule = Omit<SoftwareRule, "id"> & { reason: string; confirmed: boolean };
