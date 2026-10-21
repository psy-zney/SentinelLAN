"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import type { VpsHostStatus } from "@/types/api";
import { PlatformSessionExpired, vpsApi } from "../lib/api";
import { startVisiblePolling } from "../lib/visible-poller";
import { listeningPorts, localAddress } from "../lib/vps-ports";
import { ContainerPorts } from "./container-ports";

const gb = (bytes: number) => `${(bytes / 1024 ** 3).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} GB`;
const serviceState = (state: string) => ({ active: "Đang chạy", inactive: "Đã dừng", failed: "Lỗi", unknown: "Chưa xác định" }[state] ?? state);
const startup = (state: string) => ({ enabled: "Tự chạy", disabled: "Không tự chạy", static: "Theo dịch vụ phụ thuộc", masked: "Bị chặn", "not-found": "Chưa cài" }[state] ?? state);
const containerState = (state: string) => ({ running: "Đang chạy", exited: "Đã dừng", created: "Chưa chạy", restarting: "Đang khởi động lại", paused: "Tạm dừng", dead: "Lỗi" }[state] ?? state);

export function HostVpsMonitor() {
  const router = useRouter();
  const [status, setStatus] = useState<VpsHostStatus | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [now, setNow] = useState(() => Date.now());
  const lock = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const check = useCallback(async (signal?: AbortSignal) => {
    if (lock.current || signal?.aborted) return;
    lock.current = true; setBusy(true);
    try {
      const result = await vpsApi.hostStatus(signal);
      if (!signal?.aborted) { setStatus(result); setError(""); setNow(Date.now()); }
    } catch (cause) {
      if (!signal?.aborted) {
        if (cause instanceof PlatformSessionExpired) router.replace("/login");
        else setError(cause instanceof Error ? cause.message : "Chưa đọc được trạng thái VPS.");
      }
    } finally { lock.current = false; if (!signal?.aborted) setBusy(false); }
  }, [router]);
  useEffect(() => {
    const active = new AbortController(); controller.current = active;
    const stop = startVisiblePolling(() => check(active.signal), 1);
    const clock = setInterval(() => { if (!document.hidden) setNow(Date.now()); }, 15_000);
    return () => { stop(); clearInterval(clock); active.abort(); controller.current = null; };
  }, [check]);

  const snapshot = status?.snapshot;
  const stale = status?.stale || (snapshot && now - Date.parse(snapshot.capturedAtUtc) > 180_000);
  const containers = snapshot?.runtime.containers ?? [];
  const visible = containers.filter(container => `${container.name} ${container.image}`.toLowerCase().includes(search.toLowerCase()) &&
    (filter === "all" || (filter === "running" ? container.state === "running" : container.state !== "running")));
  const running = containers.filter(container => container.state === "running").length;
  const ports = listeningPorts(snapshot?.listeningPorts ?? []);

  return <section className="host-monitor card" aria-labelledby="host-monitor-heading">
    <div className="card-header"><div><h2 id="host-monitor-heading">VPS đang chạy SentinelLAN</h2>
      <p>Tự kiểm tra mỗi phút. Không cần thêm lại máy chủ này bằng SSH.</p></div>
      <button type="button" className="secondary" disabled={busy} onClick={() => { void check(controller.current?.signal); }}>{busy ? "Đang kiểm tra…" : "Kiểm tra VPS"}</button>
    </div>
    {error && <p className="error" role="alert">{error} {snapshot && "Dữ liệu dưới đây là lần kiểm tra gần nhất."}</p>}
    {!status && !error && <p role="status">Đang đọc trạng thái VPS…</p>}
    {status && !snapshot && <p role="status">{status.message}</p>}
    {snapshot && <>
      <div className="host-identity"><div><h3>{snapshot.name}</h3><p><code>{snapshot.host}</code> · {snapshot.osInfo}</p></div>
        <span className={`badge ${stale || error ? "badge-warning" : "badge-success"}`}>{stale || error ? "Chưa có dữ liệu mới" : "Đã kiểm tra"}</span>
      </div>
      <p className="muted">Lần kiểm tra: {new Date(snapshot.capturedAtUtc).toLocaleString("vi-VN")} · Đã chạy {Math.floor(snapshot.uptimeSeconds / 86400)} ngày {Math.floor(snapshot.uptimeSeconds % 86400 / 3600)} giờ</p>
      {stale && <p className="notice" role="status">{status?.stale ? status.message : "Đã quá 3 phút chưa có dữ liệu mới. Kiểm tra bộ thu thập trên VPS."}</p>}
      <div className="vps-metrics host-metrics">{[
        { label: "CPU đang dùng", value: snapshot.cpuPercent, detail: "Toàn bộ VPS" },
        { label: "RAM đang dùng", value: snapshot.ramPercent, detail: `${gb(snapshot.memoryUsedBytes)} / ${gb(snapshot.memoryTotalBytes)}` },
        { label: "Ổ đĩa đang dùng", value: snapshot.diskPercent, detail: `${gb(snapshot.diskUsedBytes)} / ${gb(snapshot.diskTotalBytes)}` }
      ].map(metric => <div key={metric.label}><span>{metric.label}</span><strong>{metric.value}%</strong>
        <progress max="100" value={metric.value} aria-label={metric.label} /><small>{metric.detail}</small></div>)}</div>
      <div className="card-header host-docker-heading"><div><h3>Docker trên VPS</h3>
        <p>{snapshot.runtime.dockerAvailable ? `${running} đang chạy · ${containers.length - running} đã dừng hoặc đang chuyển trạng thái` : "Chưa truy cập được Docker"}</p></div>
        <div className="host-filters"><label>Tìm Docker<input value={search} onChange={event => setSearch(event.target.value)} placeholder="Tên hoặc image" /></label>
          <label>Trạng thái<select value={filter} onChange={event => setFilter(event.target.value)}><option value="all">Tất cả</option><option value="running">Đang chạy</option><option value="stopped">Khác</option></select></label></div>
      </div>
      {snapshot.runtime.dockerError && <p className="notice" role="status">{snapshot.runtime.dockerError}</p>}
      {snapshot.warnings.map((warning, index) => <p key={index} className="notice">{warning}</p>)}
      {snapshot.runtime.dockerAvailable && <div className="table-wrap host-docker-table"><table><caption className="sr-only">Docker và cổng trên {snapshot.name}</caption>
        <thead><tr><th>Docker</th><th>Trạng thái</th><th>Cổng</th><th>CPU / RAM</th><th>Tự chạy lại</th></tr></thead>
        <tbody>{visible.length ? visible.map(container => <tr key={container.id}>
          <td data-label="Docker"><strong>{container.name}</strong><small>{container.image.split("@sha256:")[0]}</small><details><summary>Tài nguyên khác</summary>
            <small>Image đầy đủ: {container.image}</small><small>Ổ đĩa: {container.storageUsage ?? "Chưa có số liệu"}</small><small>Mạng: {container.networkIo ?? "Chưa có số liệu"}</small><small>Đọc / ghi: {container.blockIo ?? "Chưa có số liệu"}</small></details></td>
          <td data-label="Trạng thái"><span className={`badge ${container.state === "running" ? "badge-success" : "badge-warning"}`}>{containerState(container.state)}</span>
            {container.health && <small>{container.health === "healthy" ? "Kiểm tra sức khỏe: tốt" : container.health === "unhealthy" ? "Kiểm tra sức khỏe: lỗi" : "Đang kiểm tra sức khỏe"}</small>}</td>
          <td data-label="Cổng"><ContainerPorts container={container} /></td><td data-label="CPU / RAM">{container.cpuPercent == null ? "—" : `${container.cpuPercent}%`}<small>{container.memoryUsage ?? "—"}</small></td>
          <td data-label="Tự chạy lại">{({ "unless-stopped": "Có, trừ khi dừng thủ công", always: "Luôn tự chạy", "on-failure": "Khi bị lỗi", no: "Không" }[container.restartPolicy ?? ""] ?? "Chưa có thông tin")}</td>
        </tr>) : <tr><td colSpan={5}>{containers.length ? "Không có Docker phù hợp bộ lọc." : "Chưa có Docker nào."}</td></tr>}</tbody>
      </table></div>}
      <details className="host-details" open><summary>Cổng đang nghe trên VPS ({ports.length} dòng)</summary>
        <p className="muted">Cổng công bố dùng định dạng địa chỉ VPS:cổng → cổng container. Tường lửa quyết định có truy cập được từ Internet hay không.</p>
        <div className="table-wrap"><table><thead><tr><th>Cổng</th><th>Địa chỉ</th><th>Tiến trình</th><th>Phạm vi lắng nghe</th></tr></thead>
          <tbody>{ports.map(port => <tr key={`${port.address}:${port.port}/${port.protocol}`}><td><code>{port.port}{port.endPort !== port.port ? `–${port.endPort}` : ""}/{port.protocol.toUpperCase()}</code></td>
            <td><code>{port.address}</code></td><td>{port.process ?? "Chưa xác định"}</td><td>{localAddress(port.address) ? "Chỉ trên VPS" : "Địa chỉ mạng của VPS"}</td></tr>)}</tbody></table></div>
      </details>
      <details className="host-details"><summary>Dịch vụ trên VPS</summary><div className="table-wrap"><table>
        <thead><tr><th>Dịch vụ</th><th>Trạng thái</th><th>Khi bật VPS</th></tr></thead><tbody>{snapshot.runtime.services.map(service =>
          <tr key={service.name}><td>{service.name}</td><td>{serviceState(service.activeState)}</td><td>{startup(service.startupState)}</td></tr>)}</tbody>
      </table></div></details>
    </>}
  </section>;
}
