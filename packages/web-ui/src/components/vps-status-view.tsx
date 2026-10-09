"use client";

import { Fragment, useCallback, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { ApiClient, ApiError } from "@/lib/api-client";
import { containersForPort, endpoint, getProjectMeta, portScope } from "@/lib/vps-host";
import { VpsServiceDiagram } from "@/components/vps-service-diagram";
import { useTranslation } from "@/lib/i18n";
import type { VpsHostStatus, VpsContainer, VpsListeningPort } from "@/types/host-status";

function bytes(value: number) {
  return `${(value / 1024 ** 3).toFixed(1)} GiB`;
}

function formatBytes(val: number): string {
  if (val >= 1024 ** 3) return `${(val / 1024 ** 3).toFixed(2)} GiB`;
  if (val >= 1024 ** 2) return `${(val / 1024 ** 2).toFixed(1)} MiB`;
  if (val >= 1024) return `${(val / 1024).toFixed(1)} KiB`;
  return `${val} B`;
}

/* ──────────────────────────── SVG Icons ──────────────────────────── */
const svgProps = { width: 18, height: 18, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 1.8, strokeLinecap: "round" as const, strokeLinejoin: "round" as const, "aria-hidden": true as const };

function IconCpu() { return <svg {...svgProps}><rect x="4" y="4" width="16" height="16" rx="2" /><path d="M9 1v3M15 1v3M9 20v3M15 20v3M1 9h3M1 15h3M20 9h3M20 15h3" /><path d="M9 9h6v6H9z" /></svg>; }
function IconRam() { return <svg {...svgProps}><rect x="2" y="6" width="20" height="12" rx="2" /><path d="M6 6V4M10 6V4M14 6V4M18 6V4M6 18v2M10 18v2M14 18v2M18 18v2" /><path d="M6 10h2v4H6zM10 10h2v4h-2zM14 10h2v4h-2z" fill="currentColor" opacity=".15" /></svg>; }
function IconDisk() { return <svg {...svgProps}><circle cx="12" cy="12" r="10" /><circle cx="12" cy="12" r="3" /><path d="M12 2v7" /></svg>; }
function IconDocker() { return <svg {...svgProps}><path d="M22 12.5c-.5-2-2.5-3-4-2.5 0-2-1.5-3.5-3.5-3.5-.5 0-1 .1-1.5.3C12 5.8 10.5 5 9 5 6 5 3.5 7.5 3.5 10.5c0 .3 0 .7.1 1C2 12.5 1 14.5 2 16.5S5.5 19 7 19h13c2 0 3.5-1.5 3.5-3.5 0-1.5-1-2.5-1.5-3Z" /><path d="M8 11h2v2H8zM11 11h2v2h-2zM14 11h2v2h-2zM11 8h2v2h-2z" fill="currentColor" opacity=".15" /></svg>; }
function IconService() { return <svg {...svgProps}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83-2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" /></svg>; }
function IconPort() { return <svg {...svgProps}><circle cx="12" cy="12" r="2" /><path d="M16.24 7.76a6 6 0 0 1 0 8.49M19.07 4.93a10 10 0 0 1 0 14.14M7.76 16.24a6 6 0 0 1 0-8.49M4.93 19.07a10 10 0 0 1 0-14.14" /></svg>; }
function IconRefresh() { return <svg {...svgProps}><polyline points="23 4 23 10 17 10" /><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" /></svg>; }
function IconCheck() { return <svg {...svgProps} width={14} height={14}><polyline points="20 6 9 17 4 12" /></svg>; }
function IconX() { return <svg {...svgProps} width={14} height={14}><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></svg>; }
function IconSearch() { return <svg {...svgProps} width={14} height={14}><circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" /></svg>; }
function IconChevron({ open }: { open: boolean }) { return <svg {...svgProps} width={16} height={16} style={{ transition: "transform .2s", transform: open ? "rotate(90deg)" : "rotate(0)" }}><polyline points="9 18 15 12 9 6" /></svg>; }
function IconServer() { return <svg {...svgProps}><rect x="2" y="2" width="20" height="8" rx="2" /><rect x="2" y="14" width="20" height="8" rx="2" /><line x1="6" y1="6" x2="6.01" y2="6" /><line x1="6" y1="18" x2="6.01" y2="18" /></svg>; }
function IconUptime() { return <svg {...svgProps}><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></svg>; }

/* ──────────────────────────── Donut Gauge ─────────────────────────── */
function DonutGauge({ percent, color, size = 80, strokeWidth = 7 }: { percent: number; color: string; size?: number; strokeWidth?: number }) {
  const r = (size - strokeWidth) / 2;
  const circ = 2 * Math.PI * r;
  const offset = circ * (1 - Math.min(percent, 100) / 100);
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ display: "block" }}>
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="#e8ebee" strokeWidth={strokeWidth} />
      <circle
        cx={size / 2} cy={size / 2} r={r} fill="none"
        stroke={color} strokeWidth={strokeWidth}
        strokeDasharray={circ} strokeDashoffset={offset}
        strokeLinecap="round"
        transform={`rotate(-90 ${size / 2} ${size / 2})`}
        style={{ transition: "stroke-dashoffset .6s ease" }}
      />
      <text x={size / 2} y={size / 2} textAnchor="middle" dominantBaseline="central"
        fill="var(--ink)" fontSize={size * 0.2} fontWeight={650} fontFamily="inherit">
        {percent.toFixed(1)}%
      </text>
    </svg>
  );
}

function gaugeColor(pct: number) {
  if (pct >= 90) return "#b02c2c";
  if (pct >= 70) return "#c27b1a";
  return "#18634e";
}

function DisclosurePanel({ title, icon, badge, description, children, vi }: {
  title: string; icon: ReactNode; badge: ReactNode; description?: string; children: ReactNode; vi: boolean;
}) {
  const [open, setOpen] = useState(false);
  const id = useId();
  return <section className="panel vps-disclosure">
    <div className="panel-head">
      <h2 className="vps-disclosure-title"><button type="button" className="vps-disclosure-toggle" aria-expanded={open} aria-controls={id} onClick={() => setOpen(value => !value)}>
        {icon}<span>{title}</span><IconChevron open={open} />
        <small>{open ? (vi ? "Thu gọn" : "Collapse") : (vi ? "Xem chi tiết" : "View details")}</small>
      </button></h2>
      {badge}
    </div>
    <div id={id} hidden={!open}>
      {description ? <p className="vps-section-description">{description}</p> : null}
      {children}
    </div>
  </section>;
}

function PortTable({ ports, containers, vi }: { ports: VpsListeningPort[]; containers: VpsContainer[]; vi: boolean }) {
  const [selected, setSelected] = useState<string | null>(null);
  const id = useId();
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  if (!ports.length) return <p className="vps-section-description">{label("Không có cổng phù hợp bộ lọc.", "No ports match the filter.")}</p>;
  return <div className="vps-table"><table><thead><tr>
    <th>{label("Địa chỉ nghe", "Listening address")}</th><th>{label("Cổng", "Port")}</th>
    <th>{label("Giao thức", "Protocol")}</th><th>{label("Phạm vi nghe", "Binding scope")}</th>
    <th>{label("Tiến trình / Container", "Process / Container")}</th>
  </tr></thead><tbody>{ports.map((port, index) => {
    const key = `${port.protocol}-${port.address}-${port.port}-${index}`;
    const open = selected === key;
    const owners = containersForPort(port, containers);
    return <Fragment key={key}>
      <tr>
        <td><code>{port.address}</code></td>
        <td><button type="button" className="vps-port-detail-toggle" aria-label={`${label("Chi tiết cổng", "Port details")} ${endpoint(port.address, port.port)}/${port.protocol}`} aria-expanded={open} aria-controls={`${id}-${index}`} onClick={() => setSelected(open ? null : key)}>
          <strong>{port.port}</strong><IconChevron open={open} />
        </button></td>
        <td><span className="badge badge-info">{port.protocol.toUpperCase()}</span></td>
        <td><span className={`badge ${portScope(port.address, vi) === (vi ? "Chỉ localhost" : "Localhost only") ? "badge-neutral" : "badge-info"}`}>{portScope(port.address, vi)}</span></td>
        <td>{port.process || label("Chưa xác định tiến trình", "Unknown process")}{owners.map(container => <small className="vps-port-owner" key={container.id}>{container.name}</small>)}</td>
      </tr>
      <tr id={`${id}-${index}`} hidden={!open}><td colSpan={5}>
        <div className="vps-container-detail-grid">
          <div><span className="vps-detail-label">{label("Địa chỉ đầy đủ", "Full endpoint")}</span><code>{endpoint(port.address, port.port)}/{port.protocol}</code></div>
          <div><span className="vps-detail-label">{label("Tiến trình trên VPS", "Host process")}</span><span>{port.process || label("Chưa xác định", "Unknown")}</span></div>
          <div><span className="vps-detail-label">{label("Phạm vi nghe", "Binding scope")}</span><span>{portScope(port.address, vi)}</span></div>
          <div><span className="vps-detail-label">{label("Ánh xạ Docker tương ứng", "Matching Docker bindings")}</span>
            {owners.length ? owners.map(container => <div key={container.id}><strong>{container.name}</strong>{container.ports?.filter(binding => binding.hostPort === port.port && binding.protocol.toLowerCase() === port.protocol.toLowerCase()).map((binding, bindingIndex) => <code className="vps-port-owner" key={bindingIndex}>{endpoint(binding.hostIp, port.port)} → {binding.containerPort}/{binding.protocol}</code>)}</div>) : <span>{label("Không có ánh xạ Docker tương ứng trong bản ghi.", "No matching Docker binding in this snapshot.")}</span>}
          </div>
        </div>
        <p className="vps-section-description">{label("Phạm vi nghe không xác nhận khả năng truy cập từ Internet; quyền truy cập còn phụ thuộc firewall và reverse proxy.", "Binding scope does not confirm Internet access; access also depends on the firewall and reverse proxy.")}</p>
      </td></tr>
    </Fragment>;
  })}</tbody></table></div>;
}

/* ─────────────────────── Container Card ──────────────────────────── */
function ContainerCard({ container, vi, metric }: { container: VpsContainer; vi: boolean; metric?: "cpu" | "ram" }) {
  const [open, setOpen] = useState(false);
  const id = useId();
  const running = container.state === "running" && container.health !== "unhealthy";
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  const publishedPorts = container.ports?.filter(p => p.hostPort !== null) ?? [];
  const exposedPorts = container.ports?.filter(p => p.hostPort === null) ?? [];
  const meta = getProjectMeta(container.project, container.name);

  return (
    <div className="vps-container-card" data-state={running ? "ok" : "warn"}>
      <button className="vps-container-header" onClick={() => setOpen(o => !o)} aria-expanded={open} aria-controls={id} type="button">
        <span className="vps-container-icon" style={{ fontSize: 18 }} title={meta.name}>{meta.icon}</span>
        <div style={{ display: "flex", flexDirection: "column", gap: 1, minWidth: 0, textAlign: "left", flex: 1 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <span className="vps-container-name">{container.name}</span>
            <span className={`badge ${meta.badgeClass}`} style={{ fontSize: 11, padding: "1px 6px" }}>
              {meta.name}
            </span>
          </div>
        </div>
        <span className={`badge ${running ? "badge-success" : "badge-warn"}`} style={{ marginLeft: 8 }}>
          {running ? <IconCheck /> : <IconX />}
          {container.state}{container.health ? ` · ${container.health}` : ""}
        </span>
        <span className="vps-container-stats">
          {metric !== "ram" && container.cpuPercent !== null ? `${container.cpuPercent.toFixed(1)}% CPU` : ""}
          {metric !== "cpu" && container.memoryUsage ? `${metric ? "" : " · "}${container.memoryUsage}` : ""}
        </span>
        <span className="vps-container-chevron"><IconChevron open={open} /></span>
      </button>
        <div className="vps-container-body" id={id} hidden={!open}>
          <div className="vps-container-detail-grid">
            <div><span className="vps-detail-label">{label("Dự án", "Project")}</span><span className="vps-detail-value"><strong>{meta.icon} {meta.name}</strong></span></div>
            {!metric && <div><span className="vps-detail-label">Image</span><code className="vps-detail-value">{container.image}</code></div>}
            <div><span className="vps-detail-label">Status</span><span className="vps-detail-value">{container.status}</span></div>
            {metric !== "ram" && <div><span className="vps-detail-label">CPU</span><span className="vps-detail-value">{container.cpuPercent !== null ? `${container.cpuPercent.toFixed(2)}%` : label("Chưa có số liệu", "No metrics available")}</span></div>}
            {metric !== "cpu" && <div><span className="vps-detail-label">RAM</span><span className="vps-detail-value">{container.memoryUsage ? `${container.memoryUsage}${container.memoryPercent !== null ? ` (${container.memoryPercent.toFixed(1)}%)` : ""}` : label("Chưa có số liệu", "No metrics available")}</span></div>}
          </div>
          {!metric && publishedPorts.length > 0 && (
            <div className="vps-container-ports">
              <span className="vps-detail-label"><IconPort /> {label("Cổng công bố", "Published ports")}</span>
              {publishedPorts.map((port, i) => <div key={i}><code>{endpoint(port.hostIp, port.hostPort!)} → {port.containerPort}/{port.protocol}</code><span className="badge badge-neutral">{portScope(port.hostIp, vi)}</span></div>)}
            </div>
          )}
          {!metric && exposedPorts.length > 0 && <div className="vps-container-ports">
            <span className="vps-detail-label">{label("Cổng EXPOSE — không có ánh xạ ra VPS", "EXPOSE ports — no host mapping")}</span>
            {exposedPorts.map((port, i) => <code key={i}>{port.containerPort}/{port.protocol}</code>)}
          </div>}
          {!metric && container.listeningPorts && container.listeningPorts.length > 0 && (
            <div className="vps-container-ports">
              <span className="vps-detail-label">{label("Đang nghe trong container", "Listening in container")}</span>
              {container.listeningPorts.map((port, i) => <div key={i}><code>{endpoint(port.address, port.port)}/{port.protocol}</code><span className="badge badge-neutral">{portScope(port.address, vi)}</span></div>)}
            </div>
          )}
          {!metric && <p className="vps-section-description">{label("Địa chỉ nghe trong container thuộc mạng của container. Cổng EXPOSE không chứng minh cổng đang nghe hay truy cập được từ VPS.", "Container listening addresses belong to its network. EXPOSE does not prove a port is listening or reachable from the host.")}</p>}
        </div>
    </div>
  );
}

/* ──────────────────────────── Main View ──────────────────────────── */
export function VpsStatusView() {
  const { lang, t } = useTranslation();
  const vi = lang === "vi";
  const [data, setData] = useState<VpsHostStatus | null>(null);
  const [error, setError] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [checkedAt, setCheckedAt] = useState(0);
  const [activeResource, setActiveResource] = useState<"cpu" | "ram" | "disk" | "docker" | null>(null);
  const [selectedProject, setSelectedProject] = useState<string>("all");
  const [portSearch, setPortSearch] = useState("");
  const [portProtoFilter, setPortProtoFilter] = useState<"all" | "tcp" | "udp">("all");
  const [tcpOpen, setTcpOpen] = useState(true);
  const [udpOpen, setUdpOpen] = useState(false);
  const pending = useRef<AbortController | null>(null);

  const refresh = useCallback(() => {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    setBusy(true);
    setCheckedAt(Date.now());
    void new ApiClient().hostStatus(controller.signal).then(value => {
      if (!controller.signal.aborted) { setData(value); setError(null); }
    }).catch((reason: unknown) => {
      if (!controller.signal.aborted) {
        setError(reason instanceof ApiError ? reason.status : 0);
        if (reason instanceof ApiError && [401, 403].includes(reason.status)) setData(null);
      }
    }).finally(() => { if (!controller.signal.aborted) setBusy(false); });
  }, []);

  useEffect(() => {
    const initial = window.setTimeout(refresh, 0);
    const timer = window.setInterval(refresh, 60_000);
    return () => { window.clearTimeout(initial); window.clearInterval(timer); pending.current?.abort(); };
  }, [refresh]);

  const snapshot = data?.snapshot;
  const elapsed = snapshot ? (checkedAt - Date.parse(snapshot.capturedAtUtc)) / 1000 : 0;
  const stale = data?.stale || (snapshot && (elapsed > 180 || elapsed < -60)) || error !== null;
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  const toggleResource = (resource: "cpu" | "ram" | "disk" | "docker") => setActiveResource(current => current === resource ? null : resource);
  const resourceAction = (resource: string) => <span className="vps-gauge-action"><IconChevron open={activeResource === resource} />{activeResource === resource ? label("Thu gọn", "Collapse") : label("Xem chi tiết", "View details")}</span>;

  const allContainers = snapshot?.runtime.containers ?? [];
  const runningContainers = allContainers.filter(c => c.state === "running").length;
  const totalContainers = allContainers.length;

  // Project groups for filtering
  const projectGroups = [
    { id: "all", name: label("Tất cả dự án", "All projects"), icon: "⚡", count: allContainers.length },
    { id: "sentinellan", name: "SentinelLAN", icon: "🛡️", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "sentinellan").length },
    { id: "monopoly", name: "Monopoly (mpoly)", icon: "🎲", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "monopoly").length },
    { id: "kitchen-explore", name: "Kitchen Explore", icon: "🐱", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "kitchen-explore").length },
    { id: "mot-me-banh", name: "Mọt Mê Bánh", icon: "🥖", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "mot-me-banh").length },
    { id: "livekit", name: "LiveKit", icon: "📹", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "livekit").length },
    { id: "beatsync", name: "BeatSync", icon: "🎵", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "beatsync").length },
    { id: "other", name: label("Khác", "Other"), icon: "📦", count: allContainers.filter(c => getProjectMeta(c.project, c.name).id === "other").length },
  ].filter(g => g.id === "all" || g.count > 0);

  const displayedContainers = selectedProject === "all"
    ? allContainers
    : allContainers.filter(c => getProjectMeta(c.project, c.name).id === selectedProject);

  const allListeningPorts = snapshot?.listeningPorts ?? [];
  const searchLower = portSearch.trim().toLowerCase();

  const filteredPorts = allListeningPorts.filter(p => {
    if (searchLower) {
      const matchAddr = p.address.toLowerCase().includes(searchLower);
      const matchPort = String(p.port).includes(searchLower);
      const matchProto = p.protocol.toLowerCase().includes(searchLower);
      const matchProc = (p.process || "").toLowerCase().includes(searchLower);
      const matchContainer = containersForPort(p, allContainers).some(container => `${container.name} ${container.project ?? ""}`.toLowerCase().includes(searchLower));
      if (!matchAddr && !matchPort && !matchProto && !matchProc && !matchContainer) return false;
    }
    return true;
  });

  const tcpPorts = filteredPorts.filter(p => p.protocol.toLowerCase() === "tcp");
  const udpPorts = filteredPorts.filter(p => p.protocol.toLowerCase() === "udp");
  const totalTcpCount = allListeningPorts.filter(p => p.protocol.toLowerCase() === "tcp").length;
  const totalUdpCount = allListeningPorts.filter(p => p.protocol.toLowerCase() === "udp").length;

  return <>
    {/* ── Header ── */}
    <section className="panel" aria-label={t("vpsStatus")}>
      <div className="panel-head">
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 40, height: 40, borderRadius: 8, background: "var(--accent-soft)", color: "var(--accent)" }}><IconServer /></span>
          <div>
            <h2 style={{ margin: 0 }}>{snapshot ? `${snapshot.name} · ${snapshot.host}` : t("vpsStatus")}</h2>
            <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>{label("Tự động kiểm tra mỗi phút.", "Refreshes every minute.")}</p>
          </div>
        </div>
        <button className="action-outline" onClick={refresh} disabled={busy} style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          <IconRefresh />
          {busy ? t("loading") : t("refresh")}
        </button>
      </div>
      {error !== null ? <p role="alert">{error === 403
        ? label("Chỉ Admin của đơn vị vận hành được xem tình trạng VPS.", "Only the operating organization's Admin can view this VPS.")
        : t("error")}</p> : null}
      {!data && error === null ? <p role="status">{t("loading")}</p> : null}
      {data && !snapshot ? <p role="status">{vi ? data.message : data.configured ? "Host status is unavailable. Check the collector." : "Host monitoring is not configured."}</p> : null}
      {snapshot ? <>
        <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12, marginTop: 8 }}>
          <span className={`badge ${stale ? "badge-warn" : "badge-success"}`}>
            {stale ? label("Dữ liệu cũ — chưa xác nhận trạng thái hiện tại", "Old data — current status is unconfirmed") : label("Đã cập nhật", "Up to date")}
          </span>
          <span style={{ color: "var(--muted)", fontSize: 13 }}>
            <IconUptime /> {label("Lần kiểm tra", "Last checked")}: <time dateTime={snapshot.capturedAtUtc}>
              {new Date(snapshot.capturedAtUtc).toLocaleString(vi ? "vi-VN" : "en-GB")}
            </time>
          </span>
        </div>
        <p style={{ marginTop: 6, color: "var(--muted)", fontSize: 13 }}>{snapshot.osInfo} · {label("Đã chạy", "Uptime")}: {Math.floor(snapshot.uptimeSeconds / 86400)} {label("ngày", "days")} {Math.floor(snapshot.uptimeSeconds % 86400 / 3600)} {label("giờ", "hours")}</p>
      </> : null}
    </section>

    {snapshot ? <>
      {/* ── Resource Gauges ── */}
      <section className="vps-gauges" aria-label={label("Tài nguyên VPS", "VPS resources")}>
        <button type="button" className="vps-gauge-card" aria-label="CPU" aria-expanded={activeResource === "cpu"} aria-controls="vps-cpu-details" onClick={() => toggleResource("cpu")}>
          <div className="vps-gauge-header"><IconCpu /><span>CPU</span></div>
          <DonutGauge percent={snapshot.cpuPercent} color={gaugeColor(snapshot.cpuPercent)} />
          {resourceAction("cpu")}
        </button>
        <button type="button" className="vps-gauge-card" aria-label="RAM" aria-expanded={activeResource === "ram"} aria-controls="vps-ram-details" onClick={() => toggleResource("ram")}>
          <div className="vps-gauge-header"><IconRam /><span>RAM</span></div>
          <DonutGauge percent={snapshot.ramPercent} color={gaugeColor(snapshot.ramPercent)} />
          <small className="vps-gauge-detail">{bytes(snapshot.memoryUsedBytes)} / {bytes(snapshot.memoryTotalBytes)}</small>
          {resourceAction("ram")}
        </button>
        <button type="button" className="vps-gauge-card" aria-label={label("Ổ đĩa", "Disk")} aria-expanded={activeResource === "disk"} aria-controls="vps-disk-details" onClick={() => toggleResource("disk")}>
          <div className="vps-gauge-header"><IconDisk /><span>{label("Ổ đĩa", "Disk")}</span></div>
          <DonutGauge percent={snapshot.diskPercent} color={gaugeColor(snapshot.diskPercent)} />
          <small className="vps-gauge-detail">{bytes(snapshot.diskUsedBytes)} / {bytes(snapshot.diskTotalBytes)}</small>
          {resourceAction("disk")}
        </button>
        <button type="button" className="vps-gauge-card" aria-label="Docker" aria-expanded={activeResource === "docker"} aria-controls="vps-docker-details" onClick={() => toggleResource("docker")}>
          <div className="vps-gauge-header"><IconDocker /><span>Docker</span></div>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 6, flex: 1, justifyContent: "center" }}>
            <strong style={{ fontSize: 28, fontWeight: 650, letterSpacing: "-.025em", fontVariantNumeric: "tabular-nums", color: snapshot.runtime.dockerAvailable ? "var(--accent)" : "var(--danger)" }}>
              {snapshot.runtime.dockerAvailable ? label("Hoạt động", "Active") : label("Chưa đọc được", "Unavailable")}
            </strong>
            <span className={`badge ${runningContainers === totalContainers ? "badge-success" : "badge-warn"}`}>
              {runningContainers} / {totalContainers} container {label("đang chạy", "running")}
            </span>
          </div>
          {resourceAction("docker")}
        </button>
      </section>

      {snapshot.runtime.dockerError ? <p role="alert" style={{ color: "var(--danger)", margin: "0 0 16px", fontSize: 13 }}>{snapshot.runtime.dockerError}</p> : null}
      {snapshot.warnings.length ? <div className="panel" role="alert"><ul>{snapshot.warnings.map((warning, index) => <li key={index}>{warning}</li>)}</ul></div> : null}

      {(["cpu", "ram"] as const).map(resource => <section key={resource} className="panel vps-resource-details" id={`vps-${resource}-details`} hidden={activeResource !== resource}>
        <div className="panel-head"><h2>{resource === "cpu" ? label("Chi tiết CPU", "CPU details") : label("Chi tiết RAM", "RAM details")}</h2><button type="button" className="action-outline" onClick={() => setActiveResource(null)}>{label("Thu gọn", "Collapse")}</button></div>
        <p>{resource === "cpu" ? `${label("CPU toàn VPS", "Host CPU")}: ${snapshot.cpuPercent.toFixed(1)}%` : `${label("RAM toàn VPS", "Host RAM")}: ${bytes(snapshot.memoryUsedBytes)} / ${bytes(snapshot.memoryTotalBytes)} (${snapshot.ramPercent.toFixed(1)}%) · ${label("Còn khả dụng", "Available")}: ${bytes(snapshot.memoryTotalBytes - snapshot.memoryUsedBytes)}`}</p>
        <p className="vps-section-description">{label("Số liệu bên dưới là từng container Docker, không bao gồm mọi tiến trình trên VPS. Bấm từng container để xem chi tiết.", "The following metrics cover individual Docker containers, not every host process. Click a container for details.")}</p>
        <div className="vps-containers-grid">{allContainers.length ? allContainers.map(container => <ContainerCard key={container.id} container={container} vi={vi} metric={resource} />) : <p>{label("Chưa có số liệu container.", "No container metrics available.")}</p>}</div>
      </section>)}

      {/* ── Storage Breakdown by Repo & Service ── */}
      <section className="panel vps-resource-details" id="vps-disk-details" hidden={activeResource !== "disk"}>
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#fbf3e5", color: "var(--warn)" }}>
              <IconDisk />
            </span>
            <div>
              <h2 style={{ margin: 0 }}>{label("Dung lượng ổ đĩa theo Repo & Dịch vụ", "Disk Usage by Repository & Service")}</h2>
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 12 }}>
                {label("Xếp hạng các thư mục dự án và thành phần Docker chiếm nhiều dung lượng nhất trên VPS.", "Ranking of project directories and Docker components using the most disk space.")}
              </p>
            </div>
          </div>
          <div className="vps-section-actions"><span className="badge badge-info">
            {bytes(snapshot.diskUsedBytes)} / {bytes(snapshot.diskTotalBytes)} ({snapshot.diskPercent}%)
          </span><button type="button" className="action-outline" onClick={() => setActiveResource(null)}>{label("Thu gọn", "Collapse")}</button></div>
        </div>

        {/* Visual Disk Usage Bar */}
        <div className="vps-storage-meter">
          <div className="vps-storage-legend">
            <span><strong>{label("Đã dùng", "Used")}: {bytes(snapshot.diskUsedBytes)}</strong></span>
            <span><strong>{label("Còn trống", "Free")}: {bytes(snapshot.diskTotalBytes - snapshot.diskUsedBytes)}</strong></span>
          </div>
          <div className="vps-storage-bar-bg">
            <div className="vps-storage-bar-fill" style={{ width: `${snapshot.diskPercent}%`, background: gaugeColor(snapshot.diskPercent) }} />
          </div>
        </div>

        {snapshot.runtime.storageBreakdown && snapshot.runtime.storageBreakdown.length > 0 ? (
          <div className="vps-table vps-storage-table">
            <table>
              <thead>
                <tr>
                  <th style={{ width: 44 }}>#</th>
                  <th>{label("Repo / Thành phần", "Repository / Component")}</th>
                  <th>{label("Phân loại", "Category")}</th>
                  <th>{label("Tỷ lệ sử dụng", "Usage ratio")}</th>
                  <th>{label("Dung lượng", "Size")}</th>
                  <th>{label("Ghi chú / Thu hồi", "Notes / Reclaimable")}</th>
                </tr>
              </thead>
              <tbody>
                {snapshot.runtime.storageBreakdown.map((item, idx) => {
                  const pctOfUsed = snapshot.diskUsedBytes > 0 ? (item.sizeBytes / snapshot.diskUsedBytes) * 100 : 0;
                  return (
                    <tr key={item.name + idx}>
                      <td><span className="vps-storage-rank">#{idx + 1}</span></td>
                      <td>
                        <details className="vps-storage-item"><summary><strong>{item.name}</strong></summary>
                          <div className="vps-storage-item-detail"><span className="vps-detail-label">{label("Đường dẫn được đo", "Measured path")}</span><code>{item.path}</code>
                            <span>{item.sizeBytes.toLocaleString(vi ? "vi-VN" : "en-GB")} {label("byte", "bytes")} · {pctOfUsed.toFixed(1)}% {label("dung lượng đã dùng", "of used disk")}</span>
                            <small>{item.category === "docker" ? label("Dung lượng Docker dùng chung trên VPS; không quy về một dự án.", "Shared Docker storage on this host, not attributed to one project.") : label("Dung lượng các thư mục được đo; không bao gồm toàn bộ image và volume của dự án.", "Measured directories; does not include all project images and volumes.")}</small>
                          </div>
                        </details>
                      </td>
                      <td>
                        <span className={`badge ${item.category === "docker" ? "badge-info" : item.category === "repo" ? "badge-success" : "badge-neutral"}`} style={{ fontSize: 11 }}>
                          {item.category === "docker" ? "Docker" : item.category === "repo" ? "Git Repo" : item.category ?? "System"}
                        </span>
                      </td>
                      <td>
                        <div style={{ display: "flex", alignItems: "center" }}>
                          <span className="vps-storage-bar-mini">
                            <span className="vps-storage-bar-mini-fill" style={{ width: `${Math.min(pctOfUsed, 100)}%` }} />
                          </span>
                          <span style={{ fontSize: 12, color: "var(--muted)", fontVariantNumeric: "tabular-nums" }}>{pctOfUsed.toFixed(1)}%</span>
                        </div>
                      </td>
                      <td>
                        <strong style={{ fontSize: 13, fontVariantNumeric: "tabular-nums" }}>
                          {formatBytes(item.sizeBytes)}
                        </strong>
                      </td>
                      <td>
                        {item.reclaimable ? (
                          <span className="badge badge-warn" title="Có thể dọn dẹp để lấy lại dung lượng" style={{ fontSize: 11 }}>
                            ♻️ {label("Có thể thu hồi", "Reclaimable")}: {item.reclaimable}
                          </span>
                        ) : (
                          <span style={{ color: "var(--muted)", fontSize: 12 }}>—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <p style={{ color: "var(--muted)", margin: "8px 0" }}>{label("Chưa có thông tin phân bổ chi tiết.", "No storage breakdown details available.")}</p>
        )}
      </section>

      {/* ── Docker Containers with Project Filters ── */}
      <section className="panel vps-resource-details" id="vps-docker-details" hidden={activeResource !== "docker"}>
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#edf5f1", color: "var(--accent)" }}><IconDocker /></span>
            <div>
              <h2 style={{ margin: 0 }}>{label("Docker · Các dự án trên VPS", "Docker · Host Projects & Containers")}</h2>
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 12 }}>{label("Cổng công bố là cổng trên VPS; cổng EXPOSE chỉ là khai báo của image.", "Published ports belong to the host; EXPOSE ports are image metadata.")}</p>
            </div>
          </div>
          <div className="vps-section-actions"><span className={`badge ${runningContainers === totalContainers ? "badge-success" : "badge-warn"}`}>
            {runningContainers}/{totalContainers} {label("đang chạy", "running")}
          </span><button type="button" className="action-outline" onClick={() => setActiveResource(null)}>{label("Thu gọn", "Collapse")}</button></div>
        </div>

        {/* Project Filter Pills */}
        <div className="vps-filter-pills" role="group" aria-label={label("Lọc theo dự án", "Filter by project")}>
          {projectGroups.map(g => (
            <button
              key={g.id}
              type="button"
              className="vps-filter-pill"
              data-active={selectedProject === g.id}
              aria-pressed={selectedProject === g.id}
              onClick={() => setSelectedProject(g.id)}
            >
              <span>{g.icon}</span>
              <span>{g.name}</span>
              <span className="badge badge-neutral" style={{ fontSize: 10, padding: "1px 5px" }}>{g.count}</span>
            </button>
          ))}
        </div>

        <div className="vps-containers-grid">
          {displayedContainers.length > 0 ? (
            displayedContainers.map(container => (
              <ContainerCard key={container.id} container={container} vi={vi} />
            ))
          ) : (
            <p style={{ color: "var(--muted)", padding: 12 }}>{label("Không có container nào trong dự án này.", "No containers found in this project.")}</p>
          )}
        </div>
      </section>

      {/* ── Services ── */}
      <DisclosurePanel vi={vi} title={label("Dịch vụ trên VPS", "Host services")} icon={<IconService />} badge={<span className="badge badge-info">{snapshot.runtime.services.length} {label("dịch vụ", "services")}</span>}>
        <VpsServiceDiagram snapshot={snapshot} vi={vi} />
      </DisclosurePanel>

      {/* ── Host Listening Ports ── */}
      <DisclosurePanel vi={vi} title={label("Cổng đang nghe trên VPS", "Host listening ports")} icon={<IconPort />}
        badge={<span className="badge badge-info">{allListeningPorts.length} {label("cổng", "ports")}</span>}
        description={label("Bấm từng cổng để xem địa chỉ nghe, tiến trình, phạm vi localhost và ánh xạ Docker. Địa chỉ 0.0.0.0 / :: là mọi địa chỉ mạng, không tự động có nghĩa là truy cập được từ Internet.", "Click a port for its address, process, localhost scope and Docker binding. 0.0.0.0 / :: binds all network addresses; this does not by itself mean Internet access.")}>
        <div className="vps-port-toolbar">
          <div className="vps-section-actions">
            {(["all", "tcp", "udp"] as const).map(protocol => <button key={protocol} type="button" className="vps-filter-pill" data-active={portProtoFilter === protocol} aria-pressed={portProtoFilter === protocol}
              onClick={() => { setPortProtoFilter(protocol); if (protocol === "tcp") setTcpOpen(true); if (protocol === "udp") setUdpOpen(true); }}>
              {protocol === "all" ? label("Tất cả", "All") : protocol.toUpperCase()} ({protocol === "all" ? allListeningPorts.length : protocol === "tcp" ? totalTcpCount : totalUdpCount})
            </button>)}
          </div>
          <label className="vps-port-search-box"><IconSearch /><input className="vps-port-search-input" value={portSearch} onChange={event => setPortSearch(event.target.value)}
            placeholder={label("Tìm địa chỉ, cổng, tiến trình, container...", "Search address, port, process, container...")} aria-label={label("Tìm cổng", "Search ports")} /></label>
          <div className="vps-port-actions">
            <button type="button" className="vps-port-btn-sm" onClick={() => { setTcpOpen(true); setUdpOpen(true); }}>{label("Mở rộng tất cả", "Expand all")}</button>
            <button type="button" className="vps-port-btn-sm" onClick={() => { setTcpOpen(false); setUdpOpen(false); }}>{label("Thu gọn tất cả", "Collapse all")}</button>
          </div>
        </div>
        {(["tcp", "udp"] as const).filter(protocol => portProtoFilter === "all" || protocol === portProtoFilter).map(protocol => {
          const open = protocol === "tcp" ? tcpOpen : udpOpen;
          const ports = protocol === "tcp" ? tcpPorts : udpPorts;
          const total = protocol === "tcp" ? totalTcpCount : totalUdpCount;
          const toggle = () => protocol === "tcp" ? setTcpOpen(value => !value) : setUdpOpen(value => !value);
          return <div className="vps-port-group" key={protocol}>
            <button type="button" className="vps-port-group-header" aria-expanded={open} aria-controls={"vps-" + protocol + "-ports"} onClick={toggle}>
              <span className="vps-section-actions"><IconChevron open={open} /><strong>{protocol.toUpperCase()} ({ports.length}{searchLower ? "/" + total : ""})</strong></span>
              <span>{open ? label("Thu gọn", "Collapse") : label("Xem chi tiết", "View details")}</span>
            </button>
            <div className="vps-port-group-body" id={"vps-" + protocol + "-ports"} hidden={!open}><PortTable ports={ports} containers={allContainers} vi={vi} /></div>
          </div>;
        })}
      </DisclosurePanel>
    </> : null}
  </>;
}
