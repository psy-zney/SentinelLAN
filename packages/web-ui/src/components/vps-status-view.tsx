"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { VpsHostStatus, VpsContainer } from "@/types/host-status";

function bytes(value: number) {
  return `${(value / 1024 ** 3).toFixed(1)} GiB`;
}

function formatBytes(val: number): string {
  if (val >= 1024 ** 3) return `${(val / 1024 ** 3).toFixed(2)} GiB`;
  if (val >= 1024 ** 2) return `${(val / 1024 ** 2).toFixed(1)} MiB`;
  if (val >= 1024) return `${(val / 1024).toFixed(1)} KiB`;
  return `${val} B`;
}

function getProjectMeta(project?: string | null, name?: string) {
  const p = (project || "").toLowerCase();
  const n = (name || "").toLowerCase();
  if (p === "sentinellan-prod" || p === "sentinellan" || n.includes("sentinellan")) {
    return { id: "sentinellan", name: "SentinelLAN", badgeClass: "badge-success", icon: "🛡️", color: "#18634e" };
  }
  if (p === "monopoly" || n.includes("monopoly") || n.includes("mpoly")) {
    return { id: "monopoly", name: "Monopoly (mpoly)", badgeClass: "badge-warn", icon: "🎲", color: "#c27b1a" };
  }
  if (p === "exxplore-kittens" || n.includes("kitten") || n.includes("exxplore")) {
    return { id: "kitchen-explore", name: "Kitchen Explore", badgeClass: "badge-info", icon: "🐱", color: "#6b46c1" };
  }
  if (p === "mot-me-banh" || n.includes("banh")) {
    return { id: "mot-me-banh", name: "Mọt Mê Bánh", badgeClass: "badge-warn", icon: "🥖", color: "#d97706" };
  }
  if (p === "livekit" || n.includes("livekit")) {
    return { id: "livekit", name: "LiveKit", badgeClass: "badge-info", icon: "📹", color: "#2563eb" };
  }
  if (p === "beatsync" || n.includes("beat") || n.includes("sync")) {
    return { id: "beatsync", name: "BeatSync", badgeClass: "badge-info", icon: "🎵", color: "#0891b2" };
  }
  return { id: "other", name: project || "Dự án khác", badgeClass: "badge-neutral", icon: "📦", color: "#475569" };
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

/* ─────────────────────── Container Card ──────────────────────────── */
function ContainerCard({ container, vi }: { container: VpsContainer; vi: boolean }) {
  const [open, setOpen] = useState(false);
  const running = container.state === "running" && container.health !== "unhealthy";
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  const publishedPorts = container.ports?.filter(p => p.hostPort !== null) ?? [];
  const meta = getProjectMeta(container.project, container.name);

  return (
    <div className="vps-container-card" data-state={running ? "ok" : "warn"}>
      <button className="vps-container-header" onClick={() => setOpen(o => !o)} aria-expanded={open} type="button">
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
          {container.cpuPercent !== null ? `${container.cpuPercent.toFixed(1)}% CPU` : ""}
          {container.memoryUsage ? ` · ${container.memoryUsage}` : ""}
        </span>
        <span className="vps-container-chevron"><IconChevron open={open} /></span>
      </button>
      {open && (
        <div className="vps-container-body">
          <div className="vps-container-detail-grid">
            <div><span className="vps-detail-label">{label("Dự án", "Project")}</span><span className="vps-detail-value"><strong>{meta.icon} {meta.name}</strong></span></div>
            <div><span className="vps-detail-label">Image</span><code className="vps-detail-value">{container.image}</code></div>
            <div><span className="vps-detail-label">Status</span><span className="vps-detail-value">{container.status}</span></div>
            {container.cpuPercent !== null && <div><span className="vps-detail-label">CPU</span><span className="vps-detail-value">{container.cpuPercent.toFixed(2)}%</span></div>}
            {container.memoryUsage && <div><span className="vps-detail-label">RAM</span><span className="vps-detail-value">{container.memoryUsage}{container.memoryPercent !== null ? ` (${container.memoryPercent.toFixed(1)}%)` : ""}</span></div>}
          </div>
          {publishedPorts.length > 0 && (
            <div className="vps-container-ports">
              <span className="vps-detail-label"><IconPort /> {label("Cổng công bố", "Published ports")}</span>
              {publishedPorts.map((port, i) => <code key={i}>{port.hostIp}:{port.hostPort} → {port.containerPort}/{port.protocol}</code>)}
            </div>
          )}
          {container.listeningPorts && container.listeningPorts.length > 0 && (
            <div className="vps-container-ports">
              <span className="vps-detail-label">{label("Đang nghe trong container", "Listening in container")}</span>
              {container.listeningPorts.map((port, i) => <code key={i}>{port.address}:{port.port}/{port.protocol}</code>)}
            </div>
          )}
        </div>
      )}
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
      if (!matchAddr && !matchPort && !matchProto && !matchProc) return false;
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
        <div className="vps-gauge-card">
          <div className="vps-gauge-header"><IconCpu /><span>CPU</span></div>
          <DonutGauge percent={snapshot.cpuPercent} color={gaugeColor(snapshot.cpuPercent)} />
        </div>
        <div className="vps-gauge-card">
          <div className="vps-gauge-header"><IconRam /><span>RAM</span></div>
          <DonutGauge percent={snapshot.ramPercent} color={gaugeColor(snapshot.ramPercent)} />
          <small className="vps-gauge-detail">{bytes(snapshot.memoryUsedBytes)} / {bytes(snapshot.memoryTotalBytes)}</small>
        </div>
        <div className="vps-gauge-card">
          <div className="vps-gauge-header"><IconDisk /><span>{label("Ổ đĩa", "Disk")}</span></div>
          <DonutGauge percent={snapshot.diskPercent} color={gaugeColor(snapshot.diskPercent)} />
          <small className="vps-gauge-detail">{bytes(snapshot.diskUsedBytes)} / {bytes(snapshot.diskTotalBytes)}</small>
        </div>
        <div className="vps-gauge-card">
          <div className="vps-gauge-header"><IconDocker /><span>Docker</span></div>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 6, flex: 1, justifyContent: "center" }}>
            <strong style={{ fontSize: 28, fontWeight: 650, letterSpacing: "-.025em", fontVariantNumeric: "tabular-nums", color: snapshot.runtime.dockerAvailable ? "var(--accent)" : "var(--danger)" }}>
              {snapshot.runtime.dockerAvailable ? label("Hoạt động", "Active") : label("Chưa đọc được", "Unavailable")}
            </strong>
            <span className={`badge ${runningContainers === totalContainers ? "badge-success" : "badge-warn"}`}>
              {runningContainers} / {totalContainers} container {label("đang chạy", "running")}
            </span>
          </div>
        </div>
      </section>

      {snapshot.runtime.dockerError ? <p role="alert" style={{ color: "var(--danger)", margin: "0 0 16px", fontSize: 13 }}>{snapshot.runtime.dockerError}</p> : null}
      {snapshot.warnings.length ? <div className="panel" role="alert"><ul>{snapshot.warnings.map((warning, index) => <li key={index}>{warning}</li>)}</ul></div> : null}

      {/* ── Storage Breakdown by Repo & Service ── */}
      <section className="panel" style={{ marginBottom: 20 }}>
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
          <span className="badge badge-info">
            {bytes(snapshot.diskUsedBytes)} / {bytes(snapshot.diskTotalBytes)} ({snapshot.diskPercent}%)
          </span>
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
                        <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                          <strong style={{ fontSize: 13 }}>{item.name}</strong>
                          <code style={{ fontSize: 11, color: "var(--muted)", background: "none", padding: 0 }}>{item.path}</code>
                        </div>
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

      {/* ── Services ── */}
      <section className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#edf3fa", color: "#335c8a" }}><IconService /></span>
            <h2 style={{ margin: 0 }}>{label("Dịch vụ trên VPS", "Host services")}</h2>
          </div>
          <span className="badge badge-info">{snapshot.runtime.services.length} {label("dịch vụ", "services")}</span>
        </div>
        <div className="vps-table"><table><thead><tr>
          <th>{label("Dịch vụ", "Service")}</th>
          <th>{t("status")}</th>
          <th>{label("Khởi động cùng VPS", "Startup")}</th>
        </tr></thead>
          <tbody>{snapshot.runtime.services.map(service => <tr key={service.name}>
            <td style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <IconService />
              {service.name}
            </td>
            <td><span className={`badge ${service.activeState === "active" ? "badge-success" : "badge-neutral"}`}>
              {service.activeState === "active" ? <IconCheck /> : <IconX />}
              {service.activeState}
            </span></td>
            <td>
              <span className={`badge ${service.startupState === "enabled" ? "badge-info" : "badge-neutral"}`}>
                {service.startupState}
              </span>
            </td>
          </tr>)}</tbody>
        </table></div>
      </section>

      {/* ── Docker Containers with Project Filters ── */}
      <section className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#edf5f1", color: "var(--accent)" }}><IconDocker /></span>
            <div>
              <h2 style={{ margin: 0 }}>{label("Docker · Các dự án trên VPS", "Docker · Host Projects & Containers")}</h2>
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 12 }}>{label("Cổng công bố là cổng trên VPS; cổng EXPOSE chỉ là khai báo của image.", "Published ports belong to the host; EXPOSE ports are image metadata.")}</p>
            </div>
          </div>
          <span className={`badge ${runningContainers === totalContainers ? "badge-success" : "badge-warn"}`}>
            {runningContainers}/{totalContainers} {label("đang chạy", "running")}
          </span>
        </div>

        {/* Project Filter Pills */}
        <div className="vps-filter-pills" role="tablist" aria-label={label("Lọc theo dự án", "Filter by project")}>
          {projectGroups.map(g => (
            <button
              key={g.id}
              type="button"
              className="vps-filter-pill"
              data-active={selectedProject === g.id}
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

      {/* ── Host Listening Ports (TCP / UDP Accordion) ── */}
      <section className="panel">
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#fbf3e5", color: "var(--warn)" }}><IconPort /></span>
            <div>
              <h2 style={{ margin: 0 }}>{label("Cổng đang nghe trên VPS", "Host listening ports")}</h2>
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 12 }}>
                {label(
                  `Phân tách ${totalTcpCount} cổng TCP và ${totalUdpCount} cổng UDP (bấm tiêu đề để thu gọn / mở rộng)`,
                  `Separated into ${totalTcpCount} TCP and ${totalUdpCount} UDP ports (click header to collapse / expand)`
                )}
              </p>
            </div>
          </div>
          <span className="badge badge-info">
            {allListeningPorts.length} {label("cổng tổng cộng", "total ports")}
          </span>
        </div>

        {/* Toolbar: filter pill tabs + live search + expand/collapse all */}
        <div className="vps-port-toolbar" style={{ marginTop: 14 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
            <button
              type="button"
              className="vps-port-btn-sm"
              style={{
                background: portProtoFilter === "all" ? "var(--ink)" : undefined,
                color: portProtoFilter === "all" ? "#fff" : undefined,
                borderColor: portProtoFilter === "all" ? "var(--ink)" : undefined,
              }}
              onClick={() => setPortProtoFilter("all")}
            >
              {label("Tất cả", "All")} ({allListeningPorts.length})
            </button>
            <button
              type="button"
              className="vps-port-btn-sm"
              style={{
                background: portProtoFilter === "tcp" ? "var(--accent)" : undefined,
                color: portProtoFilter === "tcp" ? "#fff" : undefined,
                borderColor: portProtoFilter === "tcp" ? "var(--accent)" : undefined,
              }}
              onClick={() => { setPortProtoFilter("tcp"); setTcpOpen(true); }}
            >
              🟢 TCP ({totalTcpCount})
            </button>
            <button
              type="button"
              className="vps-port-btn-sm"
              style={{
                background: portProtoFilter === "udp" ? "var(--warn)" : undefined,
                color: portProtoFilter === "udp" ? "#fff" : undefined,
                borderColor: portProtoFilter === "udp" ? "var(--warn)" : undefined,
              }}
              onClick={() => { setPortProtoFilter("udp"); setUdpOpen(true); }}
            >
              🟠 UDP ({totalUdpCount})
            </button>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <div className="vps-port-search-box">
              <IconSearch />
              <input
                type="text"
                className="vps-port-search-input"
                placeholder={label("Tìm cổng, dịch vụ, IP...", "Search port, process, IP...")}
                value={portSearch}
                onChange={e => setPortSearch(e.target.value)}
              />
              {portSearch ? (
                <button
                  type="button"
                  onClick={() => setPortSearch("")}
                  style={{ background: "none", border: 0, cursor: "pointer", color: "var(--muted)", padding: 0, fontSize: 12 }}
                >
                  ✕
                </button>
              ) : null}
            </div>

            <div className="vps-port-actions">
              <button
                type="button"
                className="vps-port-btn-sm"
                onClick={() => { setTcpOpen(true); setUdpOpen(true); }}
                title={label("Mở rộng cả 2 danh sách", "Expand both lists")}
              >
                ⊞ {label("Mở rộng tất cả", "Expand all")}
              </button>
              <button
                type="button"
                className="vps-port-btn-sm"
                onClick={() => { setTcpOpen(false); setUdpOpen(false); }}
                title={label("Thu gọn cả 2 danh sách", "Collapse both lists")}
              >
                ⊟ {label("Thu gọn tất cả", "Collapse all")}
              </button>
            </div>
          </div>
        </div>

        {/* ── TCP Ports Group ── */}
        {(portProtoFilter === "all" || portProtoFilter === "tcp") && (
          <div className="vps-port-group">
            <button
              type="button"
              className="vps-port-group-header"
              onClick={() => setTcpOpen(prev => !prev)}
              aria-expanded={tcpOpen}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <IconChevron open={tcpOpen} />
                <div>
                  <strong style={{ fontSize: 14 }}>
                    🟢 TCP ({tcpPorts.length}{searchLower ? `/${totalTcpCount}` : ""})
                  </strong>
                  <span style={{ marginLeft: 8, fontSize: 12, color: "var(--muted)" }}>
                    {label("Các dịch vụ chính: SSH, Nginx, PostgreSQL, Reverse Proxies...", "Main services: SSH, Nginx, PostgreSQL, Reverse Proxies...")}
                  </span>
                </div>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span className="badge badge-success">
                  {tcpPorts.length} {label("cổng", "ports")}
                </span>
                <span style={{ fontSize: 12, color: "var(--muted)" }}>
                  {tcpOpen ? label("Thu gọn ▲", "Collapse ▲") : label("Mở rộng ▼", "Expand ▼")}
                </span>
              </div>
            </button>

            {tcpOpen ? (
              <div className="vps-port-group-body">
                {tcpPorts.length > 0 ? (
                  <div className="vps-table">
                    <table>
                      <thead>
                        <tr>
                          <th>{label("Địa chỉ", "Address")}</th>
                          <th>{label("Cổng", "Port")}</th>
                          <th>{label("Giao thức", "Protocol")}</th>
                          <th>{label("Dịch vụ", "Process")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {tcpPorts.map((port, index) => (
                          <tr key={`tcp-${port.address}-${port.port}-${index}`}>
                            <td><code>{port.address}</code></td>
                            <td><strong style={{ fontVariantNumeric: "tabular-nums" }}>{port.port}</strong></td>
                            <td><span className="badge badge-success">TCP</span></td>
                            <td>
                              {port.process ? (
                                <span style={{ fontWeight: 550, color: "var(--ink)" }}>{port.process}</span>
                              ) : (
                                <span style={{ color: "var(--muted)" }}>—</span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p style={{ padding: "16px 20px", color: "var(--muted)", margin: 0, fontSize: 13 }}>
                    {label("Không tìm thấy cổng TCP nào phù hợp bộ lọc.", "No TCP ports match the filter.")}
                  </p>
                )}
              </div>
            ) : (
              <div
                className="vps-port-collapsed-hint"
                onClick={() => setTcpOpen(true)}
                role="button"
                tabIndex={0}
                onKeyDown={e => { if (e.key === "Enter" || e.key === " ") setTcpOpen(true); }}
              >
                <span>
                  🟢 <strong>{tcpPorts.length} cổng TCP</strong> {label("đang lắng nghe kết nối", "actively listening")}
                </span>
                <span style={{ fontWeight: 550, color: "var(--accent)" }}>
                  {label("Bấm để xem danh sách chi tiết →", "Click to view details →")}
                </span>
              </div>
            )}
          </div>
        )}

        {/* ── UDP Ports Group ── */}
        {(portProtoFilter === "all" || portProtoFilter === "udp") && (
          <div className="vps-port-group">
            <button
              type="button"
              className="vps-port-group-header"
              onClick={() => setUdpOpen(prev => !prev)}
              aria-expanded={udpOpen}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <IconChevron open={udpOpen} />
                <div>
                  <strong style={{ fontSize: 14 }}>
                    🟠 UDP ({udpPorts.length}{searchLower ? `/${totalUdpCount}` : ""})
                  </strong>
                  <span style={{ marginLeft: 8, fontSize: 12, color: "var(--muted)" }}>
                    {label("Chủ yếu là LiveKit WebRTC (dải cổng 50000–50100) & dịch vụ DNS", "Mostly LiveKit WebRTC (range 50000–50100) & DNS services")}
                  </span>
                </div>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span className="badge badge-warn">
                  {udpPorts.length} {label("cổng", "ports")}
                </span>
                <span style={{ fontSize: 12, color: "var(--muted)" }}>
                  {udpOpen ? label("Thu gọn ▲", "Collapse ▲") : label("Mở rộng ▼", "Expand ▼")}
                </span>
              </div>
            </button>

            {udpOpen ? (
              <div className="vps-port-group-body">
                {udpPorts.length > 0 ? (
                  <div className="vps-table">
                    <table>
                      <thead>
                        <tr>
                          <th>{label("Địa chỉ", "Address")}</th>
                          <th>{label("Cổng", "Port")}</th>
                          <th>{label("Giao thức", "Protocol")}</th>
                          <th>{label("Dịch vụ", "Process")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {udpPorts.map((port, index) => (
                          <tr key={`udp-${port.address}-${port.port}-${index}`}>
                            <td><code>{port.address}</code></td>
                            <td><strong style={{ fontVariantNumeric: "tabular-nums" }}>{port.port}</strong></td>
                            <td><span className="badge badge-warn">UDP</span></td>
                            <td>
                              {port.process ? (
                                <span style={{ fontWeight: 550, color: "var(--ink)" }}>{port.process}</span>
                              ) : (
                                <span style={{ color: "var(--muted)" }}>—</span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p style={{ padding: "16px 20px", color: "var(--muted)", margin: 0, fontSize: 13 }}>
                    {label("Không tìm thấy cổng UDP nào phù hợp bộ lọc.", "No UDP ports match the filter.")}
                  </p>
                )}
              </div>
            ) : (
              <div
                className="vps-port-collapsed-hint"
                onClick={() => setUdpOpen(true)}
                role="button"
                tabIndex={0}
                onKeyDown={e => { if (e.key === "Enter" || e.key === " ") setUdpOpen(true); }}
              >
                <span>
                  🟠 <strong>{udpPorts.length} cổng UDP</strong> {label("đang mở (chủ yếu thuộc LiveKit WebRTC / docker-proxy: dải 50000–50100)", "open (mostly LiveKit WebRTC / docker-proxy: range 50000–50100)")}
                </span>
                <span style={{ fontWeight: 550, color: "var(--accent)" }}>
                  {label("Bấm để mở rộng danh sách →", "Click to expand list →")}
                </span>
              </div>
            )}
          </div>
        )}
      </section>
    </> : null}
  </>;
}
