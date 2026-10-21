import { describe, expect, it } from "vitest";
import { listeningPorts, localAddress, publishedPorts } from "../src/lib/vps-ports";

describe("VPS port display", () => {
  it("keeps declared ports separate from published ports and local IPv6 mappings", () => {
    const result = publishedPorts([
      { containerPort: 8080, protocol: "tcp", hostIp: null, hostPort: null },
      { containerPort: 8443, protocol: "tcp", hostIp: "127.0.0.1", hostPort: 3180 },
      { containerPort: 8443, protocol: "tcp", hostIp: "::1", hostPort: 3180 }
    ]);
    expect(result).toHaveLength(2);
    expect(result.every(port => port.local)).toBe(true);
    expect(result.some(port => port.label === "[::1]:3180 → 8443/TCP")).toBe(true);
    expect(result.some(port => port.label.includes("8080"))).toBe(false);
    expect(localAddress("0.0.0.0")).toBe(false);
    expect(localAddress("::")).toBe(false);
  });
  it("groups large UDP ranges without merging TCP, interfaces or different offsets", () => {
    const result = publishedPorts([
      ...Array.from({ length: 101 }, (_, i) => ({ containerPort: 50000 + i, protocol: "udp", hostIp: "0.0.0.0", hostPort: 50000 + i })),
      { containerPort: 50101, protocol: "tcp", hostIp: "0.0.0.0", hostPort: 50101 },
      { containerPort: 40000, protocol: "udp", hostIp: "0.0.0.0", hostPort: 50101 },
      { containerPort: 50000, protocol: "udp", hostIp: "::", hostPort: 50000 }
    ]);
    expect(result).toHaveLength(4);
    expect(result.some(port => port.label === "0.0.0.0:50000–50100 → 50000–50100/UDP")).toBe(true);
  });
  it("never merges listening ranges from different processes or addresses", () => {
    const result = listeningPorts([
      { address: "*", port: 50000, protocol: "udp", process: "docker-proxy" },
      { address: "*", port: 50001, protocol: "udp", process: "docker-proxy" },
      { address: "*", port: 50002, protocol: "udp", process: "other" },
      { address: "127.0.0.1", port: 50002, protocol: "udp", process: "docker-proxy" }
    ]);
    expect(result).toHaveLength(3);
    expect(result.some(port => port.port === 50000 && port.endPort === 50001)).toBe(true);
  });
});
