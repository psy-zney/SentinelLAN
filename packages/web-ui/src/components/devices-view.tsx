"use client";
import { useState } from "react";
import { DeviceTable } from "@/components/device-table";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useCurrentSession } from "@/components/app-shell";
import { useTranslation } from "@/lib/i18n";
import type { Device, EnrollmentToken } from "@/types/api";
const loadDevices = () => new ApiClient().devices();
export function DevicesView() {
  const { t, lang } = useTranslation(); const session = useCurrentSession();
  const { data: devices, error, refresh } = useLiveQuery(loadDevices);
  const [query, setQuery] = useState(""); const [filter, setFilter] = useState("all"); const [tokenOpen, setTokenOpen] = useState(false);
  const [validForMinutes, setValidForMinutes] = useState(15); const [reason, setReason] = useState(""); const [confirmed, setConfirmed] = useState(false); const [token, setToken] = useState<EnrollmentToken | null>(null); const [saving, setSaving] = useState(false); const [message, setMessage] = useState("");
  async function createToken(event: React.FormEvent) { event.preventDefault(); if (saving || !confirmed) return; setSaving(true); setMessage(""); try { const value = await new ApiClient().createEnrollmentToken({ validForMinutes, reason: reason.trim(), confirmed }); setToken(value); setReason(""); setConfirmed(false); } catch (cause) { setMessage(cause instanceof ApiError && cause.status === 403 ? (lang === "vi" ? "Chỉ quản trị viên có thể cấp mã kết nối." : "Only administrators can issue connection codes.") : cause instanceof ApiError && cause.status === 503 ? (lang === "vi" ? "Cần cấu hình địa chỉ máy chủ trước khi cấp mã. Liên hệ người vận hành hệ thống." : "Configure the Agent server address before issuing a code.") : (lang === "vi" ? "Không thể cấp mã kết nối. Hãy thử lại." : "Connection code could not be issued. Try again.")); } finally { setSaving(false); } }
  async function copyCode() {
    if (!token) return;
    try {
      await navigator.clipboard.writeText(token.connectionCode);
      setMessage(lang === "vi" ? "Đã sao chép mã kết nối." : "Connection code copied.");
    } catch { setMessage(lang === "vi" ? "Hãy chọn và sao chép mã trong ô bên dưới." : "Select and copy the code from the field."); }
  }
  if (error) return <div role="alert" className="panel"><p>{t("error")}</p><button className="action" onClick={refresh}>{t("retry")}</button></div>;
  if (!devices) return <p role="status">{t("loading")}</p>;
  const visible = devices.filter((device: Device) => { const matches = `${device.name} ${device.osVersion}`.toLowerCase().includes(query.toLowerCase()); const status = filter === "all" || filter === (device.isRevoked ? "revoked" : device.isOnline ? "online" : "offline"); return matches && status; });
  return <>
    <div className="panel" style={{ marginBottom: 18 }}><div className="filter-bar"><label htmlFor="device-search" className="sr-only">{lang === "vi" ? "Tìm thiết bị" : "Search devices"}</label><input id="device-search" className="form-input" style={{ maxWidth: 340 }} placeholder={t("search")} value={query} onChange={e => setQuery(e.target.value)} /><label htmlFor="device-filter" className="sr-only">{t("filter")}</label><select id="device-filter" className="form-select" style={{ maxWidth: 180 }} value={filter} onChange={e => setFilter(e.target.value)}><option value="all">{t("all")}</option><option value="online">{t("online")}</option><option value="offline">{t("offline")}</option><option value="revoked">{lang === "vi" ? "Đã thu hồi" : "Revoked"}</option></select>{session?.role === "Admin" && <button className="action" onClick={() => { setTokenOpen(true); setToken(null); setMessage(""); }}>{lang === "vi" ? "+ Tạo mã kết nối thiết bị" : "+ Issue enrollment token"}</button>}</div></div>
    {visible.length ? <DeviceTable initialDevices={visible} /> : <div className="panel empty-state"><p>{lang === "vi" ? "Không có thiết bị phù hợp bộ lọc." : "No devices match the current filter."}</p></div>}
    {tokenOpen && <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="token-title"><div className="modal"><div className="modal-header"><h3 id="token-title">{lang === "vi" ? "Tạo mã kết nối thiết bị" : "Issue connection code"}</h3><button className="modal-close" onClick={() => { setTokenOpen(false); setToken(null); }} aria-label={t("cancel")}>×</button></div>{token ? <>
      <p className="subtitle">{lang === "vi" ? "Sao chép mã trước khi đóng cửa sổ. Nhân viên mở bộ cài và dán mã; không cần nhập địa chỉ máy chủ hay chạy lệnh." : "Copy this code before closing. Paste it into the installer; no server address or command is needed."}</p>
      <label className="form-group">{lang === "vi" ? "Mã kết nối (giữ riêng)" : "Connection code (keep private)"}<input className="form-input" readOnly value={token.connectionCode} aria-label={lang === "vi" ? "Mã kết nối" : "Connection code"} onFocus={event => event.currentTarget.select()} /></label>
      <p className="subtitle">{lang === "vi" ? "Mỗi mã chỉ đăng ký được một máy. Đã dùng thì không thể dùng lại, dù còn thời hạn." : "One successful device registration only. A used code cannot be reused even before expiry."}</p>
      <p className="subtitle">{lang === "vi" ? "Hết hạn" : "Expires"}: {new Date(token.expiresAt).toLocaleString()}</p>
      {message && <p role="status" className="subtitle">{message}</p>}
      <div className="btn-row"><button className="action" onClick={copyCode}>{lang === "vi" ? "Sao chép mã" : "Copy code"}</button><button className="action-outline" onClick={() => { setTokenOpen(false); setToken(null); }}>{lang === "vi" ? "Đóng" : "Close"}</button></div>
    </> : <form onSubmit={createToken}><div className="form-group"><label htmlFor="token-validity">{lang === "vi" ? "Thời hạn (phút)" : "Validity (minutes)"}</label><input id="token-validity" className="form-input" type="number" min={1} max={60} required value={validForMinutes} onChange={e => setValidForMinutes(Number(e.target.value))} /></div><div className="form-group"><label htmlFor="token-reason">{lang === "vi" ? "Lý do" : "Reason"}</label><textarea id="token-reason" className="form-input" minLength={3} maxLength={1000} required value={reason} onChange={e => setReason(e.target.value)} /></div><label><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} /> {lang === "vi" ? "Tôi được phép cấp mã kết nối cho thiết bị này." : "I confirm this token issuance and accept audit accountability."}</label>{message && <p role="alert" className="subtitle">{message}</p>}<div className="btn-row"><button type="button" className="action-outline" onClick={() => setTokenOpen(false)}>{t("cancel")}</button><button type="submit" className="action" disabled={saving || !confirmed}>{saving ? t("saving") : t("save")}</button></div></form>}</div></div>}
  </>;
}
