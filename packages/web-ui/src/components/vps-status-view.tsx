"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { VpsHostStatus, VpsContainer } from "@/types/host-status";

function bytes(value: number) {
  return `${(value / 1024 ** 3).toFixed(1)} GiB`;
}

/* ──────────────────────────── SVG Icons ──────────────────────────── */
const svgProps = { width: 18, height: 18, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: 1.8, strokeLinecap: "round" as const, strokeLinejoin: "round" as const, "aria-hidden": true as const };

function IconCpu() { return <svg {...svgProps}><rect x="4" y="4" width="16" height="16" rx="2" /><path d="M9 1v3M15 1v3M9 20v3M15 20v3M1 9h3M1 15h3M20 9h3M20 15h3" /><path d="M9 9h6v6H9z" /></svg>; }
function IconRam() { return <svg {...svgProps}><rect x="2" y="6" width="20" height="12" rx="2" /><path d="M6 6V4M10 6V4M14 6V4M18 6V4M6 18v2M10 18v2M14 18v2M18 18v2" /><path d="M6 10h2v4H6zM10 10h2v4h-2zM14 10h2v4h-2z" fill="currentColor" opacity=".15" /></svg>; }
function IconDisk() { return <svg {...svgProps}><circle cx="12" cy="12" r="10" /><circle cx="12" cy="12" r="3" /><path d="M12 2v7" /></svg>; }
function IconDocker() { return <svg {...svgProps}><path d="M22 12.5c-.5-2-2.5-3-4-2.5 0-2-1.5-3.5-3.5-3.5-.5 0-1 .1-1.5.3C12 5.8 10.5 5 9 5 6 5 3.5 7.5 3.5 10.5c0 .3 0 .7.1 1C2 12.5 1 14.5 2 16.5S5.5 19 7 19h13c2 0 3.5-1.5 3.5-3.5 0-1.5-1-2.5-1.5-3Z" /><path d="M8 11h2v2H8zM11 11h2v2h-2zM14 11h2v2h-2zM11 8h2v2h-2z" fill="currentColor" opacity=".15" /></svg>; }
function IconContainer() { return <svg {...svgProps}><path d="M21 16V8a2 2 0 0 0-1-1.73L13 2.27a2 2 0 0 0-2 0L4 6.27A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" /><polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" /></svg>; }
function IconService() { return <svg {...svgProps}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" /></svg>; }
function IconPort() { return <svg {...svgProps}><circle cx="12" cy="12" r="2" /><path d="M16.24 7.76a6 6 0 0 1 0 8.49M19.07 4.93a10 10 0 0 1 0 14.14M7.76 16.24a6 6 0 0 1 0-8.49M4.93 19.07a10 10 0 0 1 0-14.14" /></svg>; }
function IconRefresh() { return <svg {...svgProps}><polyline points="23 4 23 10 17 10" /><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" /></svg>; }
function IconCheck() { return <svg {...svgProps} width={14} height={14}><polyline points="20 6 9 17 4 12" /></svg>; }
function IconX() { return <svg {...svgProps} width={14} height={14}><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></svg>; }
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

  return (
    <div className="vps-container-card" data-state={running ? "ok" : "warn"}>
      <button className="vps-container-header" onClick={() => setOpen(o => !o)} aria-expanded={open} type="button">
        <span className="vps-container-icon"><IconContainer /></span>
        <span className="vps-container-name">{container.name}</span>
        <span className={`badge ${running ? "badge-success" : "badge-warn"}`}>
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

  const runningContainers = snapshot?.runtime.containers.filter(c => c.state === "running").length ?? 0;
  const totalContainers = snapshot?.runtime.containers.length ?? 0;

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

      {/* ── Docker Containers ── */}
      <section className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#edf5f1", color: "var(--accent)" }}><IconDocker /></span>
            <div>
              <h2 style={{ margin: 0 }}>Docker · SentinelLAN</h2>
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 12 }}>{label("Cổng công bố là cổng trên VPS; cổng EXPOSE chỉ là khai báo của image.", "Published ports belong to the host; EXPOSE ports are image metadata.")}</p>
            </div>
          </div>
          <span className={`badge ${runningContainers === totalContainers ? "badge-success" : "badge-warn"}`}>
            {runningContainers}/{totalContainers} {label("đang chạy", "running")}
          </span>
        </div>
        <div className="vps-containers-grid">
          {snapshot.runtime.containers.map(container => (
            <ContainerCard key={container.id} container={container} vi={vi} />
          ))}
        </div>
      </section>

      {/* ── Host Listening Ports ── */}
      <section className="panel">
        <div className="panel-head">
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 6, background: "#fbf3e5", color: "var(--warn)" }}><IconPort /></span>
            <h2 style={{ margin: 0 }}>{label("Cổng đang nghe trên VPS", "Host listening ports")}</h2>
          </div>
          <span className="badge badge-info">{snapshot.listeningPorts.length} {label("cổng", "ports")}</span>
        </div>
        <div className="vps-table"><table><thead><tr>
          <th>{label("Địa chỉ", "Address")}</th>
          <th>{label("Cổng", "Port")}</th>
          <th>{label("Giao thức", "Protocol")}</th>
          <th>{label("Dịch vụ", "Process")}</th>
        </tr></thead>
          <tbody>{snapshot.listeningPorts.map((port, index) => <tr key={index}>
            <td><code>{port.address}</code></td>
            <td>{port.port}</td>
            <td><span className="badge badge-neutral">{port.protocol}</span></td>
            <td>{port.process ?? "—"}</td>
          </tr>)}</tbody>
        </table></div>
      </section>
    </> : null}
  </>;
}
