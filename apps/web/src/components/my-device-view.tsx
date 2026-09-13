"use client";

import { useEffect, useState } from "react";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { EmployeeDevice } from "@/types/api";

export function MyDeviceView() {
  const { t, lang } = useTranslation();
  const [data, setData] = useState<EmployeeDevice | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    new ApiClient().myDevice()
      .then(value => { if (active) setData(value); })
      .catch(() => {
        if (active) setError(lang === "vi"
          ? "Chưa thể tải thiết bị được gán. Hãy liên hệ với Quản trị viên IT của bạn."
          : "No assigned device could be loaded. Contact your SentinelLAN administrator.");
      });
    return () => { active = false; };
  }, [lang]);

  if (error) {
    return (
      <div className="panel" role="alert">
        <h2>{t("devices")}</h2>
        <p className="subtitle">{error}</p>
      </div>
    );
  }

  if (!data) return <p className="subtitle" role="status">{t("loading")}</p>;

  const device = data.device;

  return (
    <>
      <div className="privacy">
        <strong>{lang === "vi" ? "SentinelLAN cam kết quyền riêng tư (Privacy-First)" : "What SentinelLAN collects"}</strong>
        <br />
        {lang === "vi"
          ? "Hệ thống chỉ thu thập thông số kỹ thuật: Trạng thái Online, % CPU/RAM/Ổ đĩa, phiên bản hệ điều hành và chính sách bảo vệ. TUYỆT ĐỐI KHÔNG ghi phím gõ (keylogger), KHÔNG quay/chụp màn hình, KHÔNG đọc camera/micro, KHÔNG xem lịch sử duyệt web hay dữ liệu cá nhân."
          : "Online status, CPU/RAM/disk utilization, operating-system version, Agent version, and published policy events. It does not collect keystrokes, screen captures, personal content, credentials, or browsing history."}
      </div>

      <section className="metrics" aria-label="Assigned device summary">
        <div className="metric">
          <span>{lang === "vi" ? "Thiết bị được giao" : "Assigned device"}</span>
          <strong className="metric-text">{device.name}</strong>
        </div>
        <div className="metric">
          <span>{t("status")}</span>
          <strong className="metric-text" style={{ color: device.isOnline ? "var(--accent)" : "var(--danger)" }}>
            {device.isOnline ? t("online") : t("offline")}
          </strong>
        </div>
        <div className="metric">
          <span>{t("appliedPolicy")}</span>
          <strong className="metric-text">{data.appliedPolicy ?? t("none")}</strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Heartbeat gần nhất" : "Last heartbeat"}</span>
          <strong className="metric-text">
            {device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleTimeString() : t("none")}
          </strong>
        </div>
      </section>

      <div className="panel" style={{ marginBottom: "24px" }}>
        <h2>{lang === "vi" ? "Thông số kỹ thuật" : "Technical details"}</h2>
        <dl className="device-facts">
          <div>
            <dt>{t("osVersion")}</dt>
            <dd>{device.osVersion}</dd>
          </div>
          <div>
            <dt>{t("agentVersion")}</dt>
            <dd><code>{device.agentVersion}</code></dd>
          </div>
        </dl>
      </div>

      <div className="panel action-history">
        <h2>{t("recentActionsTitle")}</h2>
        {data.recentActions.length === 0 ? (
          <p className="subtitle">{t("noRecentActions")}</p>
        ) : (
          <ul>
            {data.recentActions.map((action, index) => (
              <li key={`${action.createdAt}-${index}`}>
                <strong>{action.action}</strong>
                <span>
                  {action.outcome} · {new Date(action.createdAt).toLocaleString()}
                </span>
                <small>{action.reason}</small>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}
