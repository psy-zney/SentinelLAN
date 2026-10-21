export type ProtectionStatus = "Healthy" | "Unhealthy" | "Unknown" | "NotSupported";
export type EndpointObservation = {
  inventory: {
    capturedAt: string;
    serialNumber: string | null;
    manufacturer: string | null;
    model: string | null;
    cpuName: string | null;
    ramBytes: number | null;
    systemDiskBytes: number | null;
    networkAdapters: { macAddress: string; addresses: string[] }[];
  } | null;
  security: {
    capturedAt: string;
    antivirusStatus: ProtectionStatus;
    antivirusProvider: string;
    firewallDomainStatus: ProtectionStatus;
    firewallPrivateStatus: ProtectionStatus;
    firewallPublicStatus: ProtectionStatus;
    diskEncryptionStatus: ProtectionStatus;
  } | null;
  securityStale: boolean;
};

export type MonitoringSettings = {
  cpuThresholdPercent: number;
  ramThresholdPercent: number;
  diskThresholdPercent: number;
  offlineMinutes: number;
  sustainedMinutes: number;
  maintenancePauseMinutes: number;
  preventiveIntervalMonths: number;
  warrantyNoticeDays: number;
  securityAlertsEnabled: boolean;
  revision: string;
};
