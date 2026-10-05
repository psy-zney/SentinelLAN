"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import type { CommandType, Device } from "@/types/api";
import { useTranslation } from "@/lib/i18n";
import { ApiClient } from "@/lib/api-client";

export function DeviceTable({ initialDevices }: { initialDevices: Device[] }) {
  const { t, lang } = useTranslation();
  const devices = initialDevices;

  // Modals state
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);
  const [commandModalOpen, setCommandModalOpen] = useState(false);
  const [incidentModalOpen, setIncidentModalOpen] = useState(false);

  // Command modal form state
  const [cmdType, setCmdType] = useState<CommandType>("ShowNotification");
  const [cmdReason, setCmdReason] = useState("");
  const [cmdParam, setCmdParam] = useState("");
  const [cmdStatus, setCmdStatus] = useState<string | null>(null);
  const [cmdLoading, setCmdLoading] = useState(false);

  // Incident modal form state
  const [incTitle, setIncTitle] = useState("");
  const [incDesc, setIncDesc] = useState("");
  const [incSeverity, setIncSeverity] = useState("Medium");
  const [incStatus, setIncStatus] = useState<string | null>(null);
  const [incLoading, setIncLoading] = useState(false);
  const incidentIdempotencyKey = useRef<string | null>(null);

  function openCommand(device: Device) {
    setSelectedDevice(device);
    setCmdType("ShowNotification");
    setCmdReason("");
    setCmdParam("");
    setCmdStatus(null);
    setCommandModalOpen(true);
  }

  function openIncident(device: Device) {
    setSelectedDevice(device);
    setIncTitle("");
    setIncDesc("");
    setIncSeverity("Medium");
    setIncStatus(null);
    incidentIdempotencyKey.current = null;
    setIncidentModalOpen(true);
  }

  async function handleSendCommand(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedDevice || !cmdReason.trim()) return;
    setCmdLoading(true);
    setCmdStatus(null);
    try {
      await new ApiClient().createCommand({
        deviceId: selectedDevice.id,
        type: cmdType,
        reason: cmdReason.trim(),
        confirmed: true,
        parameter: cmdParam.trim() || undefined,
        validForSeconds: 120
      });
      setCmdStatus(lang === "vi" ? "Đã gửi lệnh thành công!" : "Command dispatched successfully!");
      setTimeout(() => {
        setCommandModalOpen(false);
        setCmdStatus(null);
      }, 1500);
    } catch {
      setCmdStatus(lang === "vi" ? "Lỗi gửi lệnh điều khiển. Vui lòng kiểm tra quyền." : "Failed to dispatch command. Check permissions.");
    } finally {
      setCmdLoading(false);
    }
  }

  async function handleReportIncident(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedDevice || !incTitle.trim()) return;
    setIncLoading(true);
    setIncStatus(null);
    incidentIdempotencyKey.current ??= crypto.randomUUID();
    try {
      await new ApiClient().createIncident({
        deviceId: selectedDevice.id,
        title: incTitle.trim(),
        description: incDesc.trim() || undefined,
        severity: incSeverity,
        idempotencyKey: incidentIdempotencyKey.current
      });
      incidentIdempotencyKey.current = null;
      setIncStatus(lang === "vi" ? "Đã tiếp nhận báo hỏng thành công!" : "Incident report registered successfully!");
      setTimeout(() => {
        setIncidentModalOpen(false);
        setIncStatus(null);
      }, 1500);
    } catch {
      setIncStatus(lang === "vi" ? "Lỗi tạo phiếu báo hỏng." : "Failed to register incident.");
    } finally {
      setIncLoading(false);
    }
  }

  return (
    <div className="panel">
      <div className="panel-head">
        <h2>{t("managedDevices")}</h2>
        <Link href="/devices" style={{ fontSize: ".85rem", color: "var(--accent)", fontWeight: 700 }}>
          {lang === "vi" ? "Xem tất cả" : "View inventory →"}
        </Link>
      </div>
      <table>
        <thead>
          <tr>
            <th>{t("deviceName")}</th>
            <th>{t("status")}</th>
            <th>{lang === "vi" ? "Người sử dụng" : "Assignment"}</th>
            <th>{t("osVersion")}</th>
            <th>{t("lastSeen")}</th>
            <th style={{ textAlign: "right" }}>{t("actions")}</th>
          </tr>
        </thead>
        <tbody>
          {devices.map((device) => {
            return (
              <tr key={device.id}>
                <td>
                  <Link href={`/devices/${device.id}`} style={{ fontWeight: 600, display: "flex", alignItems: "center", gap: 6 }}>
                    <span></span>
                    <span>{device.name}</span>
                  </Link>
                </td>
                <td>
                  <span className={`status ${device.isOnline ? "" : "offline"}`}>
                    <span className="dot" />
                    {device.isOnline ? t("online") : t("offline")}
                  </span>
                </td>
                <td>
                  {device.isRevoked ? (
                    <span className="badge badge-danger">{lang === "vi" ? "Đã thu hồi" : "Revoked"}</span>
                  ) : device.assignedUserId ? (
                    <span className="badge badge-info">{lang === "vi" ? "Đã giao máy" : "Assigned"}</span>
                  ) : (
                    <span className="badge badge-neutral">{lang === "vi" ? "Chưa giao máy" : "Unassigned"}</span>
                  )}
                </td>
                <td>{device.osVersion}</td>
                <td>
                  {device.lastSeenAt
                    ? new Date(device.lastSeenAt).toLocaleTimeString(lang === "vi" ? "vi-VN" : "en-US")
                    : t("none")}
                </td>
                <td style={{ textAlign: "right" }}>
                  <div style={{ display: "inline-flex", gap: 6, alignItems: "center" }}>
                    <Link href={`/devices/${device.id}`} className="action-outline" title={t("qrCode")}>QR</Link>
                    <button
                      type="button"
                      className="action-outline"
                      style={{ padding: "4px 8px", fontSize: ".76rem" }}
                      onClick={() => openCommand(device)}
                      title={t("quickCommand")}
                    >
                      {lang === "vi" ? "Thao tác" : "Action"}
                    </button>
                    <button
                      type="button"
                      className="action-outline"
                      style={{ padding: "4px 8px", fontSize: ".76rem", color: "var(--warn)" }}
                      onClick={() => openIncident(device)}
                      title={t("reportIncident")}
                    >
                      {lang === "vi" ? "Báo hỏng" : "Issue"}
                    </button>
                    <Link
                      href={`/devices/${device.id}`}
                      className="action-sm"
                      style={{ padding: "4px 10px", fontSize: ".76rem" }}
                    >
                      {t("details")}
                    </Link>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* Modal 2: Quick Command Dispatch */}
      {commandModalOpen && selectedDevice && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("quickCommand")}: {selectedDevice.name}</h3>
              <button className="modal-close" onClick={() => setCommandModalOpen(false)}>×</button>
            </div>
            <form onSubmit={handleSendCommand}>
              <div className="form-group">
                <label>{lang === "vi" ? "Loại lệnh an toàn" : "Safe Command Type"}</label>
                <select
                  className="form-select"
                  value={cmdType}
                  onChange={(e) => setCmdType(e.target.value as CommandType)}
                >
                  <option value="ShowNotification">{lang === "vi" ? "Gửi thông báo màn hình (ShowNotification)" : "Show Notification"}</option>
                  <option value="CollectTelemetryNow">{lang === "vi" ? "Cập nhật thông số máy" : "Collect Telemetry Now"}</option>
                  <option value="RefreshPolicy">{lang === "vi" ? "Làm mới chính sách (RefreshPolicy)" : "Refresh Policy"}</option>
                  <option value="LockWorkstation">{lang === "vi" ? "Khóa Windows thật (máy lab được phép)" : "Lock Windows (authorized lab)"}</option>
                  <option value="SimulateLock">{lang === "vi" ? "Mô phỏng khóa màn hình (SimulateLock)" : "Simulate Screen Lock"}</option>
                  <option value="SimulateNetworkIsolation">{lang === "vi" ? "Mô phỏng cách ly mạng (SimulateNetworkIsolation)" : "Simulate Network Isolation"}</option>
                </select>
              </div>

              {cmdType === "ShowNotification" && (
                <div className="form-group">
                  <label>{lang === "vi" ? "Nội dung thông báo" : "Notification message"}</label>
                  <input
                    className="form-input"
                    placeholder={lang === "vi" ? "Nhập thông báo gửi đến nhân viên..." : "Message to display on endpoint..."}
                    value={cmdParam}
                    onChange={(e) => setCmdParam(e.target.value)}
                  />
                </div>
              )}

              <div className="form-group">
                <label>{lang === "vi" ? "Lý do ban hành lệnh (Kiểm toán bắt buộc)" : "Reason for action (Required audit)"}</label>
                <textarea
                  className="form-input"
                  required
                  rows={2}
                  placeholder={lang === "vi" ? "VD: Kiểm tra phản hồi bảo mật định kỳ..." : "e.g. Periodic security inspection..."}
                  value={cmdReason}
                  onChange={(e) => setCmdReason(e.target.value)}
                />
              </div>

              {cmdStatus && (
                <p style={{ fontSize: ".84rem", color: cmdStatus.includes("thành công") || cmdStatus.includes("success") ? "var(--accent)" : "var(--danger)" }}>
                  {cmdStatus}
                </p>
              )}

              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setCommandModalOpen(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="action" disabled={cmdLoading || !cmdReason.trim()}>
                  {cmdLoading ? t("loading") : (lang === "vi" ? "Gửi thao tác" : "Sign & Dispatch")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 3: Quick Incident Report */}
      {incidentModalOpen && selectedDevice && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("reportIncident")}: {selectedDevice.name}</h3>
              <button className="modal-close" onClick={() => setIncidentModalOpen(false)}>×</button>
            </div>
            <form onSubmit={handleReportIncident}>
              <div className="form-group">
                <label>{lang === "vi" ? "Tiêu đề sự cố / Hiện tượng hỏng" : "Incident Title / Symptom"}</label>
                <input
                  className="form-input"
                  required
                  placeholder={lang === "vi" ? "VD: Quạt kêu to, màn hình nhấp nháy, pin chai..." : "e.g. Fan rattling, screen flickering..."}
                  value={incTitle}
                  onChange={(e) => setIncTitle(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>{lang === "vi" ? "Mức độ nghiêm trọng" : "Severity"}</label>
                <select
                  className="form-select"
                  value={incSeverity}
                  onChange={(e) => setIncSeverity(e.target.value)}
                >
                  <option value="Low">Low - {lang === "vi" ? "Nhẹ (Chưa ảnh hưởng công việc)" : "Minor"}</option>
                  <option value="Medium">Medium - {lang === "vi" ? "Trung bình (Ảnh hưởng một phần)" : "Moderate"}</option>
                  <option value="High">High - {lang === "vi" ? "Cao (Gặp khó khăn khi vận hành)" : "Major"}</option>
                  <option value="Critical">Critical - {lang === "vi" ? "Khẩn cấp (Thiết bị dừng hoạt động hoàn toàn)" : "Critical Downtime"}</option>
                </select>
              </div>

              <div className="form-group">
                <label>{lang === "vi" ? "Mô tả chi tiết" : "Detailed Description"}</label>
                <textarea
                  className="form-input"
                  rows={3}
                  placeholder={lang === "vi" ? "Mô tả thêm hiện tượng, mã lỗi, hoặc thời điểm phát sinh..." : "Provide additional context, error codes..."}
                  value={incDesc}
                  onChange={(e) => setIncDesc(e.target.value)}
                />
              </div>

              {incStatus && (
                <p style={{ fontSize: ".84rem", color: incStatus.includes("thành công") || incStatus.includes("success") ? "var(--accent)" : "var(--danger)" }}>
                  {incStatus}
                </p>
              )}

              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setIncidentModalOpen(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="action" disabled={incLoading || !incTitle.trim()}>
                  {incLoading ? t("loading") : (lang === "vi" ? "Gửi phiếu báo hỏng" : "Submit Ticket")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
