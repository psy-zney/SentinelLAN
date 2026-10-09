import { describe, expect, it } from "vitest";
import { gatewayEvidence } from "../src/lib/vps-gateway";
import type { VpsContainer, VpsHostSnapshot } from "../src/types/host-status";

const container = (project: string, hostPort: number, extra: Partial<VpsContainer> = {}): VpsContainer => ({
  id: project, project, name: `${project}-server-1`, image: "server:test", state: "running", status: "Up", health: null,
  cpuPercent: null, memoryUsage: null, memoryPercent: null, listeningPorts: null,
  ports: [{ hostIp: "127.0.0.1", hostPort, containerPort: 3001, protocol: "tcp" }], ...extra,
});
const snapshot = (containers: VpsContainer[]): VpsHostSnapshot => ({
  name: "test", host: "192.0.2.1", capturedAtUtc: new Date().toISOString(), osInfo: "Linux", uptimeSeconds: 1,
  cpuPercent: 0, ramPercent: 0, diskPercent: 0, memoryTotalBytes: 1, memoryUsedBytes: 0, diskTotalBytes: 1, diskUsedBytes: 0, warnings: [],
  runtime: { systemState: "running", dockerAvailable: true, dockerError: null, services: [], containers },
  listeningPorts: [9004, 9005].map(port => ({ address: "127.0.0.1", port, protocol: "tcp", process: "docker-proxy" })),
});

describe("Owner project gateway policy", () => {
  it("excludes other owners and recognizes isolated loopback HTTP/TLS gateway listeners", () => {
    const data = snapshot([container("monopoly", 9004), container("exxplore-kittens", 9005), container("mot-me-banh", 3080)]);
    data.listeningPorts.push(...["127.0.0.1", "127.0.0.2"].map(address => ({ address, port: 9000, protocol: "tcp", process: "nginx" })));
    const evidence = gatewayEvidence(data);
    expect(evidence.http).toBe(true);
    expect(evidence.tls).toBe(true);
    expect(evidence.projects.filter(project => project.matches).map(project => project.id)).toEqual(["monopoly", "kitchen-explore"]);
    expect(evidence.projects.flatMap(project => project.containers).some(value => value.project === "mot-me-banh")).toBe(false);
  });

  it("rejects old ports, public bindings, stopped containers, EXPOSE metadata and missing listening sockets", () => {
    for (const value of [container("monopoly", 3110), container("monopoly", 9004, { state: "exited" }),
      container("monopoly", 9004, { ports: [{ hostIp: "0.0.0.0", hostPort: 9004, containerPort: 3001, protocol: "tcp" }] }),
      container("monopoly", 9004, { ports: [{ hostIp: null, hostPort: null, containerPort: 9004, protocol: "tcp" }] })]) {
      expect(gatewayEvidence(snapshot([value])).projects.find(project => project.id === "monopoly")?.matches).toBe(false);
    }
    expect(gatewayEvidence(snapshot([container("monopoly", 9004), container("monopoly", 3110)])).projects.find(project => project.id === "monopoly")?.matches).toBe(false);
    const data = snapshot([container("monopoly", 9004)]);
    data.listeningPorts = [];
    expect(gatewayEvidence(data).projects.find(project => project.id === "monopoly")?.matches).toBe(false);
  });

  it("requires the BeatSync process and active service, and does not count UDP as a web gateway", () => {
    const data = snapshot([]);
    data.runtime.services.push({ name: "beatsync", activeState: "active", startupState: "enabled" });
    data.listeningPorts.push({ address: "127.0.0.1", port: 9001, protocol: "tcp", process: "beatsync-server" }, { address: "127.0.0.1", port: 9000, protocol: "udp", process: "nginx" });
    expect(gatewayEvidence(data).projects[0].matches).toBe(true);
    expect(gatewayEvidence(data).http).toBe(false);
    data.runtime.services[0].activeState = "inactive";
    expect(gatewayEvidence(data).projects[0].matches).toBe(false);
  });
});
