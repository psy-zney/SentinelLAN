"use client";

import { useState } from "react";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useLiveQuery } from "@/hooks/use-live-query";
import { useTranslation } from "@/lib/i18n";
import type { MonitoringSettings } from "@/types/endpoint";

const loadSettings = () => new ApiClient().monitoringSettings();
const fields = [
  ["cpuThresholdPercent", "Ngưỡng CPU (%)", "CPU threshold (%)", 1, 100],
  ["ramThresholdPercent", "Ngưỡng RAM (%)", "RAM threshold (%)", 1, 100],
  ["diskThresholdPercent", "Ngưỡng đĩa (%)", "Disk threshold (%)", 1, 100],
  ["offlineMinutes", "Báo mất kết nối sau (phút)", "Offline after (minutes)", 2, 1440],
  ["sustainedMinutes", "CPU/RAM duy trì trong (phút)", "Sustained CPU/RAM (minutes)", 0, 30],
  ["maintenancePauseMinutes", "Thời gian tạm dừng khi được duyệt (phút)", "Approved maintenance pause (minutes)", 5, 60],
  ["preventiveIntervalMonths", "Chu kỳ bảo trì (tháng)", "Preventive interval (months)", 1, 60],
  ["warrantyNoticeDays", "Nhắc trước khi hết bảo hành (ngày)", "Warranty notice (days)", 1, 365]
] as const;

export function MonitoringSettingsPanel() {
  const { lang } = useTranslation();
  const vi = lang === "vi";
  const { data, error, refresh } = useLiveQuery(loadSettings);
  const [draft, setDraft] = useState<MonitoringSettings | null>(null);
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const settings = draft ?? data;
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (!settings || !confirmed || reason.trim().length < 3 || saving) return;
    setSaving(true);
    setMessage("");
    try {
      setDraft(await new ApiClient().saveMonitoringSettings(settings, reason.trim(), confirmed));
      setReason(""); setConfirmed(false); refresh();
      setMessage(vi ? "Đã lưu. Hệ thống đang kiểm tra lại các mẫu hiện có; cảnh báo cần dữ liệu đủ mới." : "Saved. Existing observations are being checked; alerts require fresh data.");
    } catch (failure) {
      setMessage(failure instanceof ApiError ? failure.message : vi ? "Chưa lưu được cấu hình. Vui lòng thử lại." : "Could not save settings. Please retry.");
    } finally { setSaving(false); }
  }
  return <details className="panel"><summary>{vi ? "Cấu hình cảnh báo và bảo trì của đơn vị" : "Organization monitoring and maintenance settings"}</summary>
    {error ? <p role="alert">{vi ? "Chưa tải được cấu hình." : "Could not load settings."} <button className="action-sm" type="button" onClick={refresh}>{vi ? "Thử lại" : "Retry"}</button></p> : !settings ? <p role="status">{vi ? "Đang tải…" : "Loading…"}</p> :
      <form onSubmit={save}>
        <p className="subtitle">{vi ? "Chỉ áp dụng cho đơn vị hiện tại. Duy trì 0 phút nghĩa là kiểm tra ngay trên một mẫu mới, phù hợp khi cần thử cảnh báo." : "Applies only to this organization. Zero sustained minutes evaluates one fresh sample, useful for an alert drill."}</p>
        <div className="form-grid">{fields.map(([key, vietnamese, english, minimum, maximum]) => <div className="form-group" key={key}>
          <label htmlFor={`monitor-${key}`}>{vi ? vietnamese : english}</label>
          <input id={`monitor-${key}`} className="form-input" type="number" min={minimum} max={maximum} step={1} required disabled={saving}
            value={settings[key]} onChange={event => setDraft({ ...settings, [key]: Number(event.target.value) })} />
        </div>)}</div>
        <label><input type="checkbox" checked={settings.securityAlertsEnabled} disabled={saving} onChange={event => setDraft({ ...settings, securityAlertsEnabled: event.target.checked })} /> {vi ? "Tự cảnh báo khi mẫu bảo mật xác nhận có vấn đề" : "Create alerts for confirmed protection problems"}</label>
        <div className="form-group"><label htmlFor="monitor-reason">{vi ? "Lý do thay đổi" : "Reason for change"}</label><input id="monitor-reason" className="form-input" value={reason} minLength={3} maxLength={1000} required disabled={saving} onChange={event => setReason(event.target.value)} /></div>
        <label><input type="checkbox" checked={confirmed} required disabled={saving} onChange={event => setConfirmed(event.target.checked)} /> {vi ? "Tôi xác nhận áp dụng cấu hình này cho đơn vị" : "I confirm these settings for this organization"}</label>
        <div className="btn-row"><button className="action" type="submit" disabled={saving || !confirmed || reason.trim().length < 3}>{saving ? (vi ? "Đang lưu…" : "Saving…") : (vi ? "Lưu và kiểm tra lại" : "Save and recheck")}</button>
          <button className="action-outline" type="button" disabled={saving} onClick={() => { setDraft(null); setConfirmed(false); refresh(); }}>{vi ? "Tải lại cấu hình" : "Reload settings"}</button></div>
        {message && <p role="status">{message}</p>}
      </form>}
  </details>;
}
