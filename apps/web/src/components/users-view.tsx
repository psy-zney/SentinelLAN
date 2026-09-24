"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { Role, UserItem } from "@/types/api";

type NewUserRole = Exclude<Role, "Agent">;
const emptyForm = { email: "", displayName: "", role: "Employee" as NewUserRole, reason: "", confirmed: false };

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

  // Single-use token display modal
  const [activationModal, setActivationModal] = useState<{
    open: boolean;
    userEmail: string;
    url: string;
    copied: boolean;
  }>({ open: false, userEmail: "", url: "", copied: false });

  // Reissue modal
  const [reissueTarget, setReissueTarget] = useState<UserItem | null>(null);
  const [reissueReason, setReissueReason] = useState("");
  const [reissuing, setReissuing] = useState(false);

  // Revoke confirm
  const [revokeTarget, setRevokeTarget] = useState<UserItem | null>(null);
  const [revoking, setRevoking] = useState(false);
  const [statusTarget, setStatusTarget] = useState<UserItem | null>(null);
  const [statusReason, setStatusReason] = useState("");
  const [statusConfirmed, setStatusConfirmed] = useState(false);
  const [changingStatus, setChangingStatus] = useState(false);

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
      const res = await new ApiClient().createUser(form);
      const origin = typeof window !== "undefined" ? window.location.origin : "";
      const fullUrl = res.activationUrl?.startsWith("http")
        ? res.activationUrl
        : `${origin}${res.activationUrl ?? `/activate?token=${res.activationToken}`}`;

      setForm(emptyForm);
      setOpen(false);
      refresh();

      if (res.activationToken) {
        setActivationModal({
          open: true,
          userEmail: res.user.email,
          url: fullUrl,
          copied: false
        });
      } else {
        setMessage(lang === "vi" ? "Đã tạo tài khoản." : "Account created.");
      }
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

  async function handleReissue(event: React.FormEvent) {
    event.preventDefault();
    if (!reissueTarget || !reissueReason.trim() || reissuing) return;
    setReissuing(true);
    try {
      const res = await new ApiClient().reissueActivationToken(reissueTarget.id, reissueReason.trim(), true);
      const origin = typeof window !== "undefined" ? window.location.origin : "";
      const fullUrl = res.activationUrl?.startsWith("http")
        ? res.activationUrl
        : `${origin}${res.activationUrl}`;

      setReissueTarget(null);
      setReissueReason("");
      refresh();
      setActivationModal({
        open: true,
        userEmail: res.userEmail,
        url: fullUrl,
        copied: false
      });
    } catch {
      setMessage(lang === "vi" ? "Không thể cấp lại liên kết kích hoạt." : "Failed to reissue activation link.");
    } finally {
      setReissuing(false);
    }
  }

  async function handleRevoke() {
    if (!revokeTarget || revoking) return;
    setRevoking(true);
    try {
      await new ApiClient().revokeActivationToken(revokeTarget.id, "Admin revoked invite", true);
      setRevokeTarget(null);
      refresh();
      setMessage(lang === "vi" ? "Đã thu hồi lời mời kích hoạt." : "Activation invite revoked.");
    } catch {
      setMessage(lang === "vi" ? "Không thể thu hồi lời mời." : "Failed to revoke invite.");
    } finally {
      setRevoking(false);
    }
  }

  async function handleStatusChange(event: React.FormEvent) {
    event.preventDefault();
    if (!statusTarget || !statusConfirmed || statusReason.trim().length < 3 || changingStatus) return;
    const nextStatus = statusTarget.status === "Locked" ? "Active" : "Locked";
    setChangingStatus(true);
    try {
      await new ApiClient().setUserStatus(statusTarget.id, nextStatus, statusReason.trim(), true);
      setStatusTarget(null);
      setStatusReason("");
      setStatusConfirmed(false);
      setMessage(nextStatus === "Locked"
        ? lang === "vi" ? "Đã khóa tài khoản và thu hồi phiên đăng nhập." : "Account locked and sessions revoked."
        : lang === "vi" ? "Đã mở khóa tài khoản; người dùng cần đăng nhập lại." : "Account unlocked; the user must sign in again.");
      refresh();
    } catch (cause) {
      setMessage(cause instanceof ApiError && cause.status === 409
        ? lang === "vi" ? "Không thể khóa chính mình hoặc Admin cuối cùng." : "You cannot lock yourself or the last active admin."
        : lang === "vi" ? "Không thể đổi trạng thái tài khoản." : "Could not change account status.");
    } finally {
      setChangingStatus(false);
    }
  }

  function copyActivationUrl() {
    if (!activationModal.url) return;
    navigator.clipboard.writeText(activationModal.url).then(() => {
      setActivationModal(prev => ({ ...prev, copied: true }));
      setTimeout(() => setActivationModal(prev => ({ ...prev, copied: false })), 3000);
    });
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
      <div className="panel-head">
        <div>
          <h2>{t("userDirectory")}</h2>
          <p className="subtitle" style={{ fontSize: ".82rem" }}>{t("subtitleUsers")}</p>
        </div>
        <button className="action" onClick={() => { setMessage(null); setOpen(true); }}>
          {lang === "vi" ? "+ Tạo tài khoản" : "+ Create account"}
        </button>
      </div>
      <div style={{ overflowX: "auto" }}>
        <table>
          <thead>
            <tr>
              <th>{t("userDisplayName")}</th>
              <th>{t("userEmail")}</th>
              <th>{t("userRole")}</th>
              <th>{t("status")}</th>
              <th>{t("created")}</th>
              <th>{t("actions")}</th>
            </tr>
          </thead>
          <tbody>
            {users.map(user => {
              const status = user.status ?? "Active";
              const isPending = status === "PendingActivation" || user.hasActiveInvitation;
              return (
                <tr key={user.id}>
                  <td><strong>{user.displayName}</strong></td>
                  <td>{user.email}</td>
                  <td>
                    <span className={`badge ${user.role === "Admin" ? "badge-info" : user.role === "Technician" ? "badge-warn" : "badge-neutral"}`}>
                      {user.role}
                    </span>
                  </td>
                  <td>
                    <span className={`badge ${status === "Active" ? "badge-success" : status === "PendingActivation" ? "badge-warn" : "badge-danger"}`}>
                      {status === "Active" ? t("statusActive") : status === "PendingActivation" ? t("statusPending") : t("statusLocked")}
                    </span>
                  </td>
                  <td>{new Date(user.createdAt).toLocaleDateString()}</td>
                  <td>
                    {isPending && (
                      <div style={{ display: "flex", gap: 6 }}>
                        <button
                          type="button"
                          className="action-outline"
                          style={{ padding: "3px 8px", fontSize: ".76rem" }}
                          onClick={() => { setReissueTarget(user); setReissueReason(""); }}
                        >
                          🔄 {t("reissueInvite")}
                        </button>
                        <button
                          type="button"
                          className="action-outline"
                          style={{ padding: "3px 8px", fontSize: ".76rem", color: "var(--danger)", borderColor: "var(--danger)" }}
                          onClick={() => setRevokeTarget(user)}
                        >
                          ✕ {t("revokeInvite")}
                        </button>
                      </div>
                    )}
                    {(status === "Active" || status === "Locked") && (
                      <button
                        type="button"
                        className="action-outline"
                        style={{ padding: "3px 8px", fontSize: ".76rem", color: status === "Active" ? "var(--danger)" : undefined }}
                        onClick={() => { setStatusTarget(user); setStatusReason(""); setStatusConfirmed(false); setMessage(null); }}
                      >
                        {status === "Active" ? (lang === "vi" ? "Khóa" : "Lock") : (lang === "vi" ? "Mở khóa" : "Unlock")}
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
    <div className="panel">
      <div className="panel-head"><h2>{t("rbacTitle")}</h2></div>
      <p className="subtitle">
        {lang === "vi"
          ? "Admin tạo tài khoản, quản lý lời mời và khóa/mở khóa có ghi audit. Tài khoản bị khóa mất quyền truy cập ngay; khi mở khóa phải đăng nhập lại."
          : "Admins create accounts, manage invitations and lock or unlock accounts with an audit trail. Locking removes access immediately; unlocking requires a new sign-in."}
      </p>
    </div>

    {/* Modal: Create User */}
    {open && (
      <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="new-user-title">
        <div className="modal">
          <div className="modal-header">
            <h3 id="new-user-title">{t("createPendingUser")}</h3>
            <button className="modal-close" onClick={closeForm} aria-label={t("cancel")}>×</button>
          </div>
          <form onSubmit={createUser}>
            <div className="form-group">
              <label htmlFor="user-display-name">{t("userDisplayName")}</label>
              <input
                id="user-display-name"
                className="form-input"
                minLength={2}
                maxLength={100}
                required
                value={form.displayName}
                onChange={e => setForm({ ...form, displayName: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label htmlFor="user-email">{t("userEmail")}</label>
              <input
                id="user-email"
                className="form-input"
                type="email"
                maxLength={254}
                required
                value={form.email}
                onChange={e => setForm({ ...form, email: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label htmlFor="user-role">{t("userRole")}</label>
              <select
                id="user-role"
                className="form-select"
                value={form.role}
                onChange={e => setForm({ ...form, role: e.target.value as NewUserRole })}
              >
                <option value="Employee">Employee</option>
                <option value="Technician">Technician</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="user-reason">{lang === "vi" ? "Lý do tạo tài khoản" : "Operational Reason"}</label>
              <textarea
                id="user-reason"
                className="form-input"
                minLength={3}
                maxLength={1000}
                required
                placeholder={lang === "vi" ? "VD: Cấp quyền nhân viên mới phòng Kế toán..." : "e.g., Onboarding new finance analyst..."}
                value={form.reason}
                onChange={e => setForm({ ...form, reason: e.target.value })}
              />
            </div>
            <label style={{ display: "flex", alignItems: "flex-start", gap: 8, margin: "14px 0", fontSize: ".85rem" }}>
              <input
                type="checkbox"
                checked={form.confirmed}
                onChange={e => setForm({ ...form, confirmed: e.target.checked })}
              />
              <span>{lang === "vi" ? "Tôi xác nhận tạo tài khoản này và chịu trách nhiệm kiểm toán." : "I confirm this account creation and accept audit accountability."}</span>
            </label>
            {message && <p role="alert" className="subtitle">{message}</p>}
            <div className="btn-row">
              <button type="button" className="action-outline" onClick={closeForm}>{t("cancel")}</button>
              <button type="submit" className="action" disabled={submitting || !form.confirmed}>
                {submitting ? t("saving") : t("save")}
              </button>
            </div>
          </form>
        </div>
      </div>
    )}

    {/* Modal: Single-Use Activation Link Display */}
    {activationModal.open && (
      <div className="modal-backdrop" role="dialog" aria-modal="true">
        <div className="modal" style={{ maxWidth: 520 }}>
          <div className="modal-header">
            <h3>🔗 {t("activationLinkTitle")}</h3>
            <button className="modal-close" onClick={() => setActivationModal(prev => ({ ...prev, open: false }))}>×</button>
          </div>
          <div className="privacy" style={{ borderColor: "var(--warn)", margin: "12px 0" }}>
            <strong>⚠️ {t("activationLinkWarning")}</strong>
          </div>
          <div style={{ margin: "14px 0" }}>
            <label style={{ fontSize: ".82rem", color: "var(--muted)" }}>
              {lang === "vi" ? `Liên kết kích hoạt cho ${activationModal.userEmail}:` : `Activation link for ${activationModal.userEmail}:`}
            </label>
            <div style={{ display: "flex", gap: 8, marginTop: 6 }}>
              <input
                readOnly
                className="form-input"
                style={{ fontSize: ".85rem", fontFamily: "monospace" }}
                value={activationModal.url}
                onClick={e => (e.target as HTMLInputElement).select()}
              />
              <button
                type="button"
                className="action"
                style={{ whiteSpace: "nowrap" }}
                onClick={copyActivationUrl}
              >
                {activationModal.copied ? `✓ ${t("linkCopied")}` : `📋 ${t("copyLink")}`}
              </button>
            </div>
          </div>
          <div className="btn-row" style={{ marginTop: 20 }}>
            <button
              type="button"
              className="action"
              onClick={() => setActivationModal(prev => ({ ...prev, open: false }))}
            >
              {lang === "vi" ? "Đã sao chép & Đóng" : "Copied & Close"}
            </button>
          </div>
        </div>
      </div>
    )}

    {/* Modal: Reissue Activation Token */}
    {reissueTarget && (
      <div className="modal-backdrop" role="dialog" aria-modal="true">
        <div className="modal">
          <div className="modal-header">
            <h3>🔄 {t("reissueInvite")}</h3>
            <button className="modal-close" onClick={() => setReissueTarget(null)}>×</button>
          </div>
          <form onSubmit={handleReissue}>
            <p className="subtitle">
              {lang === "vi"
                ? `Cấp lại liên kết kích hoạt cho ${reissueTarget.displayName} (${reissueTarget.email}). Các liên kết cũ sẽ bị vô hiệu hóa ngay lập tức.`
                : `Reissue a new activation link for ${reissueTarget.displayName} (${reissueTarget.email}). Any prior unused tokens will be revoked.`}
            </p>
            <div className="form-group">
              <label>{lang === "vi" ? "Lý do cấp lại (Bắt buộc)" : "Reason for reissuing (Required)"}</label>
              <textarea
                className="form-input"
                required
                minLength={3}
                placeholder={lang === "vi" ? "VD: Nhân viên chưa nhận được link kích hoạt..." : "e.g. Employee requested new activation link..."}
                value={reissueReason}
                onChange={e => setReissueReason(e.target.value)}
              />
            </div>
            <div className="btn-row">
              <button type="button" className="action-outline" onClick={() => setReissueTarget(null)}>
                {t("cancel")}
              </button>
              <button type="submit" className="action" disabled={reissuing || !reissueReason.trim()}>
                {reissuing ? t("saving") : t("confirm")}
              </button>
            </div>
          </form>
        </div>
      </div>
    )}

    {/* Modal: Revoke Invitation Confirm */}
    {revokeTarget && (
      <div className="modal-backdrop" role="dialog" aria-modal="true">
        <div className="modal">
          <div className="modal-header">
            <h3>✕ {t("revokeInvite")}</h3>
            <button className="modal-close" onClick={() => setRevokeTarget(null)}>×</button>
          </div>
          <p className="subtitle">
            {lang === "vi"
              ? `Bạn có chắc chắn muốn thu hồi lời mời kích hoạt của ${revokeTarget.displayName} (${revokeTarget.email})?`
              : `Are you sure you want to revoke the activation invitation for ${revokeTarget.displayName} (${revokeTarget.email})?`}
          </p>
          <div className="btn-row">
            <button type="button" className="action-outline" onClick={() => setRevokeTarget(null)}>
              {t("cancel")}
            </button>
            <button type="button" className="action" style={{ background: "var(--danger)" }} onClick={handleRevoke} disabled={revoking}>
              {revoking ? t("saving") : t("confirm")}
            </button>
          </div>
        </div>
      </div>
    )}
    {statusTarget && (
      <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="user-status-title">
        <div className="modal">
          <div className="modal-header">
            <h3 id="user-status-title">{statusTarget.status === "Locked"
              ? lang === "vi" ? "Mở khóa tài khoản" : "Unlock account"
              : lang === "vi" ? "Khóa tài khoản" : "Lock account"}</h3>
            <button className="modal-close" onClick={() => setStatusTarget(null)} aria-label={t("cancel")}>×</button>
          </div>
          <form onSubmit={handleStatusChange}>
            <p className="subtitle">{statusTarget.displayName} ({statusTarget.email})</p>
            <div className="form-group">
              <label htmlFor="status-reason">{lang === "vi" ? "Lý do" : "Reason"}</label>
              <textarea id="status-reason" className="form-input" required minLength={3} maxLength={1000}
                value={statusReason} onChange={event => setStatusReason(event.target.value)} />
            </div>
            <label style={{ display: "flex", gap: 8, alignItems: "flex-start", margin: "14px 0" }}>
              <input type="checkbox" checked={statusConfirmed} onChange={event => setStatusConfirmed(event.target.checked)} />
              <span>{lang === "vi" ? "Tôi xác nhận thay đổi quyền truy cập của tài khoản này." : "I confirm this account access change."}</span>
            </label>
            <div className="btn-row">
              <button type="button" className="action-outline" onClick={() => setStatusTarget(null)}>{t("cancel")}</button>
              <button type="submit" className="action" disabled={changingStatus || !statusConfirmed || statusReason.trim().length < 3}>
                {changingStatus ? t("saving") : t("confirm")}
              </button>
            </div>
          </form>
        </div>
      </div>
    )}
  </>;
}
