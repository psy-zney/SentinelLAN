import type { VpsListeningPort, VpsPortBinding } from "@/types/api";

export function localAddress(address: string | null | undefined) {
  return address === "::1" || address?.startsWith("127.") === true;
}

// Keep distinct interfaces/protocols separate and collapse only consecutive 1:1 mappings.
export function publishedPorts(ports: VpsPortBinding[]) {
  const sorted = ports.filter(port => port.hostPort != null).sort((a, b) =>
    `${a.protocol}|${a.hostIp}`.localeCompare(`${b.protocol}|${b.hostIp}`) || a.hostPort! - b.hostPort! || a.containerPort - b.containerPort);
  const groups: { first: VpsPortBinding; last: VpsPortBinding }[] = [];
  for (const port of sorted) {
    const group = groups.at(-1);
    if (group && group.last.protocol === port.protocol && group.last.hostIp === port.hostIp &&
        port.hostPort === group.last.hostPort! + 1 && port.containerPort === group.last.containerPort + 1) group.last = port;
    else groups.push({ first: port, last: port });
  }
  return groups.map(({ first, last }) => {
    const host = first.hostIp ?? "0.0.0.0";
    const address = host.includes(":") ? `[${host}]` : host;
    const range = (start: number, end: number) => start === end ? `${start}` : `${start}–${end}`;
    return { label: `${address}:${range(first.hostPort!, last.hostPort!)} → ${range(first.containerPort, last.containerPort)}/${first.protocol.toUpperCase()}`,
      local: localAddress(host) };
  });
}

export function listeningPorts(ports: VpsListeningPort[]) {
  const sorted = ports.toSorted((a, b) => `${a.protocol}|${a.address}|${a.process ?? ""}`.localeCompare(`${b.protocol}|${b.address}|${b.process ?? ""}`) || a.port - b.port);
  const groups: { first: VpsListeningPort; last: VpsListeningPort }[] = [];
  for (const port of sorted) {
    const group = groups.at(-1);
    if (group && group.last.address === port.address && group.last.protocol === port.protocol &&
        group.last.process === port.process && port.port === group.last.port + 1) group.last = port;
    else groups.push({ first: port, last: port });
  }
  return groups.map(({ first, last }) => ({ ...first, endPort: last.port }));
}
