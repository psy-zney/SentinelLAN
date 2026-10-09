import { describe, expect, it } from "vitest";
import { containersForPort, endpoint, portsForService, portScope } from "../src/lib/vps-host";
import type { VpsContainer } from "../src/types/host-status";

describe("VPS diagram evidence", () => {
  it("matches bindings by address and protocol, keeps EXPOSE separate, and does not guess unknown host addresses", () => {
    const container: VpsContainer = { id: "proxy", name: "proxy", image: "nginx:test", state: "running", status: "Up", health: null,
      cpuPercent: null, memoryUsage: null, memoryPercent: null, listeningPorts: null,
      ports: [{ containerPort: 8443, protocol: "tcp", hostIp: "127.0.0.1", hostPort: 9003 },
        { containerPort: 9004, protocol: "tcp", hostIp: null, hostPort: null }] };
    const socket = { address: "127.0.0.1", port: 9003, protocol: "tcp", process: "docker-proxy" };
    expect(containersForPort(socket, [container])).toEqual([container]);
    expect(containersForPort({ ...socket, address: "0.0.0.0" }, [container])).toEqual([]);
    expect(containersForPort({ ...socket, protocol: "udp" }, [container])).toEqual([]);
    expect(containersForPort({ ...socket, port: 9004 }, [container])).toEqual([]);
    expect(containersForPort(socket, [{ ...container, ports: [{ containerPort: 8443, protocol: "tcp", hostIp: null, hostPort: 9003 }] }])).toEqual([]);
  });

  it("recognizes service aliases and comma-separated process names without matching unrelated processes", () => {
    const ports = [{ address: "0.0.0.0", port: 443, protocol: "tcp", process: "nginx, nginx-worker" },
      { address: "::1", port: 22, protocol: "tcp", process: "sshd" },
      { address: "::", port: 5353, protocol: "udp", process: null }];
    expect(portsForService("nginx.service", ports)).toEqual([ports[0]]);
    expect(portsForService("ssh", ports)).toEqual([ports[1]]);
    expect(portsForService("ufw", ports)).toEqual([]);
  });

  it("labels loopback, wildcard, unknown, and specific addresses and brackets IPv6 endpoints", () => {
    expect(portScope("127.0.0.2", true)).toBe("Chỉ localhost");
    expect(portScope("[::1]", true)).toBe("Chỉ localhost");
    expect(portScope("::", true)).toBe("Mọi địa chỉ mạng");
    expect(portScope("192.0.2.1", true)).toBe("Địa chỉ mạng cụ thể");
    expect(portScope(null, false)).toBe("Unknown address");
    expect(endpoint("::1", 22)).toBe("[::1]:22");
  });
});
