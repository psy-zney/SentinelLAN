"use client";
import { useCallback, useState } from "react";
import { ApiClient } from "@/lib/api-client";
import { useLiveQuery } from "@/hooks/use-live-query";
import type { SoftwareRule } from "@/types/software";

const labels: Record<string, string> = { Installed: "Đã cài", Missing: "Chưa cài", UpdateAvailable: "Có bản cập nhật", VersionUnknown: "Chưa đọc được phiên bản", ProhibitedPresent: "Cần IT kiểm tra", Compliant: "Không phát hiện", Unknown: "Chưa xác định", NotSupported: "Chưa hỗ trợ trên máy này", DetectionNotConfigured: "IT chưa cấu hình nhận diện" };
export function SoftwareCompliancePanel({ deviceId }: { deviceId?: string }) {
  const load = useCallback(() => new ApiClient().softwareStatus(deviceId), [deviceId]);
  const { data, error, refresh } = useLiveQuery(load);
  return <section className="panel"><div className="panel-head"><h2>Phần mềm của cơ quan</h2><button className="action-sm" type="button" onClick={refresh}>Cập nhật</button></div>
    <p>Máy chỉ đối chiếu các phần mềm IT đã cấu hình. Thông tin có thể chậm tối đa 15 phút; IT có thể yêu cầu cập nhật ngay.</p>
    {error ? <p role="alert">Chưa tải được trạng thái phần mềm.</p> : !data ? <p role="status">Đang tải…</p> : data.length === 0 ? <p>Chưa có phần mềm được cấu hình.</p> : <ul>{data.map(app => <li key={`${app.source}:${app.ruleId}`}><strong>{app.name}</strong> — {labels[app.status] ?? "Chưa xác định"}{app.installedVersion && ` · ${app.installedVersion}`}{app.status === "UpdateAvailable" && ` → ${app.requiredVersion}`}{app.capturedAt && <small> · Kiểm tra lúc {new Date(app.capturedAt).toLocaleString("vi-VN")}</small>}</li>)}</ul>}
  </section>;
}

export function SoftwareRulesPanel() {
  const load = useCallback(() => new ApiClient().softwareRules(), []);
  const { data, error, refresh } = useLiveQuery(load);
  const [editing, setEditing] = useState<SoftwareRule | null | undefined>(undefined);
  return <section className="panel"><div className="panel-head"><h2>Quy tắc phần mềm bị cấm</h2><button type="button" className="action-sm" onClick={() => setEditing(null)}>Thêm quy tắc</button></div>
    <p>Chỉ hỗ trợ nhận diện MSI trên Windows theo ProductCode hoặc UpgradeCode. Không quét danh sách ứng dụng cá nhân và không tự gỡ cài đặt. Phần mềm portable và bản cài riêng của tài khoản khác có thể không được nhận diện. Tắt quy tắc dừng kiểm tra mới; IT vẫn cần xác minh và xử lý cảnh báo đã mở.</p>
    {error && <p role="alert">Chưa đọc được quy tắc.</p>}
    {data?.map(rule => <p key={rule.id}>{rule.name} · {rule.isActive ? "Đang kiểm tra" : "Đã tắt"} <button className="action-sm" type="button" onClick={() => setEditing(rule)}>Sửa</button></p>)}
    {editing !== undefined && <RuleEditor key={editing?.id ?? "new"} rule={editing} onSaved={() => { setEditing(undefined); refresh(); }} onCancel={() => setEditing(undefined)} />}
  </section>;
}
function RuleEditor({ rule, onSaved, onCancel }: { rule: SoftwareRule | null; onSaved: () => void; onCancel: () => void }) {
  const [name, setName] = useState(rule?.name ?? ""); const [product, setProduct] = useState(rule?.productCode ?? ""); const [upgrade, setUpgrade] = useState(rule?.upgradeCode ?? "");
  const [active, setActive] = useState(rule?.isActive ?? true); const [reason, setReason] = useState(""); const [confirmed, setConfirmed] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  return <form className="management-form" onSubmit={event => { event.preventDefault(); if (busy) return; setBusy(true); setError(""); void new ApiClient().saveSoftwareRule({ name, productCode: product.trim() || null, upgradeCode: upgrade.trim() || null, isActive: active, revision: rule?.revision ?? "00000000-0000-0000-0000-000000000000", reason, confirmed }, rule?.id).then(onSaved).catch(cause => setError(cause instanceof Error ? cause.message : "Không thể lưu quy tắc")).finally(() => setBusy(false)); }}>
    <label>Tên phần mềm<input required maxLength={120} value={name} onChange={e => setName(e.target.value)} /></label>
    <label>MSI ProductCode (UUID)<input value={product} onChange={e => setProduct(e.target.value)} /></label><label>MSI UpgradeCode (UUID)<input value={upgrade} onChange={e => setUpgrade(e.target.value)} /></label>
    <label>Lý do<input required minLength={3} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><label><input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} /> Đang kiểm tra</label><label><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} /> Tôi xác nhận quy tắc áp dụng trong cơ quan</label>
    {error && <p role="alert">{error}</p>}<button className="action" disabled={busy || !confirmed}>Lưu</button><button type="button" className="action-outline" onClick={onCancel}>Hủy</button>
  </form>;
}
