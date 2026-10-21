"use client";
import { useCallback, useState } from "react";
import { ApiClient } from "@/lib/api-client";
import { useLiveQuery } from "@/hooks/use-live-query";
const empty = "00000000-0000-0000-0000-000000000000";
export function DepartmentPanel() {
  const load = useCallback(async () => { const api = new ApiClient(); return { directory: await api.departments(), policies: await api.policies() }; }, []);
  const { data, error, refresh } = useLiveQuery(load);
  const [editing, setEditing] = useState(""); const [name, setName] = useState(""); const [policy, setPolicy] = useState(""); const [reason, setReason] = useState(""); const [confirmed, setConfirmed] = useState(false); const [busy, setBusy] = useState(false); const [message, setMessage] = useState("");
  const revision = data?.directory.departments.find(x => x.id === editing)?.revision ?? empty;
  async function mutate(action: () => Promise<unknown>) { if (busy || !confirmed || reason.trim().length < 3) return; setBusy(true); setMessage(""); try { await action(); refresh(); setMessage("Đã lưu thay đổi."); } catch (cause) { setMessage(cause instanceof Error ? cause.message : "Không thể lưu"); } finally { setBusy(false); } }
  return <details className="panel upgrade-panel"><summary>Phòng ban và chính sách mặc định</summary><p>Chính sách gán riêng cho máy được ưu tiên. Máy mới vào phòng ban sẽ nhận chính sách mặc định khi Agent cập nhật chính sách; thay đổi phòng ban của nhân viên không tự chuyển máy của họ.</p>
    {error && <p role="alert">Chưa đọc được phòng ban.</p>}
    <label>Phòng ban<select value={editing} onChange={e => { setEditing(e.target.value); const d = data?.directory.departments.find(x => x.id === e.target.value); setName(d?.name ?? ""); setPolicy(d?.policyId ?? ""); }}><option value="">Tạo phòng ban mới</option>{data?.directory.departments.map(d => <option key={d.id} value={d.id}>{d.name} ({d.deviceIds.length} máy)</option>)}</select></label>
    <form className="management-form" onSubmit={e => { e.preventDefault(); void mutate(() => new ApiClient().saveDepartment({ name, policyId: policy || null, revision }, reason, editing || undefined)); }}>
      <label>Tên phòng ban<input required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></label><label>Chính sách mặc định<select value={policy} onChange={e => setPolicy(e.target.value)}><option value="">Không có</option>{data?.policies.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
      <label>Lý do thay đổi<input required minLength={3} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><label><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} /> Tôi xác nhận thay đổi tổ chức và chính sách</label><button className="action" disabled={busy || !confirmed}>Lưu phòng ban</button>
    </form>
    {(["users", "devices"] as const).map(kind => <details key={kind}><summary>{kind === "users" ? "Nhân viên" : "Thiết bị"}</summary>{data?.directory[kind].map(member => <label key={member.id}>{member.name}<select aria-label={`Phòng ban của ${member.name}`} value={member.departmentId ?? ""} disabled={busy || !confirmed || reason.trim().length < 3} onChange={e => { const d = data.directory.departments.find(x => x.id === e.target.value); void mutate(() => new ApiClient().assignDepartment(kind, member.id, d?.id ?? null, d?.revision ?? empty, reason)); }}><option value="">Chưa thuộc phòng ban</option>{data.directory.departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>)}</details>)}
    {message && <p role="status">{message}</p>}
  </details>;
}
