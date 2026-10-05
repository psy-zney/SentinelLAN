"use client";

import { useEffect, useRef, useState } from "react";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type {
  MyDeviceDetailDto,
  MyDeviceTelemetryDto,
  IncidentSummaryDto
} from "@/types/api";

export function MyDeviceView() {
  const { t, lang } = useTranslation();
  const [device, setDevice] = useState<MyDeviceDetailDto | null>(null);
  const [telemetry, setTelemetry] = useState<MyDeviceTelemetryDto[]>([]);
  const [incidents, setIncidents] = useState<IncidentSummaryDto[]>([]);
  const [error, setError] = useState("");
  const [empty, setEmpty] = useState(false);
  const [loading, setLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);
  const incidentIdempotencyKey = useRef<string | null>(null);

  // Report Incident Modal State
  const [reportOpen, setReportOpen] = useState(false);
  const [reportTitle, setReportTitle] = useState("");
  const [reportDesc, setReportDesc] = useState("");
  const [reportSeverity, setReportSeverity] = useState("Medium");
  const [reporting, setReporting] = useState(false);
  const [reportSuccessMessage, setReportSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const client = new ApiClient();
    Promise.all([
      client.myDevice(),
      client.myDeviceTelemetry(10).catch(() => []),
      client.myDeviceIncidents().catch(() => [])
    ])
      .then(([dev, tele, incs]) => {
        if (!active) return;
        setDevice(dev);
        setTelemetry(tele);
        setIncidents(incs);
        setError("");
        setEmpty(false);
        setLoading(false);
      })
      .catch((cause) => {
        if (!active) return;
        setLoading(false);
        if (cause instanceof ApiError && cause.status === 404) {
          setDevice(null);
          setError("");
          setEmpty(true);
          return;
        }
        setDevice(null);
        setEmpty(false);
        setError(
          lang === "vi"
            ? "Chưa thể tải thiết bị được gán. Hãy liên hệ với Quản trị viên IT của bạn."
            : "No assigned device could be loaded. Contact your SentinelLAN administrator."
        );
      });

    return () => {
      active = false;
    };
  }, [lang, reloadKey]);

  async function handleReportIncident(e: React.FormEvent) {
    e.preventDefault();
    if (!reportTitle.trim() || reporting) return;
    setReporting(true);
    setReportSuccessMessage(null);
    incidentIdempotencyKey.current ??= crypto.randomUUID();

    try {
      await new ApiClient().reportMyDeviceIncident({
        title: reportTitle.trim(),
        description: reportDesc.trim() || undefined,
        severity: reportSeverity,
        idempotencyKey: incidentIdempotencyKey.current
      });
      incidentIdempotencyKey.current = null;
      setReportOpen(false);
      setReportTitle("");
      setReportDesc("");
      setReportSuccessMessage(t("issueSubmitted"));

      // Refresh incidents
      const updatedIncidents = await new ApiClient().myDeviceIncidents().catch(() => []);
      setIncidents(updatedIncidents);
    } catch {
      alert(lang === "vi" ? "Không thể gửi báo cáo sự cố. Vui lòng thử lại." : "Failed to submit incident report.");
    } finally {
      setReporting(false);
    }
  }

  if (error) {
    return (
      <div className="panel" role="alert">
        <h2>{t("devices")}</h2>
        <p className="subtitle">{error}</p>
        <button
          className="action"
          type="button"
          onClick={() => {
            setError("");
            setEmpty(false);
            setLoading(true);
            setReloadKey((k) => k + 1);
          }}
        >
          {t("retry")}
        </button>
      </div>
    );
  }

  if (empty) {
    return (
      <div className="panel empty-state" role="status" style={{ textAlign: "center", padding: 32 }}>
        <h2>{lang === "vi" ? "Chưa có máy tính được giao cho bạn" : "No assigned computer"}</h2>
        <p className="subtitle" style={{ maxWidth: 460, margin: "8px auto 20px" }}>
          {lang === "vi"
            ? "Hãy nhờ IT giao máy vào tài khoản của bạn. Thông tin máy tính sẽ xuất hiện ở đây sau khi được giao."
            : "An administrator has not assigned a corporate device to your account yet. When assigned, its health telemetry and profile will appear here."}
        </p>
        <button
          className="action"
          type="button"
          onClick={() => {
            setLoading(true);
            setReloadKey((k) => k + 1);
          }}
        >
          {t("refresh")}
        </button>
      </div>
    );
  }

  if (loading || !device) {
    return <p className="subtitle" role="status">{t("loading")}</p>;
  }

  const latestTelemetry = telemetry[0] ?? null;
  const manifest = device.transparencyManifest;

  return (
    <>
      {/* Top Banner Message */}
      {reportSuccessMessage && (
        <div className="panel" role="status" style={{ background: "#f0fdf4", borderColor: "#bbf7d0", marginBottom: 16, color: "#166534" }}>
          <strong>✓ {reportSuccessMessage}</strong>
        </div>
      )}

      {/* Header Bar with Action Button */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 12, marginBottom: 18 }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", margin: 0 }}>{device.name}</h2>
          <p className="subtitle" style={{ fontSize: ".84rem", margin: "2px 0 0" }}>
            {device.manufacturer ?? "Hãng"} · {device.model ?? device.assetType ?? "Máy tính làm việc"}
          </p>
        </div>
        <button
          type="button"
          className="action"
          onClick={() => { incidentIdempotencyKey.current = null; setReportOpen(true); }}
          style={{ display: "flex", alignItems: "center", gap: 6 }}
        >
          {t("reportMyDeviceIssue")}
        </button>
      </div>

      {/* Key Metric Cards */}
      <section className="metrics employee-summary" aria-label={lang === "vi" ? "Tình trạng máy tính" : "Computer status"}>
        <div className="metric">
          <span>{t("status")}</span>
          <strong className="metric-text" style={{ color: device.isOnline ? "var(--accent)" : "var(--danger)" }}>
            {device.isOnline ? t("online") : t("offline")}
          </strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Kết nối gần nhất" : "Last connection"}</span>
          <strong className="metric-text">
            {device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleTimeString(lang === "vi" ? "vi-VN" : "en-US") : t("none")}
          </strong>
        </div>
      </section>

      <details className="panel device-technical" style={{ marginBottom: 20 }}>
        <summary>{lang === "vi" ? "Thông số máy tính" : "Computer details"}</summary>
        <section className="metrics" aria-label={lang === "vi" ? "Thông số máy tính" : "Computer metrics"}>
        <div className="metric">
          <span>{t("cpuUsage")}</span>
          <strong>{latestTelemetry?.cpuPercent !== null && latestTelemetry?.cpuPercent !== undefined ? `${latestTelemetry.cpuPercent}%` : "—"}</strong>
        </div>
        <div className="metric">
          <span>{t("ramUsage")}</span>
          <strong>{latestTelemetry?.ramPercent !== null && latestTelemetry?.ramPercent !== undefined ? `${latestTelemetry.ramPercent}%` : "—"}</strong>
        </div>
        <div className="metric">
          <span>{t("diskUsage")}</span>
          <strong>{latestTelemetry?.diskPercent !== null && latestTelemetry?.diskPercent !== undefined ? `${latestTelemetry.diskPercent}%` : "—"}</strong>
        </div>
        <div className="metric">
          <span>{t("appliedPolicy")}</span>
          <strong className="metric-text">{device.appliedPolicy ?? t("none")}</strong>
        </div>
        </section>
      </details>

      {/* Technical Profile Card */}
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head">
          <h2>{lang === "vi" ? "Thông tin máy tính" : "Computer information"}</h2>
        </div>
        <dl className="device-facts">
          <div>
            <dt>{t("serialNumber")}</dt>
            <dd style={{ fontFamily: "monospace", fontWeight: 700 }}>{device.serialNumber ?? "Chưa ghi nhận"}</dd>
          </div>
          <div>
            <dt>{t("osVersion")}</dt>
            <dd>{device.osVersion}</dd>
          </div>
          <div>
            <dt>{t("agentVersion")}</dt>
            <dd><code>{device.agentVersion}</code></dd>
          </div>
          <div>
            <dt>{t("location")}</dt>
            <dd>{device.location ?? "Chưa xác định"}</dd>
          </div>
          <div>
            <dt>{lang === "vi" ? "Ngày bàn giao" : "Assigned Date"}</dt>
            <dd>{device.assignedAt ? new Date(device.assignedAt).toLocaleDateString(lang === "vi" ? "vi-VN" : "en-US") : t("none")}</dd>
          </div>
        </dl>
      </div>

      {/* Recent Incidents Panel */}
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head" style={{ justifyContent: "space-between", alignItems: "center" }}>
          <h2>{t("recentIncidents")}</h2>
          <button
            type="button"
            className="action-outline"
            style={{ fontSize: ".76rem", padding: "4px 10px" }}
            onClick={() => setReportOpen(true)}
          >
            + {t("reportMyDeviceIssue")}
          </button>
        </div>

        {incidents.length === 0 ? (
          <p className="subtitle" style={{ fontSize: ".84rem" }}>{t("noIncidentsReported")}</p>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>{lang === "vi" ? "Tiêu đề sự cố" : "Issue Title"}</th>
                  <th>{lang === "vi" ? "Ảnh hưởng công việc" : "Work impact"}</th>
                  <th>{t("status")}</th>
                  <th>{t("created")}</th>
                </tr>
              </thead>
              <tbody>
                {incidents.map((inc) => (
                  <tr key={inc.id}>
                    <td><strong>{inc.title}</strong></td>
                    <td>
                      <span className={`badge ${inc.severity === "Critical" || inc.severity === "High" ? "badge-danger" : "badge-warn"}`}>
                        {lang === "vi" ? ({ Low: "Vẫn làm việc được", Medium: "Công việc bị gián đoạn", High: "Không làm việc được", Critical: "Cần hỗ trợ ngay" } as Record<string, string>)[inc.severity] ?? inc.severity : inc.severity}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${inc.status === "Resolved" || inc.status === "Closed" ? "badge-success" : "badge-neutral"}`}>
                        {lang === "vi" ? ({ Open: "Đã gửi", Acknowledged: "IT đã tiếp nhận", InProgress: "Đang xử lý", Resolved: "Đã xử lý", Closed: "Đã đóng" } as Record<string, string>)[inc.status] ?? inc.status : inc.status}
                      </span>
                    </td>
                    <td>{new Date(inc.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Full Transparency & Privacy Manifest Card */}
      {manifest && (
        <details className="panel privacy-details" style={{ marginBottom: 24 }}>
              <summary>{t("privacyCommitment")}</summary>
              <p className="subtitle" style={{ fontSize: ".82rem" }}>
                {t("transparencyManifest")} · {t("dataRetention")}: <strong>{manifest.dataRetentionDays} {t("days")}</strong>
              </p>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(min(240px, 100%), 1fr))", gap: 16, marginTop: 12 }}>
            {/* Monitored Data */}
            <div style={{ background: "#f8faf9", padding: "14px 16px", borderRadius: 10, border: "1px solid #e2ebe8" }}>
              <strong style={{ color: "var(--accent)", fontSize: ".88rem" }}>✓ {t("privacyCollected")}</strong>
              <ul style={{ margin: "8px 0 0", paddingLeft: 18, fontSize: ".82rem", color: "var(--ink)", lineHeight: 1.6 }}>
                {manifest.collectedTelemetry.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </div>

            {/* Prohibited Boundaries */}
            <div style={{ background: "#fff5f5", padding: "14px 16px", borderRadius: 10, border: "1px solid #fed7d7" }}>
              <strong style={{ color: "#c53030", fontSize: ".88rem" }}>✕ {t("privacyProhibited")}</strong>
              <ul style={{ margin: "8px 0 0", paddingLeft: 18, fontSize: ".82rem", color: "var(--ink)", lineHeight: 1.6 }}>
                {manifest.privacyBoundaries.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </div>
          </div>
        </details>
      )}

      {/* Modal: Report Incident */}
      {reportOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("reportMyDeviceIssue")}</h3>
              <button className="modal-close" onClick={() => setReportOpen(false)}>×</button>
            </div>
            <form onSubmit={handleReportIncident}>
              <p className="subtitle" style={{ margin: "0 0 14px" }}>
                {lang === "vi"
                  ? `Báo hỏng hoặc yêu cầu hỗ trợ kỹ thuật cho máy ${device.name}. Kỹ thuật viên IT sẽ nhận được thông báo.`
                  : `Report a hardware issue or request IT support for ${device.name}.`}
              </p>
              <div className="form-group">
                <label>{lang === "vi" ? "Hiện tượng / Vấn đề gặp phải" : "Issue Summary"}</label>
                <input
                  className="form-input"
                  required
                  placeholder={lang === "vi" ? "VD: Máy khởi động chậm, quạt kêu to, bàn phím hỏng..." : "e.g. Broken screen, overheating, fan noise..."}
                  value={reportTitle}
                  onChange={(e) => setReportTitle(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="work-impact">{lang === "vi" ? "Bạn còn làm việc được không?" : "Can you continue working?"}</label>
                <select
                  id="work-impact"
                  className="form-select"
                  value={reportSeverity}
                  onChange={(e) => setReportSeverity(e.target.value)}
                >
                  <option value="Low">{lang === "vi" ? "Có, tôi vẫn làm việc được" : "Yes, I can keep working"}</option>
                  <option value="Medium">{lang === "vi" ? "Có, nhưng công việc bị gián đoạn" : "Yes, with interruptions"}</option>
                  <option value="High">{lang === "vi" ? "Không, tôi cần IT hỗ trợ" : "No, I need IT help"}</option>
                </select>
              </div>
              <div className="form-group">
                <label>{lang === "vi" ? "Mô tả chi tiết bổ sung" : "Detailed Description (Optional)"}</label>
                <textarea
                  className="form-input"
                  rows={3}
                  placeholder={lang === "vi" ? "Vị trí bàn làm việc, các triệu chứng cụ thể..." : "Specific symptoms, desk location..."}
                  value={reportDesc}
                  onChange={(e) => setReportDesc(e.target.value)}
                />
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setReportOpen(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="action" disabled={reporting || !reportTitle.trim()}>
                  {reporting ? t("loading") : (lang === "vi" ? "Gửi báo cáo" : "Submit Report")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
