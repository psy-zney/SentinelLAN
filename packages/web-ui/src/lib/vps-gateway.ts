import type { VpsHostSnapshot } from "@/types/host-status";
import { endpoint, getProjectMeta, portsForService } from "./vps-host";

export const projectGatewayPolicy = [
  { id: "beatsync", name: "BeatSync", port: 9001, route: "beatsync-server.zney295.id.vn/", tls: false },
  { id: "livekit", name: "LiveKit", port: 9002, route: "voice.zney295.id.vn/", tls: false },
  { id: "sentinellan", name: "SentinelLAN", port: 9003, route: "manager.zney295.id.vn/company · /employee · /api", tls: true },
  { id: "monopoly", name: "Monopoly", port: 9004, route: "beatsync-server.zney295.id.vn/monopoly/", tls: false },
  { id: "kitchen-explore", name: "Kitchen Explore", port: 9005, route: "beatsync-server.zney295.id.vn/kittens/", tls: false },
] as const;

export function gatewayEvidence(snapshot: VpsHostSnapshot) {
  const gatewayPorts = portsForService("nginx", snapshot.listeningPorts).filter(port => port.protocol === "tcp" && port.port === 9000);
  return {
    http: gatewayPorts.some(port => port.address === "127.0.0.1"),
    tls: gatewayPorts.some(port => port.address === "127.0.0.2"),
    projects: projectGatewayPolicy.map(policy => {
      const containers = snapshot.runtime.containers.filter(container => getProjectMeta(container.project, container.name).id === policy.id);
      const bindings = containers.flatMap(container => (container.ports ?? [])
        .filter(binding => binding.hostPort !== null && binding.protocol === "tcp")
        .map(binding => ({ container, binding })));
      const servicePorts = policy.id === "beatsync" ? portsForService("beatsync", snapshot.listeningPorts).filter(port => port.protocol === "tcp") : [];
      const expected = { address: "127.0.0.1", port: policy.port, protocol: "tcp" };
      const hasSocket = snapshot.listeningPorts.some(port => port.address === expected.address && port.port === expected.port && port.protocol === expected.protocol);
      const matches = policy.id === "beatsync"
        ? snapshot.runtime.services.some(service => service.name.replace(/\.service$/, "") === "beatsync" && service.activeState === "active")
          && servicePorts.some(port => port.address === expected.address && port.port === expected.port)
        : bindings.some(({ container, binding }) => container.state === "running" && binding.hostIp === expected.address && binding.hostPort === expected.port);
      const endpoints = [...bindings.map(({ binding }) => endpoint(binding.hostIp, binding.hostPort!)), ...servicePorts.map(port => endpoint(port.address, port.port))];
      const onlyExpected = endpoints.length > 0 && endpoints.every(value => value === endpoint(expected.address, expected.port));
      return { ...policy, containers, bindings, endpoints: [...new Set(endpoints)], matches: matches && hasSocket && onlyExpected };
    }),
  };
}
