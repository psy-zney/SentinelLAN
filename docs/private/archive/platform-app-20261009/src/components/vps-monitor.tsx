"use client";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import type { VpsNode } from "@/types/api";
import { PlatformSessionExpired, vpsApi, type VpsAction } from "../lib/api";
import { startVisiblePolling } from "../lib/visible-poller";
import { ContainerPorts } from "./container-ports";

type Operation = { node: VpsNode; action: VpsAction | "RestartService"; target: string | null; value: string | null; label: string };
const percentage = (value?: number | null) => value == null ? "Chưa có số liệu" : `${value}%`;
const serviceLabel = (state: string) => ({ active: "Đang chạy", inactive: "Đã dừng", failed: "Lỗi", unknown: "Chưa xác định" }[state] ?? state);
const startupLabel = (state: string) => ({ enabled: "Tự chạy", disabled: "Không tự chạy", static: "Theo dịch vụ phụ thuộc", masked: "Bị chặn", "not-found": "Chưa cài" }[state] ?? state);

function Dialog({ title, busy, close, children }: { title: string; busy: boolean; close: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const previous = document.activeElement;
    ref.current?.showModal();
    return () => { if (previous instanceof HTMLElement) previous.focus(); };
  }, []);
  return <dialog ref={ref} className="vps-dialog" aria-label={title} onCancel={event => { event.preventDefault(); if (!busy) close(); }}>
    <div className="card-header"><h3>{title}</h3><button type="button" className="secondary" disabled={busy} onClick={close} aria-label="Đóng">Đóng</button></div>
    {children}
  </dialog>;
}

export function VpsMonitor({ refreshBackend }: { refreshBackend: () => Promise<void> }) {
  const router = useRouter();
  const [nodes, setNodes] = useState<VpsNode[] | null>(null);
  const [minutes, setMinutes] = useState(3);
  const [now, setNow] = useState(() => Date.now());
  const [working, setWorking] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [adding, setAdding] = useState(false);
  const [operation, setOperation] = useState<Operation | null>(null);
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [policy, setPolicy] = useState("unless-stopped");
  const lock = useRef(false);
  const activeController = useRef<AbortController | null>(null);

  const reportError = useCallback((cause: unknown) => {
    if (cause instanceof PlatformSessionExpired) router.replace("/login");
    else setError(cause instanceof Error ? cause.message : "Chưa kết nối được hệ thống. Hãy thử lại.");
  }, [router]);

  const check = useCallback(async (id?: string, signal?: AbortSignal) => {
    if (lock.current || signal?.aborted) return;
    lock.current = true;
    setWorking(true);
    setError("");
    try {
      if (id) {
        const result = await vpsApi.refresh(id, signal);
        if (!signal?.aborted) setNodes(current => current?.map(node => node.id === id ? result : node) ?? [result]);
      } else {
        const list = await vpsApi.list(signal);
        if (signal?.aborted) return;
        // Each row shows the time of its own sample. At most two VPS probes run together.
        setNodes(current => list.map(node => ({ ...node, runtime: current?.find(old => old.id === node.id && old.lastCheckedAt === node.lastCheckedAt)?.runtime })));
        let cursor = 0;
        const worker = async () => {
          while (cursor < list.length && !signal?.aborted && !document.hidden) {
            const node = list[cursor++];
            try {
              const result = await vpsApi.refresh(node.id, signal);
              if (!signal?.aborted) setNodes(current => current?.map(old => old.id === node.id ? result : old) ?? [result]);
            } catch (cause) { if (!signal?.aborted) reportError(cause); }
          }
        };
        await Promise.all([worker(), worker()]);
      }
      if (!signal?.aborted) await refreshBackend();
    } catch (cause) { if (!signal?.aborted) reportError(cause); }
    finally { lock.current = false; if (!signal?.aborted) setWorking(false); }
  }, [refreshBackend, reportError]);

  useEffect(() => {
    const timer = setInterval(() => { if (!document.hidden) setNow(Date.now()); }, 15_000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    activeController.current = controller;
    const stop = startVisiblePolling(() => check(undefined, controller.signal), minutes);
    return () => { stop(); controller.abort(); };
  }, [check, minutes]);

  async function addNode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current) return;
    const form = event.currentTarget;
    const data = new FormData(form);
    // Remove key text from the visible form as soon as the request is prepared.
    form.reset();
    lock.current = true;
    setWorking(true); setError(""); setNotice("");
    try {
      const node = await vpsApi.create({ name: String(data.get("name")).trim(), host: String(data.get("host")).trim(), port: Number(data.get("port")), username: String(data.get("username")).trim(), privateKey: String(data.get("privateKey")).trim(), hostKeyFingerprint: String(data.get("fingerprint")).trim() });
      setNodes(current => [...(current ?? []), node]);
      setAdding(false);
      if (node.status === "Online") setNotice("Đã lưu VPS và xác minh kết nối SSH.");
      else setError(`Đã lưu VPS. Kết nối SSH chưa thành công: ${node.errorMessage ?? "Hãy kiểm tra lại."}`);
    } catch (cause) { reportError(cause); }
    finally { lock.current = false; setWorking(false); }
  }

  function openOperation(next: Operation) {
    setOperation(next); setReason(""); setConfirmed(false); setPolicy(next.value && ["no", "always", "unless-stopped"].includes(next.value) ? next.value : "unless-stopped"); setError(""); setNotice("");
  }

  async function execute(event: React.FormEvent) {
    event.preventDefault();
    if (!operation || lock.current || !confirmed || reason.trim().length < 3) return;
    lock.current = true; setWorking(true); setError(""); setNotice("");
    let succeeded = false;
    try {
      const result = operation.action === "RestartService"
        ? await vpsApi.restart(operation.node.id, operation.target!, reason.trim())
        : await vpsApi.operate(operation.node.id, operation.action, operation.target, operation.action === "SetContainerRestartPolicy" ? policy : null, reason.trim());
      if (result.success) { setNotice(result.message); setOperation(null); succeeded = true; }
      else setError(result.message);
    } catch (cause) { reportError(cause); }
    finally { lock.current = false; setWorking(false); }
    if (succeeded && operation.action !== "Reboot") await check(operation.node.id, activeController.current?.signal);
  }

  const selectedCommand = operation?.action === "Reboot" ? "sudo -n shutdown -r +1"
    : operation?.action === "SetContainerRestartPolicy" ? `sudo -n docker update --restart=${policy} ${operation.target}`
    : operation ? `sudo -n systemctl ${operation.action === "EnableServiceStartup" ? "enable" : operation.action === "DisableServiceStartup" ? "disable" : "restart"} ${operation.target}` : "";

  return <section className="vps-monitor" aria-label="Quản lý VPS qua SSH" aria-busy={working}>
    <div className="card-header">
      <div><h3>Máy chủ đã kết nối</h3><p>Tự cập nhật khi bạn đang xem trang này.</p></div>
      <div className="vps-actions">
        <label htmlFor="vps-interval">Cập nhật mỗi</label>
        <select id="vps-interval" value={minutes} disabled={working} onChange={event => setMinutes(Number(event.target.value))}>
          {[1, 2, 3, 4, 5].map(value => <option key={value} value={value}>{value} phút</option>)}
        </select>
        <button type="button" className="secondary" disabled={working} onClick={() => void check(undefined, activeController.current?.signal)}>{working ? "Đang kiểm tra…" : "Kiểm tra lại"}</button>
        <button type="button" className="primary" disabled={working} onClick={() => setAdding(true)}>Thêm máy chủ</button>
      </div>
    </div>
    {error && <p className="alert-box alert-error" role="alert">{error}</p>}
    {notice && <p className="alert-box alert-success" role="status">{notice}</p>}
    {nodes === null && <p role="status">{error ? "Chưa tải được danh sách VPS. Chọn Kiểm tra lại để thử lại." : "Đang tải danh sách VPS…"}</p>}
    {nodes?.length === 0 && <p>Chưa có máy chủ. Chọn “Thêm máy chủ” để kết nối VPS của bạn.</p>}
    {nodes?.map(node => {
      const runtime = node.runtime;
      const stale = !node.lastCheckedAt || now - new Date(node.lastCheckedAt).getTime() > (minutes + 1) * 60_000 || node.status !== "Online";
      return <article key={node.id} className="card vps-node">
        <div className="card-header">
          <div><h3>{node.name}</h3><code>{node.username}@{node.host}:{node.port}</code></div>
          <div className="vps-actions">
            <span className={`badge ${node.status === "Online" && !stale ? "badge-success" : "badge-warning"}`}>{node.status === "Online" ? "Đang kết nối" : "Chưa kết nối được"}</span>
            <button type="button" className="secondary" disabled={working} onClick={() => void check(node.id, activeController.current?.signal)}>Kiểm tra VPS này</button>
            <button type="button" className="danger" disabled={working} onClick={() => openOperation({ node, action: "Reboot", target: null, value: null, label: "Khởi động lại VPS" })}>Khởi động lại VPS</button>
          </div>
        </div>
        <p className="vps-muted">Lần kiểm tra: {node.lastCheckedAt ? new Date(node.lastCheckedAt).toLocaleString("vi-VN") : "Chưa kiểm tra"} {stale && "· Số liệu cũ hoặc chưa xác minh"}</p>
        {node.errorMessage && <p role="alert" className="alert-box alert-error">{node.errorMessage}</p>}
        <div className="vps-metrics">
          {([["CPU", node.cpuPercent], ["RAM", node.ramPercent], ["Ổ đĩa /", node.diskPercent]] as const).map(([label, value]) => <div key={label}><strong>{label}: {percentage(value)}</strong><progress aria-label={`${label} của ${node.name}`} max={100} value={value ?? 0} /></div>)}
        </div>
        <p>{node.osInfo ?? "Chưa có thông tin hệ điều hành"} · {node.uptime ?? "Chưa có thời gian hoạt động"}</p>
        {runtime ? <>
          <p>Hệ thống: <strong>{runtime.systemState === "running" ? "Hoạt động bình thường" : runtime.systemState === "degraded" ? "Có dịch vụ lỗi" : runtime.systemState}</strong> · Container đang chạy: <strong>{node.dockerContainersCount ?? "Chưa xác định"}</strong></p>
          <div className="table-wrap"><table><caption>Dịch vụ trên máy chủ</caption><thead><tr><th>Dịch vụ</th><th>Hoạt động</th><th>Khi khởi động VPS</th><th>Thao tác</th></tr></thead><tbody>
            {runtime.services.map(service => <tr key={service.name}><td>{service.name}</td><td>{serviceLabel(service.activeState)}</td><td>{startupLabel(service.startupState)}</td><td><div className="vps-actions">
              <button type="button" className="secondary" disabled={working} onClick={() => openOperation({ node, action: "RestartService", target: service.name, value: null, label: "Khởi động lại dịch vụ" })}>Khởi động lại</button>
              <button type="button" className="secondary" disabled={working || !["enabled", "disabled"].includes(service.startupState)} onClick={() => openOperation({ node, action: service.startupState === "enabled" ? "DisableServiceStartup" : "EnableServiceStartup", target: service.name, value: null, label: service.startupState === "enabled" ? "Tắt tự khởi động dịch vụ" : "Bật tự khởi động dịch vụ" })}>{service.startupState === "enabled" ? "Tắt tự chạy" : "Bật tự chạy"}</button>
            </div></td></tr>)}
          </tbody></table></div>
          {runtime.dockerError && <p className="alert-box alert-error" role="alert">{runtime.dockerError}</p>}
          {runtime.dockerAvailable && <>
            <div className="table-wrap"><table><caption>Ứng dụng Docker ({runtime.containers.length})</caption><thead><tr><th>Ứng dụng / bản chạy</th><th>Trạng thái</th><th>Cổng</th><th>CPU / RAM</th><th>Dung lượng sử dụng</th><th>Dữ liệu truyền / đọc ghi</th><th>Tự khởi động</th></tr></thead><tbody>
              {runtime.containers.map(container => <tr key={container.id}><td><strong>{container.name}</strong><br /><small>{container.image}</small></td><td>{container.status}<br />{container.health ?? "Chưa cấu hình healthcheck"}</td><td><ContainerPorts container={container} /></td><td>{percentage(container.cpuPercent)}<br />{container.memoryUsage ?? "—"} ({percentage(container.memoryPercent)})</td><td>{container.storageUsage ?? "—"}</td><td>{container.networkIo ?? "—"}<br />{container.blockIo ?? "—"}</td><td>{container.restartPolicy ?? "Chưa đọc được"}<br /><button type="button" className="secondary" disabled={working} onClick={() => openOperation({ node, action: "SetContainerRestartPolicy", target: container.id, value: container.restartPolicy ?? "unless-stopped", label: `Cấu hình tự chạy: ${container.name}` })}>Cấu hình</button></td></tr>)}
            </tbody></table></div>
            {runtime.containers.length === 0 && !runtime.dockerError && <p>Docker hoạt động, hiện chưa có container.</p>}
            <p className="vps-muted">Dung lượng là lớp ghi của container; phần “virtual” gồm image có thể dùng chung. Chưa bao gồm volume và log. I/O ổ đĩa là lượng dữ liệu đọc/ghi, không phải dung lượng lưu trữ.</p>
          </>}
        </> : <p className="vps-muted">Chi tiết dịch vụ và container xuất hiện sau khi kiểm tra VPS thành công.</p>}
      </article>;
    })}
    {adding && <Dialog title="Thêm máy chủ" busy={working} close={() => setAdding(false)}><form onSubmit={addNode}>
      {error && <p className="alert-box alert-error" role="alert">{error}</p>}
      <label>Tên máy chủ<input name="name" required minLength={2} maxLength={100} autoFocus /></label>
      <div className="grid-2"><label>Địa chỉ IP hoặc tên miền<input name="host" required minLength={3} maxLength={255} /></label><label>Cổng SSH<input name="port" type="number" defaultValue={22} min={1} max={65535} required /></label></div>
      <label>Tài khoản SSH<input name="username" defaultValue="ubuntu" required maxLength={64} /></label>
      <label>Khóa riêng SSH (private key)<textarea name="privateKey" required rows={5} autoComplete="off" spellCheck={false} /></label>
      <label>Mã xác minh máy chủ (SHA256)<input name="fingerprint" required pattern="SHA256:[A-Za-z0-9+/]{43}" placeholder="SHA256:…" /></label>
      <p className="vps-muted">Dán nội dung tệp khóa SSH của tài khoản được phép quản lý VPS. Khóa được lưu mã hóa trên máy chủ SentinelLAN.</p>
      <details className="vps-key-help"><summary>Cách lấy mã xác minh máy chủ</summary><p>Mở bảng điều khiển của nhà cung cấp VPS và chạy:</p><code className="vps-command">ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256</code><p>Sao chép phần bắt đầu bằng SHA256: vào ô bên trên.</p></details>
      <button className="primary" type="submit" disabled={working}>{working ? "Đang kết nối…" : "Lưu và kiểm tra SSH"}</button>
    </form></Dialog>}
    {operation && <Dialog title={operation.label} busy={working} close={() => setOperation(null)}><form onSubmit={execute}>
      {error && <p className="alert-box alert-error" role="alert">{error}</p>}
      <p>VPS: <strong>{operation.node.name}</strong> ({operation.node.host})</p>
      {operation.target && <p>Đối tượng: <code>{operation.target}</code></p>}
      {operation.action === "SetContainerRestartPolicy" && <><label>Cách tự khởi động<select value={policy} onChange={event => setPolicy(event.target.value)}><option value="unless-stopped">Tự chạy, giữ dừng nếu đã dừng thủ công</option><option value="always">Tự chạy sau mỗi lần Docker/VPS khởi động</option><option value="no">Không tự khởi động</option></select></label><p>Cần bật tự khởi động dịch vụ Docker. Container đã dừng không được khởi động ngay bởi thao tác cấu hình này. Với Docker Compose, lưu cùng chính sách trong compose để giữ cấu hình khi tạo lại container.</p></>}
      {operation.action === "Reboot" && <p className="alert-box alert-error">VPS sẽ khởi động lại sau khoảng 1 phút. Các dịch vụ trên máy chủ sẽ tạm gián đoạn, gồm SentinelLAN nếu chạy trên VPS này.</p>}
      <p>Lệnh sẽ chạy: <code className="vps-command">{selectedCommand}</code></p>
      <label>Lý do<textarea required minLength={3} maxLength={1000} value={reason} onChange={event => setReason(event.target.value)} autoFocus /></label>
      <label className="vps-confirm"><input type="checkbox" required checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />Tôi xác nhận thực hiện thao tác thật trên VPS này.</label>
      <button type="submit" className={operation.action === "Reboot" ? "danger" : "primary"} disabled={working || !confirmed || reason.trim().length < 3}>{working ? "Đang thực hiện…" : "Xác nhận thực hiện"}</button>
    </form></Dialog>}
  </section>;
}
