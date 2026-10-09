export type VpsPortBinding = { containerPort: number; protocol: string; hostIp: string | null; hostPort: number | null };
export type VpsListeningPort = { address: string; port: number; protocol: string; process: string | null };
export type VpsContainer = {
  id: string; name: string; image: string; state: string; status: string; health: string | null;
  cpuPercent: number | null; memoryUsage: string | null; memoryPercent: number | null;
  ports: VpsPortBinding[] | null; listeningPorts: VpsListeningPort[] | null;
  project?: string | null;
};
export type VpsStorageItem = {
  name: string;
  path: string;
  sizeBytes: number;
  category?: string | null;
  reclaimable?: string | null;
};
export type VpsHostSnapshot = {
  name: string; host: string; capturedAtUtc: string; osInfo: string; uptimeSeconds: number;
  cpuPercent: number; ramPercent: number; diskPercent: number;
  memoryTotalBytes: number; memoryUsedBytes: number; diskTotalBytes: number; diskUsedBytes: number;
  runtime: {
    systemState: string; dockerAvailable: boolean; dockerError: string | null;
    services: { name: string; activeState: string; startupState: string }[];
    containers: VpsContainer[];
    storageBreakdown?: VpsStorageItem[];
  };
  listeningPorts: VpsListeningPort[]; warnings: string[];
};
export type VpsHostStatus = { configured: boolean; available: boolean; stale: boolean; message: string; snapshot: VpsHostSnapshot | null };
