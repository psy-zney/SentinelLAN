import type { VpsContainer, VpsListeningPort } from "@/types/host-status";

export function getProjectMeta(project?: string | null, name?: string) {
  const p = (project || "").toLowerCase();
  const n = (name || "").toLowerCase();
  if (p === "sentinellan-prod" || p === "sentinellan" || n.includes("sentinellan")) return { id: "sentinellan", name: "SentinelLAN", badgeClass: "badge-success", icon: "🛡️", color: "#18634e" };
  if (p === "monopoly" || n.includes("monopoly") || n.includes("mpoly")) return { id: "monopoly", name: "Monopoly (mpoly)", badgeClass: "badge-warn", icon: "🎲", color: "#c27b1a" };
  if (p === "exxplore-kittens" || n.includes("kitten") || n.includes("exxplore")) return { id: "kitchen-explore", name: "Kitchen Explore", badgeClass: "badge-info", icon: "🐱", color: "#6b46c1" };
  if (p === "mot-me-banh" || n.includes("banh")) return { id: "mot-me-banh", name: "Mọt Mê Bánh", badgeClass: "badge-warn", icon: "🥖", color: "#d97706" };
  if (p === "livekit" || n.includes("livekit")) return { id: "livekit", name: "LiveKit", badgeClass: "badge-info", icon: "📹", color: "#2563eb" };
  if (p === "beatsync" || n.includes("beat") || n.includes("sync")) return { id: "beatsync", name: "BeatSync", badgeClass: "badge-info", icon: "🎵", color: "#0891b2" };
  return { id: "other", name: project || "Dự án khác", badgeClass: "badge-neutral", icon: "📦", color: "#475569" };
}

export function portScope(address: string | null, vi: boolean) {
  const ip = address?.replace(/^\[|\]$/g, "");
  if (!ip) return vi ? "Chưa rõ địa chỉ" : "Unknown address";
  if (/^127\./.test(ip) || ip === "::1" || ip === "localhost") return vi ? "Chỉ localhost" : "Localhost only";
  if (["0.0.0.0", "::", "*"].includes(ip)) return vi ? "Mọi địa chỉ mạng" : "All network addresses";
  return vi ? "Địa chỉ mạng cụ thể" : "Specific network address";
}

export function endpoint(address: string | null, port: number) {
  const ip = address?.replace(/^\[|\]$/g, "") ?? "?";
  return `${ip.includes(":") ? `[${ip}]` : ip}:${port}`;
}

export function containersForPort(port: VpsListeningPort, containers: VpsContainer[]) {
  const address = port.address.replace(/^\[|\]$/g, "");
  return containers.filter(container => container.ports?.some(binding => {
    const bound = binding.hostIp?.replace(/^\[|\]$/g, "");
    return binding.hostPort === port.port && binding.protocol.toLowerCase() === port.protocol.toLowerCase()
      && (bound === address || address === "*" && ["0.0.0.0", "::"].includes(bound ?? ""));
  }));
}

export function portsForService(name: string, ports: VpsListeningPort[]) {
  const aliases: Record<string, string[]> = { ssh: ["ssh", "sshd"], docker: ["dockerd", "docker-proxy"], postgresql: ["postgres", "postgresql"] };
  const service = name.replace(/\.service$/, "");
  return ports.filter(port => port.process?.split(/,\s*/).some(process => (aliases[service] ?? [service]).includes(process)));
}
