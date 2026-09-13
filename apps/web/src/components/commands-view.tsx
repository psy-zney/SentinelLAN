"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { CommandHistoryItem, CommandType, Device } from "@/types/api";

const loadCommands = () => new ApiClient().commands();

export function CommandsView() {
  const { t, lang } = useTranslation();
  const { data: commands, error, refresh } = useLiveQuery<CommandHistoryItem[]>(loadCommands);
  const [isDispatching, setIsDispatching] = useState(false);
  const [devices, setDevices] = useState<Device[]>([]);
  const [loadingDevices, setLoadingDevices] = useState(false);

  // Form state
  const [selectedDeviceId, setSelectedDeviceId] = useState("");
  const [commandType, setCommandType] = useState<CommandType>("SimulateLock");
  const [serviceName, setServiceName] = useState("docker");
  const [notificationText, setNotificationText] = useState("");
  const [reason, setReason] = useState("");
  const [validForSeconds, setValidForSeconds] = useState(300);
  const [confirmed, setConfirmed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [statusMessage, setStatusMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

  const handleOpenDispatch = useCallback(async () => {
    setIsDispatching(true);
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

  const handleDispatchCommand = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedDeviceId || !reason.trim() || !confirmed || submitting) return;
    setSubmitting(true);
    setStatusMessage(null);

    let parameter: string | undefined = undefined;
    if (commandType === "RestartService") {
      parameter = serviceName;
    } else if (commandType === "ShowNotification") {
      parameter = notificationText.trim();
    }

    try {
      await new ApiClient().createCommand(
        selectedDeviceId,
        commandType,
        reason.trim(),
        confirmed,
        Number(validForSeconds),
        parameter
      );
      setStatusMessage({ text: t("commandDispatchedSuccess"), type: "success" });
      setIsDispatching(false);
      setReason("");
      setConfirmed(false);
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to dispatch command.", type: "error" });
    } finally {
      setSubmitting(false);
    }
  };

  const getStatusBadge = (status: string, succeeded?: boolean | null) => {
    if (status === "Succeeded" || succeeded === true) return <span className="badge badge-success">{t("succeeded")}</span>;
    if (status === "Failed" || succeeded === false) return <span className="badge badge-danger">{t("failed")}</span>;
    if (status === "Delivered") return <span className="badge badge-info">{t("delivered")}</span>;
    if (status === "Pending") return <span className="badge badge-warn">{t("pending")}</span>;
    return <span className="badge badge-neutral">{status}</span>;
  };

  if (error) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{t("commands")}</h2></div>
        <p className="subtitle">{t("error")}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  if (!commands) return <p className="subtitle" role="status">{t("loading")}</p>;

  return (
    <>
      <section className="metrics" aria-label="Command activity metrics">
        <div className="metric">
          <span>{lang === "vi" ? "Tổng lệnh đã phát" : "Total Dispatched"}</span>
          <strong>{commands.length}</strong>
        </div>
        <div className="metric">
          <span>{t("pending")}</span>
          <strong style={{ color: "var(--warn)" }}>
            {commands.filter((c) => c.status === "Pending").length}
          </strong>
        </div>
        <div className="metric">
          <span>{t("succeeded")}</span>
          <strong style={{ color: "var(--accent)" }}>
            {commands.filter((c) => c.status === "Succeeded" || c.succeeded === true).length}
          </strong>
        </div>
        <div className="metric">
          <span>{t("failed")} / {t("expired")}</span>
          <strong style={{ color: "var(--danger)" }}>
            {commands.filter((c) => c.status === "Failed" || c.status === "Expired" || c.succeeded === false).length}
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
            <h2>{t("commandHistory")}</h2>
            <p className="subtitle" style={{ fontSize: ".82rem" }}>
              {t("subtitleCommands")}
            </p>
          </div>
          <button className="action" onClick={handleOpenDispatch}>
            {t("dispatchCommandBtn")}
          </button>
        </div>

        {commands.length === 0 ? (
          <div className="empty-state">
            <p>{lang === "vi" ? "Chưa có lệnh điều khiển nào được phát." : "No commands issued yet."}</p>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>{t("commandTarget")}</th>
                  <th>{t("commandType")}</th>
                  <th>{lang === "vi" ? "Tham số" : "Parameter"}</th>
                  <th>{t("commandReason")}</th>
                  <th>{t("commandStatus")}</th>
                  <th>{t("commandResult")}</th>
                  <th>{t("commandIssuedAt")}</th>
                </tr>
              </thead>
              <tbody>
                {commands.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <code style={{ fontSize: ".78rem" }}>{c.id.substring(0, 8)}</code>
                    </td>
                    <td>
                      <strong>{c.deviceName}</strong>
                    </td>
                    <td>
                      <span className="badge badge-info">{c.type}</span>
                    </td>
                    <td>
                      {c.parameter ? (
                        <code style={{ fontSize: ".78rem", background: "#f1f5f9", padding: "2px 6px", borderRadius: 4 }}>
                          {c.parameter}
                        </code>
                      ) : (
                        <span style={{ color: "var(--muted)" }}>—</span>
                      )}
                    </td>
                    <td style={{ maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {c.reason}
                    </td>
                    <td>{getStatusBadge(c.status, c.succeeded)}</td>
                    <td style={{ maxWidth: 180, fontSize: ".8rem" }}>
                      {c.resultMessage ?? (c.succeeded === true ? t("succeeded") : t("pending"))}
                    </td>
                    <td>{new Date(c.issuedAt).toLocaleTimeString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Dispatch Command Modal */}
      {isDispatching && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("commandModalTitle")}</h3>
              <button className="modal-close" onClick={() => setIsDispatching(false)} aria-label="Close">×</button>
            </div>
            {loadingDevices ? (
              <p className="subtitle">{t("loading")}</p>
            ) : devices.length === 0 ? (
              <p className="subtitle">{lang === "vi" ? "Không có thiết bị nào sẵn sàng nhận lệnh." : "No registered devices available."}</p>
            ) : (
              <form onSubmit={handleDispatchCommand}>
                <div className="form-group">
                  <label htmlFor="cmd-device">{t("commandTarget")}</label>
                  <select
                    id="cmd-device"
                    className="form-select"
                    value={selectedDeviceId}
                    onChange={(e) => setSelectedDeviceId(e.target.value)}
                  >
                    {devices.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name} ({d.osVersion}) - {d.isOnline ? t("online") : t("offline")}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group">
                  <label htmlFor="cmd-type">{t("commandTypeLabel")}</label>
                  <select
                    id="cmd-type"
                    className="form-select"
                    value={commandType}
                    onChange={(e) => setCommandType(e.target.value as CommandType)}
                  >
                    <option value="SimulateLock">{lang === "vi" ? "Khóa màn hình (LockWorkStation Win32 thật / Lab)" : "Workstation Lock (Win32 LockWorkStation)"}</option>
                    <option value="RestartService">{lang === "vi" ? "Khởi động lại Service (Cloud Server / VPS Linux)" : "Restart Service (Cloud Server / VPS)"}</option>
                    <option value="CollectTelemetryNow">{lang === "vi" ? "Thu thập Telemetry tức thì" : "Collect Telemetry Immediately"}</option>
                    <option value="RefreshPolicy">{lang === "vi" ? "Làm mới chính sách bảo vệ" : "Refresh Applied Policy"}</option>
                    <option value="SimulateNetworkIsolation">{lang === "vi" ? "Mô phỏng cách ly mạng" : "Simulate Network Isolation"}</option>
                    <option value="ShowNotification">{lang === "vi" ? "Hiển thị thông báo người dùng" : "Display User Notification"}</option>
                  </select>
                </div>

                {commandType === "RestartService" && (
                  <div className="form-group">
                    <label htmlFor="cmd-svc">{lang === "vi" ? "Tên Service (Chỉ trong Allow-list an toàn)" : "Service Name (Allow-listed only)"}</label>
                    <select
                      id="cmd-svc"
                      className="form-select"
                      value={serviceName}
                      onChange={(e) => setServiceName(e.target.value)}
                    >
                      <option value="docker">docker</option>
                      <option value="nginx">nginx</option>
                      <option value="caddy">caddy</option>
                    </select>
                  </div>
                )}

                {commandType === "ShowNotification" && (
                  <div className="form-group">
                    <label htmlFor="cmd-notif">{lang === "vi" ? "Nội dung thông báo" : "Notification Message"}</label>
                    <input
                      id="cmd-notif"
                      className="form-input"
                      required
                      placeholder={lang === "vi" ? "VD: Quản trị viên bảo trì sau 10 phút" : "e.g. Scheduled IT maintenance"}
                      value={notificationText}
                      onChange={(e) => setNotificationText(e.target.value)}
                    />
                  </div>
                )}

                <div className="form-group">
                  <label htmlFor="cmd-reason">{t("commandReasonLabel")}</label>
                  <input
                    id="cmd-reason"
                    className="form-input"
                    required
                    placeholder={t("commandReasonPlaceholder")}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                  <small>{lang === "vi" ? "Lý do được lưu bất biến vào nhật ký kiểm toán cùng danh tính của bạn." : "Appended to tamper-evident audit log along with your operator ID."}</small>
                </div>

                <div className="form-group">
                  <label htmlFor="cmd-expiry">{lang === "vi" ? "Thời hạn lệnh hiệu lực (Giây)" : "Validity Window (Seconds)"}</label>
                  <input
                    id="cmd-expiry"
                    type="number"
                    min="30"
                    max="900"
                    className="form-input"
                    value={validForSeconds}
                    onChange={(e) => setValidForSeconds(Number(e.target.value))}
                  />
                </div>

                <div className="form-group" style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 14 }}>
                  <input
                    id="cmd-confirm"
                    type="checkbox"
                    checked={confirmed}
                    onChange={(e) => setConfirmed(e.target.checked)}
                    required
                  />
                  <label htmlFor="cmd-confirm" style={{ cursor: "pointer", fontSize: ".82rem" }}>
                    {t("commandConfirmCheck")}
                  </label>
                </div>

                <div className="btn-row">
                  <button type="button" className="action-outline" onClick={() => setIsDispatching(false)}>{t("cancel")}</button>
                  <button type="submit" className="action" disabled={submitting || !confirmed}>
                    {submitting ? t("dispatching") : t("commandSignAndDispatch")}
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
