"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { Role } from "@/types/api";

type NewUserRole = Exclude<Role, "Agent">;
const emptyForm = { email: "", displayName: "", role: "Employee" as NewUserRole, password: "", reason: "", confirmed: false };

export function UsersView() {
  const { t, lang } = useTranslation();
  const load = useCallback(async () => {
    const client = new ApiClient();
    const [users, org] = await Promise.all([client.users(), client.organization()]);
    return { users, org };
  }, []);
  const { data, error, refresh } = useLiveQuery(load);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const closeForm = () => {
    if (submitting) return;
    setOpen(false);
    setForm(emptyForm);
  };

  async function createUser(event: React.FormEvent) {
    event.preventDefault();
    if (submitting || !form.confirmed) return;
    setSubmitting(true);
    setMessage(null);
    try {
      await new ApiClient().createUser(form);
      setMessage(lang === "vi" ? "Đã tạo tài khoản." : "Account created.");
      setForm(emptyForm);
      setOpen(false);
      refresh();
    } catch (cause) {
      setMessage(
        cause instanceof ApiError && cause.status === 409
          ? lang === "vi" ? "Email đã tồn tại trong tổ chức." : "That email already exists in this organization."
          : cause instanceof ApiError && cause.status === 403
            ? lang === "vi" ? "Bạn không có quyền tạo tài khoản." : "You are not allowed to create accounts."
            : lang === "vi" ? "Không thể tạo tài khoản. Kiểm tra dữ liệu và quyền truy cập." : "Account could not be created. Check the form and your permissions."
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <div className="panel" role="alert"><div className="panel-head"><h2>{t("users")}</h2></div><p className="subtitle">{t("error")}</p><button className="action" onClick={refresh}>{t("retry")}</button></div>;
  if (!data) return <p className="subtitle" role="status">{t("loading")}</p>;
  const { users, org } = data;

  return <>
    {org && <section className="metrics" aria-label="Organization identity">
      <div className="metric"><span>{t("orgName")}</span><strong className="metric-text">{org.name}</strong></div>
      <div className="metric"><span>{t("orgCode")}</span><strong className="metric-text">{org.code}</strong></div>
      <div className="metric"><span>{lang === "vi" ? "Tổng tài khoản" : "Total Accounts"}</span><strong>{users.length}</strong></div>
      <div className="metric"><span>{lang === "vi" ? "Khởi tạo ngày" : "Tenant Established"}</span><strong className="metric-text">{new Date(org.createdAt).toLocaleDateString()}</strong></div>
    </section>}
    {message && <div className="panel" role="status" style={{ marginBottom: 18 }}><strong>{message}</strong></div>}
    <div className="panel" style={{ marginBottom: 24 }}>
      <div className="panel-head"><div><h2>{t("userDirectory")}</h2><p className="subtitle" style={{ fontSize: ".82rem" }}>{t("subtitleUsers")}</p></div><button className="action" onClick={() => { setMessage(null); setOpen(true); }}>{lang === "vi" ? "+ Tạo tài khoản" : "+ Create account"}</button></div>
      <div style={{ overflowX: "auto" }}><table><thead><tr><th>{t("userDisplayName")}</th><th>{t("userEmail")}</th><th>{t("userRole")}</th><th>{t("created")}</th></tr></thead><tbody>{users.map(user => <tr key={user.id}><td><strong>{user.displayName}</strong></td><td>{user.email}</td><td><span className={`badge ${user.role === "Admin" ? "badge-info" : user.role === "Technician" ? "badge-warn" : "badge-neutral"}`}>{user.role}</span></td><td>{new Date(user.createdAt).toLocaleDateString()}</td></tr>)}</tbody></table></div>
    </div>
    <div className="panel"><div className="panel-head"><h2>{t("rbacTitle")}</h2></div><p className="subtitle">{lang === "vi" ? "Chỉ quản trị viên có thể xem danh bạ và tạo tài khoản. Mật khẩu chỉ dùng để khởi tạo và không xuất hiện trong danh sách." : "Only administrators can view the directory and create accounts. Passwords are used at creation time and never shown in the directory."}</p></div>
    {open && <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="new-user-title"><div className="modal"><div className="modal-header"><h3 id="new-user-title">{lang === "vi" ? "Tạo tài khoản nội bộ" : "Create internal account"}</h3><button className="modal-close" onClick={closeForm} aria-label={t("cancel")}>×</button></div>
      <form onSubmit={createUser}>
        <div className="form-group"><label htmlFor="user-display-name">{t("userDisplayName")}</label><input id="user-display-name" className="form-input" minLength={2} maxLength={100} required value={form.displayName} onChange={e => setForm({ ...form, displayName: e.target.value })} /></div>
        <div className="form-group"><label htmlFor="user-email">{t("userEmail")}</label><input id="user-email" className="form-input" type="email" maxLength={254} required value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
        <div className="form-group"><label htmlFor="user-role">{t("userRole")}</label><select id="user-role" className="form-select" value={form.role} onChange={e => setForm({ ...form, role: e.target.value as NewUserRole })}><option>Employee</option><option>Technician</option><option>Admin</option></select></div>
        <div className="form-group"><label htmlFor="user-password">{lang === "vi" ? "Mật khẩu ban đầu" : "Initial password"}</label><input id="user-password" className="form-input" type="password" minLength={12} maxLength={128} required value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} /></div>
        <div className="form-group"><label htmlFor="user-reason">{lang === "vi" ? "Lý do" : "Reason"}</label><textarea id="user-reason" className="form-input" minLength={3} maxLength={1000} required value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></div>
        <label><input type="checkbox" checked={form.confirmed} onChange={e => setForm({ ...form, confirmed: e.target.checked })} /> {lang === "vi" ? "Tôi xác nhận tạo tài khoản này và chịu trách nhiệm kiểm toán." : "I confirm this account creation and accept audit accountability."}</label>
        {message && <p role="alert" className="subtitle">{message}</p>}
        <div className="btn-row"><button type="button" className="action-outline" onClick={closeForm}>{t("cancel")}</button><button type="submit" className="action" disabled={submitting || !form.confirmed}>{submitting ? t("saving") : t("save")}</button></div>
      </form>
    </div></div>}
  </>;
}
