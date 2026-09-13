"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { AlertItem, Device } from "@/types/api";

const loadAlerts = () => new ApiClient().alerts();

export function AlertsView() {
  const { t, lang } = useTranslation();
  const { data: alerts, error, refresh } = useLiveQuery<AlertItem[]>(loadAlerts);
  const [filter, setFilter] = useState<"all" | "open" | "resolved">("open");
  const [isTriggering, setIsTriggering] = useState(false);
  const [devices, setDevices] = useState<Device[]>([]);
  const [loadingDevices, setLoadingDevices] = useState(false);

  // Form state for test alert
  const [selectedDeviceId, setSelectedDeviceId] = useState("");
  const [severity, setSeverity] = useState<"Info" | "Warning" | "Critical">("Warning");
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

  const handleOpenTrigger = useCallback(async () => {
    setIsTriggering(true);
    setLoadingDevices(true);
    try {
      const list = await new ApiClient().devices();
      setDevices(list);
      if (list.length > 0) setSelectedDeviceId(list[0].id);
    } catch {
      setStatusMessage({ text: "Failed to load device list.", type: "error" });
    } finally {
      setLoadingDevices(false);
    }
  }, []);

  const handleAcknowledge = async (id: string) => {
    setActionInProgress(id);
    try {
      await new ApiClient().acknowledgeAlert(id);
      setStatusMessage({ text: t("alertAckSuccess"), type: "success" });
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to acknowledge alert.", type: "error" });
    } finally {
      setActionInProgress(null);
    }
  };

  const handleResolve = async (id: string) => {
    setActionInProgress(id);
    try {
      await new ApiClient().resolveAlert(id);
      setStatusMessage({ text: t("alertResolveSuccess"), type: "success" });
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to resolve alert.", type: "error" });
    } finally {
      setActionInProgress(null);
    }
  };

  const handleCreateAlert = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!message.trim() || submitting) return;
    setSubmitting(true);
    setStatusMessage(null);
    try {
      await new ApiClient().createAlert({
        deviceId: selectedDeviceId || null,
        severity,
        message: message.trim()
      });
      setStatusMessage({ text: t("alertTriggerSuccess"), type: "success" });
      setIsTriggering(false);
      setMessage("");
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to trigger alert.", type: "error" });
    } finally {
      setSubmitting(false);
    }
  };

  if (error) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{t("alerts")}</h2></div>
        <p className="subtitle">{t("error")}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  if (!alerts) return <p className="subtitle" role="status">{t("loading")}</p>;

  const filteredAlerts = alerts.filter((a) => {
    if (filter === "open") return a.isOpen;
    if (filter === "resolved") return !a.isOpen;
    return true;
  });

  const getSeverityBadge = (s: string) => {
    if (s === "Critical") return <span className="badge badge-danger">{t("critical")}</span>;
    if (s === "Warning") return <span className="badge badge-warn">{t("warning")}</span>;
    return <span className="badge badge-info">{t("info")}</span>;
  };

  return (
    <>
      <section className="metrics" aria-label="Security alert metrics">
        <div className="metric">
          <span>{lang === "vi" ? "Cảnh báo đang mở" : "Active Open Alerts"}</span>
          <strong style={{ color: alerts.filter((a) => a.isOpen).length > 0 ? "var(--danger)" : "inherit" }}>
            {alerts.filter((a) => a.isOpen).length}
          </strong>
        </div>
        <div className="metric">
          <span>{t("critical")}</span>
          <strong style={{ color: "var(--danger)" }}>
            {alerts.filter((a) => a.isOpen && a.severity === "Critical").length}
          </strong>
        </div>
        <div className="metric">
          <span>{t("warning")}</span>
          <strong style={{ color: "var(--warn)" }}>
            {alerts.filter((a) => a.isOpen && a.severity === "Warning").length}
          </strong>
        </div>
        <div className="metric">
          <span>{t("resolved")}</span>
          <strong style={{ color: "var(--accent)" }}>
            {alerts.filter((a) => !a.isOpen).length}
          </strong>
        </div>
      </section>

      {statusMessage && (
        <div
          className={`panel ${statusMessage.type === "error" ? "privacy" : ""}`}
          style={{ marginBottom: "1.2rem", padding: "12px 18px" }}
          role="status"
        >
          <strong>{statusMessage.text}</strong>
        </div>
      )}

      <div className="panel">
        <div className="panel-head">
          <div>
            <h2>{t("alerts")}</h2>
            <p className="subtitle" style={{ fontSize: ".82rem" }}>
              {t("subtitleAlerts")}
            </p>
          </div>
          <button className="action" onClick={handleOpenTrigger}>
            {t("triggerAlertBtn")}
          </button>
        </div>

        <div className="tabs-bar">
          <button
            className={`tab-btn ${filter === "open" ? "active" : ""}`}
            onClick={() => setFilter("open")}
          >
            {t("open")} ({alerts.filter((a) => a.isOpen).length})
          </button>
          <button
            className={`tab-btn ${filter === "all" ? "active" : ""}`}
            onClick={() => setFilter("all")}
          >
            {t("all")} ({alerts.length})
          </button>
          <button
            className={`tab-btn ${filter === "resolved" ? "active" : ""}`}
            onClick={() => setFilter("resolved")}
          >
            {t("resolved")} ({alerts.filter((a) => !a.isOpen).length})
          </button>
        </div>

        {filteredAlerts.length === 0 ? (
          <div className="empty-state">
            <p>{t("noAlertsFound")}</p>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>{t("status")}</th>
                  <th>{lang === "vi" ? "Nguồn cảnh báo" : "Source Endpoint"}</th>
                  <th>{lang === "vi" ? "Nội dung sự cố" : "Incident Message"}</th>
                  <th>{lang === "vi" ? "Tiến độ" : "Progress"}</th>
                  <th>{t("created")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filteredAlerts.map((a) => (
                  <tr key={a.id}>
                    <td>{getSeverityBadge(a.severity)}</td>
                    <td>
                      <strong>{a.deviceName ?? (lang === "vi" ? "Hệ thống chung" : "System")}</strong>
                    </td>
                    <td>{a.message}</td>
                    <td>
                      {!a.isOpen ? (
                        <span className="badge badge-success">{t("resolved")}</span>
                      ) : a.acknowledgedAt ? (
                        <span className="badge badge-warn">{t("acknowledged")}</span>
                      ) : (
                        <span className="badge badge-danger">{t("open")}</span>
                      )}
                    </td>
                    <td>{new Date(a.createdAt).toLocaleString()}</td>
                    <td>
                      {a.isOpen && (
                        <div style={{ display: "flex", gap: 6 }}>
                          {!a.acknowledgedAt && (
                            <button
                              className="action-sm action-outline"
                              onClick={() => handleAcknowledge(a.id)}
                              disabled={actionInProgress === a.id}
                            >
                              {t("ackBtn")}
                            </button>
                          )}
                          <button
                            className="action-sm"
                            onClick={() => handleResolve(a.id)}
                            disabled={actionInProgress === a.id}
                          >
                            {t("resolveBtn")}
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Trigger Alert Modal */}
      {isTriggering && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("triggerAlertModalTitle")}</h3>
              <button className="modal-close" onClick={() => setIsTriggering(false)} aria-label="Close">×</button>
            </div>
            {loadingDevices ? (
              <p className="subtitle">{t("loading")}</p>
            ) : (
              <form onSubmit={handleCreateAlert}>
                <div className="form-group">
                  <label htmlFor="alert-device">{t("alertDeviceLabel")}</label>
                  <select
                    id="alert-device"
                    className="form-select"
                    value={selectedDeviceId}
                    onChange={(e) => setSelectedDeviceId(e.target.value)}
                  >
                    <option value="">{lang === "vi" ? "(Toàn tổ chức / Hạ tầng chung)" : "(Organization-wide / Infrastructure)"}</option>
                    {devices.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name} ({d.osVersion})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group">
                  <label htmlFor="alert-severity">{t("alertSeverityLabel")}</label>
                  <select
                    id="alert-severity"
                    className="form-select"
                    value={severity}
                    onChange={(e) => setSeverity(e.target.value as "Info" | "Warning" | "Critical")}
                  >
                    <option value="Info">Info - {lang === "vi" ? "Thông báo thông thường" : "Informational notice"}</option>
                    <option value="Warning">Warning - {lang === "vi" ? "Bất thường tài nguyên hoặc tuân thủ" : "Resource or compliance anomaly"}</option>
                    <option value="Critical">Critical - {lang === "vi" ? "Vi phạm an ninh nghiêm trọng khẩn cấp" : "Urgent security violation"}</option>
                  </select>
                </div>

                <div className="form-group">
                  <label htmlFor="alert-msg">{t("alertMessageLabel")}</label>
                  <input
                    id="alert-msg"
                    className="form-input"
                    required
                    placeholder={t("alertMessagePlaceholder")}
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                  />
                </div>

                <div className="btn-row">
                  <button type="button" className="action-outline" onClick={() => setIsTriggering(false)}>{t("cancel")}</button>
                  <button type="submit" className="action" disabled={submitting}>
                    {submitting ? t("loading") : t("alertTriggerSubmit")}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </>
  );
}
