import type { VpsHostStatus } from '../src/lib/validation/vps';

export function vpsStatus(): VpsHostStatus {
  return {
    configured: true, available: true, stale: false, message: 'Đã đọc trạng thái VPS.',
    snapshot: {
      name: 'VPS công ty', host: 'vps.example.test', capturedAtUtc: new Date().toISOString(),
      osInfo: 'Ubuntu 24.04', uptimeSeconds: 90061,
      cpuPercent: 12.5, ramPercent: 45, diskPercent: 70,
      memoryTotalBytes: 8 * 1024 ** 3, memoryUsedBytes: 3.6 * 1024 ** 3,
      diskTotalBytes: 100 * 1024 ** 3, diskUsedBytes: 70 * 1024 ** 3,
      runtime: {
        systemState: 'running', dockerAvailable: true, dockerError: null,
        services: [{ name: 'nginx.service', activeState: 'active', startupState: 'enabled' }],
        containers: [{
          id: 'container-1', name: 'sentinellan-api', image: 'sentinellan:current',
          state: 'running', status: 'Up 2 hours', health: 'healthy', restartPolicy: 'unless-stopped',
          cpuPercent: 125, memoryUsage: '128 MiB / 8 GiB', memoryPercent: 1.6,
          storageUsage: null, networkIo: null, blockIo: null, project: 'sentinellan',
          ports: [{ containerPort: 8080, hostPort: 8080, hostIp: '127.0.0.1', protocol: 'tcp' }],
        }],
      },
      listeningPorts: [{ address: '127.0.0.1', port: 8080, protocol: 'tcp', process: 'docker-proxy' }],
      warnings: [],
    },
  };
}
