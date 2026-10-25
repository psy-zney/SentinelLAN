import { z } from 'zod';

const percent = z.number().finite().min(0).max(100);
const quantity = z.number().finite().nonnegative();
const port = z.number().int().min(1).max(65535);

const listeningPort = z.object({
  address: z.string(),
  port,
  protocol: z.string(),
  process: z.string().nullable().optional(),
});

export const VpsHostStatusSchema = z.object({
  configured: z.boolean(),
  available: z.boolean(),
  stale: z.boolean(),
  message: z.string(),
  snapshot: z.object({
    name: z.string(),
    host: z.string(),
    capturedAtUtc: z.string().datetime({ offset: true }),
    osInfo: z.string(),
    uptimeSeconds: quantity,
    cpuPercent: percent,
    ramPercent: percent,
    diskPercent: percent,
    memoryTotalBytes: quantity,
    memoryUsedBytes: quantity,
    diskTotalBytes: quantity,
    diskUsedBytes: quantity,
    runtime: z.object({
      systemState: z.string(),
      dockerAvailable: z.boolean(),
      dockerError: z.string().nullable(),
      services: z.array(z.object({
        name: z.string(),
        activeState: z.string(),
        startupState: z.string(),
      })),
      containers: z.array(z.object({
        id: z.string(),
        name: z.string(),
        image: z.string(),
        state: z.string(),
        status: z.string(),
        health: z.string().nullable(),
        restartPolicy: z.string().nullable(),
        cpuPercent: quantity.nullable(),
        memoryUsage: z.string().nullable(),
        memoryPercent: percent.nullable(),
        storageUsage: z.string().nullable(),
        networkIo: z.string().nullable(),
        blockIo: z.string().nullable(),
        ports: z.array(z.object({
          containerPort: port,
          protocol: z.string(),
          hostIp: z.string().nullable(),
          hostPort: port.nullable(),
        })).nullable().optional(),
        listeningPorts: z.array(listeningPort).nullable().optional(),
        networkMode: z.string().nullable().optional(),
        project: z.string().nullable().optional(),
      })),
    }),
    listeningPorts: z.array(listeningPort),
    warnings: z.array(z.string()),
  }).nullable(),
});

export type VpsHostStatus = z.infer<typeof VpsHostStatusSchema>;
