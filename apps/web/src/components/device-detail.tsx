"use client";

import { useCallback, useRef, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useCurrentSession } from "@/components/app-shell";
import { useTranslation } from "@/lib/i18n";
import { generateQrMatrix, generateQrSvgPath } from "@/lib/qr-generator";
import type {
  DeviceAssetDetail,
  AssetTimelineItem,
  IncidentItem,
  WorkOrderItem,
  UserItem,
  CommandType,
  DeviceQrLabelDto
} from "@/types/api";

export function DeviceDetail({ id }: { id: string }) {
  const { t, lang } = useTranslation();
  const session = useCurrentSession();
  const isAdmin = session?.role === "Admin";
  const isTechnician = session?.role === "Admin" || session?.role === "Technician";

  // Tab state
  const [activeTab, setActiveTab] = useState<"overview" | "timeline" | "cmms" | "tco">("overview");

  // Status message
  const [message, setMessage] = useState("");
  const [sending, setSending] = useState(false);

  // Forms / Modals state
  const [editProfileOpen, setEditProfileOpen] = useState(false);
  const [reportIncOpen, setReportIncOpen] = useState(false);
  const incidentIdempotencyKey = useRef<string | null>(null);
  const [createWoOpen, setCreateWoOpen] = useState(false);
  const [completeWoOpen, setCompleteWoOpen] = useState(false);
  const [selectedWo, setSelectedWo] = useState<WorkOrderItem | null>(null);

  // Assignment / Revoke / Command state
  const [assignedUserId, setAssignedUserId] = useState<string | undefined>(undefined);
  const [assignmentReason, setAssignmentReason] = useState("");
  const [assignmentConfirmed, setAssignmentConfirmed] = useState(false);
  const [revokeReason, setRevokeReason] = useState("");
  const [revokeConfirmed, setRevokeConfirmed] = useState(false);
  const [commandType, setCommandType] = useState<CommandType>("SimulateLock");
  const [commandReason, setCommandReason] = useState("");
  const [commandConfirmed, setCommandConfirmed] = useState(false);
  const [users, setUsers] = useState<UserItem[]>([]);

  // Edit Profile Form State
  const [editSerial, setEditSerial] = useState("");
  const [editManufacturer, setEditManufacturer] = useState("");
  const [editModel, setEditModel] = useState("");
  const [editAssetType, setEditAssetType] = useState("Laptop");
  const [editCampus, setEditCampus] = useState("");
  const [editBuilding, setEditBuilding] = useState("");
  const [editFloor, setEditFloor] = useState("");
  const [editRoom, setEditRoom] = useState("");
  const [editCost, setEditCost] = useState<number | undefined>(undefined);
  const [editReason, setEditReason] = useState("");
  const [editConfirmed, setEditConfirmed] = useState(false);

  // New Incident Form State
  const [newIncTitle, setNewIncTitle] = useState("");
  const [newIncDesc, setNewIncDesc] = useState("");
  const [newIncSeverity, setNewIncSeverity] = useState("Medium");

  // New Work Order Form State
  const [newWoTitle, setNewWoTitle] = useState("");
  const [newWoType, setNewWoType] = useState("Corrective");
  const [newWoPriority, setNewWoPriority] = useState("Medium");
  const [newWoNotes, setNewWoNotes] = useState("");

  // Complete Work Order Form State
  const [compHours, setCompHours] = useState(1.5);
  const [compPartsCost, setCompPartsCost] = useState(0);
  const [compLaborCost, setCompLaborCost] = useState(0);
  const [compNotes, setCompNotes] = useState("");

  // QR Management State
  const [rotateModalOpen, setRotateModalOpen] = useState(false);
  const [rotateReason, setRotateReason] = useState("");
  const [rotating, setRotating] = useState(false);
  const [activeQrCode, setActiveQrCode] = useState<string | null>(null);
  const [revokeModalOpen, setRevokeModalOpen] = useState(false);
  const [revokeQrReason, setRevokeQrReason] = useState("");
  const [revokingQr, setRevokingQr] = useState(false);
  const [qrActionMessage, setQrActionMessage] = useState<string | null>(null);

  // Load Data
  const load = useCallback(async () => {
    const client = new ApiClient();
    const [assetDetail, telemetry, timeline, incidents, workOrders, qrLabel] = await Promise.all([
      client.getDeviceAssetDetail(id).catch(() => null),
      client.deviceTelemetry(id).catch(() => []),
      client.getDeviceTimeline(id).catch(() => []),
      client.getIncidents(id).catch(() => []),
      client.getWorkOrders(id).catch(() => []),
      client.getQrLabel(id).catch(() => null)
    ]);

    if (isAdmin) {
      const directory = await client.users().catch(() => []);
      setUsers(directory);
    }

    return {
      assetDetail,
      telemetry,
      timeline,
      incidents,
      workOrders,
      qrLabel
    };
  }, [id, isAdmin]);

  const { data, error, refresh } = useLiveQuery(load);
  const asset = data?.assetDetail;
  const telemetry = data?.telemetry ?? [];
  const timeline: AssetTimelineItem[] = data?.timeline ?? [];
  const incidents: IncidentItem[] = data?.incidents ?? [];
  const workOrders: WorkOrderItem[] = data?.workOrders ?? [];
  const qrLabel: DeviceQrLabelDto | null = data?.qrLabel ?? null;

  function openEditModal(d: DeviceAssetDetail) {
    setEditSerial(d.serialNumber ?? "");
    setEditManufacturer(d.manufacturer ?? "");
    setEditModel(d.model ?? "");
    setEditAssetType(d.assetType ?? "Laptop");
    setEditCampus(d.locationCampus ?? "");
    setEditBuilding(d.locationBuilding ?? "");
    setEditFloor(d.locationFloor ?? "");
    setEditRoom(d.locationRoom ?? "");
    setEditCost(d.purchaseCost ?? undefined);
    setEditReason("");
    setEditConfirmed(false);
    setEditProfileOpen(true);
  }

  async function handleSaveProfile(e: React.FormEvent) {
    e.preventDefault();
    if (!asset || !editReason.trim() || !editConfirmed) return;
    setSending(true);
    try {
      await new ApiClient().updateAssetProfile(asset.id, {
        serialNumber: editSerial.trim() || null,
        manufacturer: editManufacturer.trim() || null,
        model: editModel.trim() || null,
        assetType: editAssetType,
        locationCampus: editCampus.trim() || null,
        locationBuilding: editBuilding.trim() || null,
        locationFloor: editFloor.trim() || null,
        locationRoom: editRoom.trim() || null,
        purchaseCost: editCost ? Number(editCost) : null,
        reason: editReason.trim(),
        confirmed: editConfirmed
      });
      setEditProfileOpen(false);
      refresh();
      setMessage(lang === "vi" ? "Cập nhật hồ sơ tài sản thành công." : "Asset profile updated.");
    } catch {
      setMessage(lang === "vi" ? "Không thể cập nhật hồ sơ. Kiểm tra quyền hạn." : "Failed to update profile.");
    } finally {
      setSending(false);
    }
  }

  async function handleCreateIncident(e: React.FormEvent) {
    e.preventDefault();
    if (!asset || !newIncTitle.trim()) return;
    setSending(true);
    incidentIdempotencyKey.current ??= crypto.randomUUID();
    try {
      await new ApiClient().createIncident({
        deviceId: asset.id,
        title: newIncTitle.trim(),
        description: newIncDesc.trim() || null,
        severity: newIncSeverity,
        idempotencyKey: incidentIdempotencyKey.current
      });
      incidentIdempotencyKey.current = null;
      setReportIncOpen(false);
      setNewIncTitle("");
      setNewIncDesc("");
      refresh();
      setMessage(lang === "vi" ? "Đã ghi nhận báo hỏng sự cố." : "Incident registered.");
    } catch {
      setMessage(lang === "vi" ? "Không thể tạo báo hỏng." : "Failed to create incident.");
    } finally {
      setSending(false);
    }
  }

  async function handleCreateWorkOrder(e: React.FormEvent) {
    e.preventDefault();
    if (!asset || !newWoTitle.trim()) return;
    setSending(true);
    try {
      await new ApiClient().createWorkOrder({
        deviceId: asset.id,
        title: newWoTitle.trim(),
        type: newWoType,
        priority: newWoPriority,
        notes: newWoNotes.trim() || null
      });
      setCreateWoOpen(false);
      setNewWoTitle("");
      setNewWoNotes("");
      refresh();
      setMessage(lang === "vi" ? "Đã tạo phiếu bảo trì (Work Order)." : "Work order created.");
    } catch {
      setMessage(lang === "vi" ? "Không thể tạo phiếu bảo trì." : "Failed to create work order.");
    } finally {
      setSending(false);
    }
  }

  async function handleCompleteWorkOrder(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedWo) return;
    setSending(true);
    try {
      await new ApiClient().completeWorkOrder(selectedWo.id, {
        laborHours: Number(compHours),
        partsCost: Number(compPartsCost),
        laborCost: Number(compLaborCost),
        notes: compNotes.trim() || null
      });
      setCompleteWoOpen(false);
      setSelectedWo(null);
      refresh();
      setMessage(lang === "vi" ? "Đã hoàn thành phiếu bảo trì." : "Work order marked completed.");
    } catch {
      setMessage(lang === "vi" ? "Không thể hoàn thành phiếu bảo trì." : "Failed to complete work order.");
    } finally {
      setSending(false);
    }
  }

  async function runAction(action: () => Promise<unknown>, successText: string, clear: () => void) {
    if (sending) return;
    setSending(true);
    setMessage("");
    try {
      await action();
      setMessage(successText);
      clear();
      refresh();
    } catch (cause) {
      setMessage(
        cause instanceof ApiError && cause.status === 403
          ? (lang === "vi" ? "Bạn không có quyền thực hiện thao tác này." : "Permission denied.")
          : (lang === "vi" ? "Thao tác thất bại. Kiểm tra kết nối." : "Operation failed.")
      );
    } finally {
      setSending(false);
    }
  }

  if (!data && !error) {
    return <p className="subtitle" role="status">{lang === "vi" ? "Đang tải hồ sơ tài sản và chỉ số..." : "Loading asset profile..."}</p>;
  }

  if (error || !asset) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{lang === "vi" ? "Không tìm thấy hồ sơ thiết bị" : "Device unavailable"}</h2></div>
        <p className="subtitle">{lang === "vi" ? "Không thể tải dữ liệu tài sản. Kiểm tra quyền hoặc kết nối API." : "Unable to load asset."}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  const latest = telemetry[0] ?? null;
  const health = asset.healthScore;
  const tco = asset.repairVsReplace;

  // QR Code generation & handlers
  const hasActiveQr = Boolean(activeQrCode || qrLabel?.isActive);
  const qrTargetUrl = activeQrCode && typeof window !== "undefined"
    ? `${window.location.origin}/qr/${activeQrCode}`
    : null;
  const qrSvg = qrTargetUrl ? generateQrSvgPath(generateQrMatrix(qrTargetUrl), 5) : null;

  async function handleRotateQr(e: React.FormEvent) {
    e.preventDefault();
    if (!rotateReason.trim() || rotating) return;
    setRotating(true);
    setQrActionMessage(null);
    try {
      const res = await new ApiClient().generateQrLabel(id, rotateReason.trim());
      setActiveQrCode(res.code);
      setRotateModalOpen(false);
      setRotateReason("");
      setQrActionMessage(lang === "vi" ? "Đã phát hành / xoay vòng tem QR mới thành công!" : "New QR label issued successfully!");
      refresh();
    } catch {
      setQrActionMessage(lang === "vi" ? "Không thể xoay vòng tem QR." : "Failed to rotate QR label.");
    } finally {
      setRotating(false);
    }
  }

  async function handleRevokeQr() {
    if (revokingQr) return;
    setRevokingQr(true);
    setQrActionMessage(null);
    try {
      await new ApiClient().revokeQrLabel(id, revokeQrReason.trim() || "Admin revoked QR label");
      setActiveQrCode(null);
      setRevokeModalOpen(false);
      setRevokeQrReason("");
      setQrActionMessage(lang === "vi" ? "Đã thu hồi tem QR thành công." : "QR label revoked successfully.");
      refresh();
    } catch {
      setQrActionMessage(lang === "vi" ? "Không thể thu hồi tem QR." : "Failed to revoke QR label.");
    } finally {
      setRevokingQr(false);
    }
  }

  return (
    <>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 16 }}>
        <div>
          <p className="subtitle" style={{ margin: 0, fontSize: "1.05rem", fontWeight: 700, color: "var(--ink)" }}>
            💻 {asset.name} &nbsp;·&nbsp;
            <span style={{ color: "var(--muted)", fontWeight: 400 }}>{asset.model ?? asset.osVersion}</span>
          </p>
          <p className="subtitle" style={{ margin: "4px 0 0" }}>
            ID: <code>{asset.id}</code> &nbsp;·&nbsp; {t("serialNumber")}: <strong>{asset.serialNumber ?? "Chưa thiết lập"}</strong>
          </p>
        </div>

        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {isTechnician && (
            <button
              type="button"
              className="action-outline"
              onClick={() => openEditModal(asset)}
            >
              ✏️ {t("editAssetProfile")}
            </button>
          )}
          <button
            type="button"
            className="action-outline"
            style={{ color: "var(--warn)" }}
            onClick={() => { incidentIdempotencyKey.current = null; setReportIncOpen(true); }}
          >
            🚨 {t("reportIncident")}
          </button>
          {isTechnician && (
            <button
              type="button"
              className="action"
              onClick={() => setCreateWoOpen(true)}
            >
              🔧 {t("createWorkOrder")}
            </button>
          )}
        </div>
      </div>

      {message && <p role="status" className="subtitle" style={{ color: "var(--accent)", fontWeight: 600, marginTop: 12 }}>{message}</p>}

      {/* 4 Tabs Bar */}
      <div className="tabs-bar" style={{ marginTop: 24 }}>
        <button
          type="button"
          className={`tab-btn ${activeTab === "overview" ? "active" : ""}`}
          onClick={() => setActiveTab("overview")}
        >
          📊 {t("tabOverview")}
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === "timeline" ? "active" : ""}`}
          onClick={() => setActiveTab("timeline")}
        >
          ⏳ {t("tabTimeline")} ({timeline.length})
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === "cmms" ? "active" : ""}`}
          onClick={() => setActiveTab("cmms")}
        >
          🛠️ {t("tabCmms")} ({workOrders.length} WO / {incidents.length} {lang === "vi" ? "Sự cố" : "Incidents"})
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === "tco" ? "active" : ""}`}
          onClick={() => setActiveTab("tco")}
        >
          💰 {t("tabTco")}
        </button>
      </div>

      {/* ========================================================================= */}
      {/* TAB 1: OVERVIEW & PROFILE */}
      {/* ========================================================================= */}
      {activeTab === "overview" && (
        <>
          {/* Health Score & Key Metrics Banner */}
          <section className="metrics" aria-label="Device health metrics">
            <div className="metric">
              <span>{t("healthScore")}</span>
              <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginTop: 4 }}>
                <strong style={{ color: health.score >= 85 ? "var(--accent)" : health.score >= 60 ? "var(--warn)" : "var(--danger)" }}>
                  {health.score}%
                </strong>
                <span className={`badge ${health.score >= 85 ? "badge-success" : health.score >= 60 ? "badge-warn" : "badge-danger"}`}>
                  {health.grade}
                </span>
              </div>
            </div>

            <div className="metric">
              <span>{t("status")}</span>
              <strong style={{ color: asset.isRevoked ? "var(--danger)" : asset.isOnline ? "var(--accent)" : "var(--danger)" }}>
                {asset.isRevoked ? (lang === "vi" ? "Đã thu hồi" : "Revoked") : asset.isOnline ? "Online" : "Offline"}
              </strong>
            </div>

            <div className="metric">
              <span>CPU & RAM</span>
              <strong>
                {latest ? `${latest.cpuPercent.toFixed(0)}% / ${latest.ramPercent.toFixed(0)}%` : "N/A"}
              </strong>
            </div>

            <div className="metric">
              <span>{t("tcoAnalysis")}</span>
              <span className={`badge ${tco.recommendation === "Keep & Maintain" ? "badge-success" : tco.recommendation === "Evaluate Replacement" ? "badge-warn" : "badge-danger"}`} style={{ marginTop: 8, fontSize: ".82rem" }}>
                {tco.recommendation}
              </span>
            </div>
          </section>

          {/* Health Recommendations Alert if score < 85 */}
          {health.recommendations.length > 0 && health.score < 85 && (
            <div className="privacy" style={{ marginBottom: 20, borderColor: health.score < 60 ? "var(--danger)" : "var(--warn)" }}>
              <strong>⚠️ {lang === "vi" ? "Khuyến nghị cải thiện sức khỏe thiết bị:" : "Health Improvement Insights:"}</strong>
              <ul style={{ margin: "6px 0 0 18px", padding: 0, fontSize: ".88rem" }}>
                {health.recommendations.map((rec, idx) => (
                  <li key={idx}>{rec}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Asset Info & Physical Specs */}
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 20, marginBottom: 24 }}>
            <div className="panel">
              <div className="panel-head">
                <h2>📋 {t("assetInfo")}</h2>
              </div>
              <section className="device-facts" style={{ margin: 0 }}>
                <div>
                  <dt>{t("manufacturer")} & {t("model")}</dt>
                  <dd>{asset.manufacturer ?? "N/A"} - {asset.model ?? "N/A"}</dd>
                </div>
                <div>
                  <dt>{t("assetType")}</dt>
                  <dd><span className="badge badge-info">{asset.assetType ?? "Workstation"}</span></dd>
                </div>
                <div>
                  <dt>{t("serialNumber")}</dt>
                  <dd><code>{asset.serialNumber ?? "Chưa gán"}</code></dd>
                </div>
                <div>
                  <dt>{t("location")}</dt>
                  <dd>{[asset.locationCampus, asset.locationBuilding, asset.locationFloor, asset.locationRoom].filter(Boolean).join(" - ") || "Chưa xác định"}</dd>
                </div>
                <div>
                  <dt>{t("purchaseDate")} & {t("purchaseCost")}</dt>
                  <dd>
                    {asset.purchaseDate ? new Date(asset.purchaseDate).toLocaleDateString() : "N/A"} &nbsp;·&nbsp;
                    <strong>{asset.purchaseCost ? `${asset.purchaseCost.toLocaleString()} USD` : "N/A"}</strong>
                  </dd>
                </div>
                <div>
                  <dt>{t("warrantyExpiry")}</dt>
                  <dd>{asset.warrantyExpiresAt ? new Date(asset.warrantyExpiresAt).toLocaleDateString() : "N/A"}</dd>
                </div>
                <div>
                  <dt>{t("vendor")}</dt>
                  <dd>{asset.vendorName ?? "N/A"}</dd>
                </div>
                <div>
                  <dt>{lang === "vi" ? "Người sử dụng" : "Assigned User"}</dt>
                  <dd>{asset.assignedUserName ?? "Chưa gán"}</dd>
                </div>
              </section>

              {/* Hardware Specs JSON display if present */}
              {asset.specificationsJson && (
                <div style={{ marginTop: 18, borderTop: "1px solid #e7eeeb", paddingTop: 14 }}>
                  <span style={{ fontSize: ".76rem", textTransform: "uppercase", color: "var(--muted)", fontWeight: 700 }}>
                    {lang === "vi" ? "Chi tiết Cấu hình Phần cứng" : "Hardware Configuration"}
                  </span>
                  <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 10, marginTop: 8 }}>
                    {(() => {
                      try {
                        const parsed = JSON.parse(asset.specificationsJson);
                        return Object.entries(parsed).map(([k, v]) => (
                          <div key={k} style={{ fontSize: ".82rem", background: "#f8faf9", padding: "6px 10px", borderRadius: 6 }}>
                            <strong style={{ textTransform: "capitalize" }}>{k}:</strong> {String(v)}
                          </div>
                        ));
                      } catch {
                        return <code style={{ fontSize: ".8rem" }}>{asset.specificationsJson}</code>;
                      }
                    })()}
                  </div>
                </div>
              )}
            </div>

            {/* QR Code Tag Card */}
            <div className="panel" style={{ textAlign: "center" }}>
              <div className="panel-head" style={{ justifyContent: "space-between", alignItems: "center" }}>
                <h2 style={{ margin: 0 }}>📷 {t("qrCode")}</h2>
                {hasActiveQr ? (
                  <span className="badge badge-success">{t("statusActive")}</span>
                ) : (
                  <span className="badge badge-neutral">{lang === "vi" ? "Chưa cấp" : "None"}</span>
                )}
              </div>

              {qrActionMessage && (
                <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", padding: "6px 10px", borderRadius: 8, margin: "8px 0", fontSize: ".8rem", color: "#166534" }}>
                  {qrActionMessage}
                </div>
              )}

              {hasActiveQr ? (
                <>
                  {qrSvg ? <div style={{ background: "#ffffff", padding: 14, borderRadius: 12, border: "2px dashed #0b6b5f", display: "inline-block", margin: "8px 0" }}>
                    <svg width={qrSvg.size} height={qrSvg.size} viewBox={`0 0 ${qrSvg.size} ${qrSvg.size}`} style={{ display: "block", margin: "0 auto" }}>
                      <path d={qrSvg.path} fill="#102a2a" />
                    </svg>
                    <div style={{ marginTop: 8, fontSize: ".76rem", borderTop: "1px solid #e7eeeb", paddingTop: 6 }}>
                      <strong>{asset.name}</strong>
                      <div style={{ color: "var(--muted)", fontFamily: "monospace" }}>
                        {qrLabel ? `${qrLabel.codePrefix}***` : (asset.serialNumber ?? asset.id.substring(0, 8))}
                      </div>
                    </div>
                  </div> : <p className="subtitle">{lang === "vi" ? "Mã QR chỉ hiển thị lúc phát hành. Xoay vòng tem để in lại." : "QR code is shown only when issued. Rotate the label to print a new one."}</p>}

                  {qrLabel && (
                    <div style={{ fontSize: ".76rem", color: "var(--muted)", margin: "4px 0 10px" }}>
                      {lang === "vi" ? "Lần quét cuối" : "Last scan"}: <strong>{qrLabel.lastScannedAt ? new Date(qrLabel.lastScannedAt).toLocaleString() : "—"}</strong>
                    </div>
                  )}

                  <p className="subtitle" style={{ fontSize: ".78rem", margin: "4px 0 12px" }}>
                    {t("scanQrNotice")}
                  </p>

                  <div style={{ display: "grid", gap: 8 }}>
                    <button
                      type="button"
                      className="action-outline"
                      style={{ width: "100%", fontSize: ".82rem" }}
                      onClick={() => window.print()}
                      disabled={!qrSvg}
                    >
                      🖨️ {t("printQr")}
                    </button>

                    {isTechnician && (
                      <div style={{ display: "flex", gap: 6 }}>
                        <button
                          type="button"
                          className="action-outline"
                          style={{ flex: 1, fontSize: ".78rem" }}
                          onClick={() => { setRotateModalOpen(true); setRotateReason(""); }}
                        >
                          🔄 {t("rotateQr")}
                        </button>
                        <button
                          type="button"
                          className="action-outline"
                          style={{ flex: 1, fontSize: ".78rem", color: "var(--danger)", borderColor: "var(--danger)" }}
                          onClick={() => { setRevokeModalOpen(true); setRevokeQrReason(""); }}
                        >
                          ✕ {t("revokeQr")}
                        </button>
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <div style={{ padding: "16px 0" }}>
                  <p className="subtitle" style={{ fontSize: ".84rem", margin: "0 0 14px" }}>
                    {t("noActiveQr")}
                  </p>
                  {isTechnician && (
                    <button
                      type="button"
                      className="action"
                      style={{ width: "100%", fontSize: ".82rem" }}
                      onClick={() => { setRotateModalOpen(true); setRotateReason(""); }}
                    >
                      ➕ {t("generateQr")}
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Device Assignment (Admin only) */}
          {isAdmin && (
            <div className="panel" style={{ marginBottom: 24 }}>
              <div className="panel-head"><h2>{lang === "vi" ? "Phân công thiết bị cho nhân sự" : "Device assignment"}</h2></div>
              <p className="subtitle">{lang === "vi" ? "Chỉ Employee cùng tổ chức có thể được gán. Để bỏ gán, chọn Chưa gán." : "Only an Employee in this organization can be assigned."}</p>
              <label className="form-group" htmlFor="assigned-user">
                Employee
                <select
                  id="assigned-user"
                  className="form-select"
                  disabled={asset.isRevoked || sending}
                  value={assignedUserId === undefined ? asset.assignedUserId ?? "" : assignedUserId}
                  onChange={(e) => setAssignedUserId(e.target.value)}
                >
                  <option value="">{lang === "vi" ? "Chưa gán" : "Unassigned"}</option>
                  {users.filter((u) => u.role === "Employee").map((u) => (
                    <option key={u.id} value={u.id}>{u.displayName} · {u.email}</option>
                  ))}
                </select>
              </label>
              <label className="form-group" htmlFor="assignment-reason">
                {lang === "vi" ? "Lý do thay đổi" : "Reason"}
                <textarea
                  id="assignment-reason"
                  className="form-input"
                  rows={2}
                  value={assignmentReason}
                  disabled={asset.isRevoked || sending}
                  onChange={(e) => setAssignmentReason(e.target.value)}
                />
              </label>
              <label style={{ fontSize: ".85rem" }}>
                <input
                  type="checkbox"
                  checked={assignmentConfirmed}
                  disabled={asset.isRevoked || sending}
                  onChange={(e) => setAssignmentConfirmed(e.target.checked)}
                /> {lang === "vi" ? "Tôi xác nhận thay đổi phân công và chịu trách nhiệm kiểm toán." : "I confirm this assignment change."}
              </label>
              <div className="btn-row">
                <button
                  type="button"
                  className="action"
                  disabled={sending || asset.isRevoked || !assignmentConfirmed || !assignmentReason.trim()}
                  onClick={() => runAction(
                    () => new ApiClient().assignDevice(asset.id, assignedUserId === undefined ? asset.assignedUserId : assignedUserId || null, assignmentReason.trim(), assignmentConfirmed),
                    lang === "vi" ? "Đã cập nhật phân công thành công." : "Assignment updated.",
                    () => { setAssignmentReason(""); setAssignmentConfirmed(false); setAssignedUserId(undefined); }
                  )}
                >
                  {sending ? t("loading") : t("save")}
                </button>
              </div>
            </div>
          )}

          {/* Safe Operations (Lock/Isolation) */}
          {isTechnician && !asset.isRevoked && (
            <div className="panel" style={{ marginBottom: 24 }}>
              <div className="panel-head"><h2>{lang === "vi" ? "Thao tác an toàn trên thiết bị" : "Safe operations"}</h2></div>
              <p className="subtitle">{lang === "vi" ? "Lệnh được ký số mật mã với lý do bắt buộc và kiểm toán bất biến." : "Commands require cryptographic signatures and audit accountability."}</p>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: 14 }}>
                <div className="form-group">
                  <label>{lang === "vi" ? "Loại lệnh" : "Command"}</label>
                  <select
                    className="form-select"
                    value={commandType}
                    onChange={(e) => setCommandType(e.target.value as CommandType)}
                  >
                    <option value="SimulateLock">{lang === "vi" ? "Mô phỏng khóa màn hình (SimulateLock)" : "Simulate Lock"}</option>
                    <option value="SimulateNetworkIsolation">{lang === "vi" ? "Mô phỏng cách ly mạng (SimulateNetworkIsolation)" : "Simulate Network Isolation"}</option>
                    <option value="CollectTelemetryNow">{lang === "vi" ? "Mô phỏng yêu cầu telemetry tức thời" : "Simulate Immediate Telemetry Request"}</option>
                    <option value="RefreshPolicy">{lang === "vi" ? "Mô phỏng làm mới chính sách" : "Simulate Policy Refresh"}</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Lý do ban hành lệnh" : "Reason"}</label>
                  <input
                    className="form-input"
                    value={commandReason}
                    placeholder={lang === "vi" ? "Nhập lý do bắt buộc..." : "Enter required reason..."}
                    onChange={(e) => setCommandReason(e.target.value)}
                  />
                </div>
              </div>
              <label style={{ fontSize: ".85rem" }}>
                <input
                  type="checkbox"
                  checked={commandConfirmed}
                  onChange={(e) => setCommandConfirmed(e.target.checked)}
                /> {lang === "vi" ? `Tôi xác nhận ban hành lệnh trên ${asset.name}` : `I confirm this command on ${asset.name}`}
              </label>
              <div className="btn-row">
                <button
                  type="button"
                  className="action"
                  disabled={sending || !commandConfirmed || !commandReason.trim()}
                  onClick={() => runAction(
                    () => new ApiClient().createCommand({ deviceId: asset.id, type: commandType, reason: commandReason.trim(), confirmed: true }),
                    lang === "vi" ? "Đã gửi lệnh điều khiển an toàn." : "Command dispatched.",
                    () => { setCommandReason(""); setCommandConfirmed(false); }
                  )}
                >
                  {sending ? t("loading") : (lang === "vi" ? "Ký số & Gửi lệnh" : "Sign & Dispatch")}
                </button>
              </div>
            </div>
          )}

          {/* Revoke Device (Admin only) */}
          {isAdmin && !asset.isRevoked && (
            <div className="panel" style={{ borderColor: "#fecaca" }}>
              <div className="panel-head"><h2 style={{ color: "var(--danger)" }}>{lang === "vi" ? "Thu hồi thiết bị (Revoke)" : "Revoke device"}</h2></div>
              <p className="subtitle">{lang === "vi" ? "Thu hồi credential vĩnh viễn và chấm dứt quyền truy cập." : "Permanently revokes credentials."}</p>
              <div className="form-group">
                <input
                  className="form-input"
                  placeholder={lang === "vi" ? "Lý do thu hồi..." : "Reason for revocation..."}
                  value={revokeReason}
                  onChange={(e) => setRevokeReason(e.target.value)}
                />
              </div>
              <label style={{ fontSize: ".85rem" }}>
                <input
                  type="checkbox"
                  checked={revokeConfirmed}
                  onChange={(e) => setRevokeConfirmed(e.target.checked)}
                /> {lang === "vi" ? "Tôi hiểu rằng credential sẽ bị thu hồi vĩnh viễn." : "I understand revocation is permanent."}
              </label>
              <div className="btn-row">
                <button
                  type="button"
                  className="action-danger"
                  disabled={sending || !revokeConfirmed || !revokeReason.trim()}
                  onClick={() => runAction(
                    () => new ApiClient().revokeDevice(asset.id, revokeReason.trim(), revokeConfirmed),
                    lang === "vi" ? "Đã thu hồi thiết bị." : "Device revoked.",
                    () => { setRevokeReason(""); setRevokeConfirmed(false); }
                  )}
                >
                  {sending ? t("loading") : (lang === "vi" ? "Xác nhận thu hồi" : "Confirm Revocation")}
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* ========================================================================= */}
      {/* TAB 2: ASSET TIMELINE */}
      {/* ========================================================================= */}
      {activeTab === "timeline" && (
        <div className="panel">
          <div className="panel-head">
            <h2>⏳ {t("tabTimeline")}</h2>
            <span style={{ fontSize: ".82rem", color: "var(--muted)" }}>
              {timeline.length} {lang === "vi" ? "sự kiện vòng đời được ghi nhận" : "lifecycle events recorded"}
            </span>
          </div>

          {timeline.length === 0 ? (
            <div className="empty-state">
              <p>{lang === "vi" ? "Chưa có sự kiện vòng đời nào." : "No timeline events recorded yet."}</p>
            </div>
          ) : (
            <div style={{ position: "relative", paddingLeft: 32, margin: "20px 0" }}>
              {/* Vertical line */}
              <div style={{ position: "absolute", left: 11, top: 8, bottom: 8, width: 2, background: "#dce6e2" }} />

              {timeline.map((item) => {
                const icon = item.eventType === "Enrolled" ? "🚀" :
                  item.eventType === "Purchased" ? "🛒" :
                  item.eventType.includes("Incident") ? "🚨" :
                  item.eventType.includes("WorkOrder") ? "🔧" :
                  item.eventType === "Command" ? "⚡" :
                  item.eventType.includes("Loan") ? "🤝" : "📌";

                const badgeBg = item.severity === "critical" ? "#fee2e2" :
                  item.severity === "warning" ? "#fef3c7" :
                  item.severity === "success" ? "#dcfce7" : "#e0f2fe";

                return (
                  <div key={item.id} style={{ position: "relative", marginBottom: 24 }}>
                    {/* Node Dot */}
                    <div style={{
                      position: "absolute",
                      left: -32,
                      top: 2,
                      width: 24,
                      height: 24,
                      borderRadius: "50%",
                      background: badgeBg,
                      border: "2px solid white",
                      boxShadow: "0 2px 4px rgba(0,0,0,0.1)",
                      display: "grid",
                      placeItems: "center",
                      fontSize: ".75rem"
                    }}>
                      {icon}
                    </div>

                    <div style={{ background: "#ffffff", padding: "14px 18px", borderRadius: 12, border: "1px solid #e7eeeb", boxShadow: "0 2px 6px rgba(0,0,0,0.02)" }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", flexWrap: "wrap", gap: 8 }}>
                        <strong style={{ fontSize: ".92rem", color: "var(--ink)" }}>{item.title}</strong>
                        <span style={{ fontSize: ".76rem", color: "var(--muted)" }}>
                          {new Date(item.timestamp).toLocaleString()}
                        </span>
                      </div>
                      <p style={{ margin: "6px 0 0", fontSize: ".84rem", color: "var(--muted)", lineHeight: 1.5 }}>
                        {item.description}
                      </p>
                      {item.actor && (
                        <div style={{ marginTop: 6, fontSize: ".75rem", color: "var(--accent)" }}>
                          👤 {lang === "vi" ? "Thực hiện bởi:" : "By:"} {item.actor}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 3: CMMS WORK ORDERS & INCIDENTS */}
      {/* ========================================================================= */}
      {activeTab === "cmms" && (
        <div style={{ display: "grid", gap: 24 }}>
          {/* Work Orders List */}
          <div className="panel">
            <div className="panel-head">
              <h2>🛠️ {lang === "vi" ? "Phiếu Bảo trì & Sửa chữa (Work Orders)" : "Maintenance Work Orders"}</h2>
              {isTechnician && (
                <button
                  type="button"
                  className="action-sm"
                  onClick={() => setCreateWoOpen(true)}
                >
                  + {t("createWorkOrder")}
                </button>
              )}
            </div>

            {workOrders.length === 0 ? (
              <p className="subtitle">{lang === "vi" ? "Chưa có phiếu bảo trì nào được lập cho thiết bị này." : "No work orders created for this device."}</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>{lang === "vi" ? "Mã phiếu" : "WO Number"}</th>
                    <th>{lang === "vi" ? "Tiêu đề công việc" : "Title"}</th>
                    <th>{lang === "vi" ? "Phân loại" : "Type"}</th>
                    <th>{lang === "vi" ? "Độ ưu tiên" : "Priority"}</th>
                    <th>{t("status")}</th>
                    <th>{lang === "vi" ? "Chi phí" : "Cost"}</th>
                    <th style={{ textAlign: "right" }}>{t("actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {workOrders.map((wo) => (
                    <tr key={wo.id}>
                      <td><strong>{wo.workOrderNumber}</strong></td>
                      <td>{wo.title}</td>
                      <td><span className="badge badge-info">{wo.type}</span></td>
                      <td><span className={`badge ${wo.priority === "Urgent" ? "badge-danger" : wo.priority === "High" ? "badge-warn" : "badge-neutral"}`}>{wo.priority}</span></td>
                      <td>
                        <span className={`badge ${wo.status === "Completed" ? "badge-success" : wo.status === "InProgress" ? "badge-info" : "badge-neutral"}`}>
                          {wo.status}
                        </span>
                      </td>
                      <td>
                        {wo.status === "Completed" ? (
                          <strong>{wo.totalCost.toLocaleString()} USD</strong>
                        ) : (
                          <span style={{ color: "var(--muted)" }}>{lang === "vi" ? "Chưa nghiệm thu" : "Pending"}</span>
                        )}
                      </td>
                      <td style={{ textAlign: "right" }}>
                        {isTechnician && wo.status !== "Completed" && wo.status !== "Cancelled" && (
                          <button
                            type="button"
                            className="action-sm"
                            style={{ padding: "4px 10px", fontSize: ".76rem" }}
                            onClick={() => {
                              setSelectedWo(wo);
                              setCompHours(1.5);
                              setCompPartsCost(0);
                              setCompLaborCost(40);
                              setCompNotes("");
                              setCompleteWoOpen(true);
                            }}
                          >
                            ✓ {t("completeWorkOrder")}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {/* Incidents List */}
          <div className="panel">
            <div className="panel-head">
              <h2>🚨 {lang === "vi" ? "Danh sách Báo hỏng & Sự cố (Incidents)" : "Reported Incidents"}</h2>
              <button
                type="button"
                className="action-outline"
                style={{ color: "var(--warn)" }}
                onClick={() => { incidentIdempotencyKey.current = null; setReportIncOpen(true); }}
              >
                + {t("reportIncident")}
              </button>
            </div>

            {incidents.length === 0 ? (
              <p className="subtitle">{lang === "vi" ? "Không có sự cố nào được báo cáo trên thiết bị này." : "No incidents reported."}</p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>{lang === "vi" ? "Sự cố" : "Title"}</th>
                    <th>{lang === "vi" ? "Mức độ" : "Severity"}</th>
                    <th>{t("status")}</th>
                    <th>{lang === "vi" ? "Người báo" : "Reported By"}</th>
                    <th>{lang === "vi" ? "Thời gian" : "Reported At"}</th>
                    <th style={{ textAlign: "right" }}>{t("actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {incidents.map((inc) => (
                    <tr key={inc.id}>
                      <td>
                        <strong>{inc.title}</strong>
                        {inc.description && <div style={{ fontSize: ".76rem", color: "var(--muted)" }}>{inc.description}</div>}
                      </td>
                      <td>
                        <span className={`badge ${inc.severity === "Critical" ? "badge-danger" : inc.severity === "High" ? "badge-warn" : "badge-neutral"}`}>
                          {inc.severity}
                        </span>
                      </td>
                      <td>
                        <span className={`badge ${inc.status === "Resolved" ? "badge-success" : inc.status === "Closed" ? "badge-neutral" : "badge-warn"}`}>
                          {inc.status}
                        </span>
                      </td>
                      <td>{inc.reportedByUserName ?? "Nhân sự"}</td>
                      <td>{new Date(inc.createdAt).toLocaleString()}</td>
                      <td style={{ textAlign: "right" }}>
                        {isTechnician && inc.status !== "Resolved" && inc.status !== "Closed" && (
                          <button
                            type="button"
                            className="action-outline"
                            style={{ padding: "4px 8px", fontSize: ".75rem" }}
                            onClick={() => runAction(
                              () => new ApiClient().updateIncidentStatus(inc.id, { status: "Resolved", resolutionNotes: "Đã khắc phục hoàn tất." }),
                              lang === "vi" ? "Đã giải quyết sự cố." : "Incident resolved.",
                              () => {}
                            )}
                          >
                            ✓ {lang === "vi" ? "Đã sửa xong" : "Resolve"}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 4: REPAIR VS REPLACE & TCO */}
      {/* ========================================================================= */}
      {activeTab === "tco" && (
        <div style={{ display: "grid", gap: 20 }}>
          <div className="panel">
            <div className="panel-head">
              <h2>💰 {t("tcoAnalysis")}</h2>
              <span className={`badge ${tco.recommendation === "Keep & Maintain" ? "badge-success" : tco.recommendation === "Evaluate Replacement" ? "badge-warn" : "badge-danger"}`} style={{ fontSize: ".88rem", padding: "6px 14px" }}>
                {tco.recommendation}
              </span>
            </div>

            <p className="subtitle" style={{ fontSize: ".92rem", lineHeight: 1.6 }}>
              {tco.analysisSummary}
            </p>

            <div className="metrics" style={{ marginTop: 20 }}>
              <div className="metric">
                <span>{t("purchaseCost")} (Nguyên giá)</span>
                <strong>{tco.replacementCost ? `${tco.replacementCost.toLocaleString()} USD` : "1,200 USD"}</strong>
              </div>

              <div className="metric">
                <span>{t("cumulativeMaintenance")}</span>
                <strong style={{ color: tco.maintenanceToCostRatioPercent >= 50 ? "var(--danger)" : "var(--ink)" }}>
                  {tco.cumulativeMaintenanceCost.toLocaleString()} USD
                </strong>
                <span style={{ fontSize: ".76rem", color: "var(--muted)" }}>
                  ({tco.maintenanceToCostRatioPercent}% {lang === "vi" ? "nguyên giá" : "of purchase cost"})
                </span>
              </div>

              <div className="metric">
                <span>{t("estimated3Years")}</span>
                <strong style={{ color: "var(--warn)" }}>
                  {tco.estimatedNext3YearsCost.toLocaleString()} USD
                </strong>
              </div>

              <div className="metric">
                <span>{t("recommendation")}</span>
                <strong style={{ fontSize: "1.1rem", marginTop: 12, color: tco.recommendation === "Keep & Maintain" ? "var(--accent)" : "var(--danger)" }}>
                  {tco.recommendation}
                </strong>
              </div>
            </div>

            {/* Economic Decision Bar */}
            <div style={{ marginTop: 16 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: ".82rem", marginBottom: 6 }}>
                <span>{lang === "vi" ? "Tỷ lệ chi phí sửa chữa / Nguyên giá máy" : "Maintenance Cost / Replacement Ratio"}</span>
                <strong>{tco.maintenanceToCostRatioPercent}%</strong>
              </div>
              <div style={{ height: 12, background: "#e7eeeb", borderRadius: 6, overflow: "hidden", position: "relative" }}>
                <div
                  style={{
                    height: "100%",
                    width: `${Math.min(100, tco.maintenanceToCostRatioPercent)}%`,
                    background: tco.maintenanceToCostRatioPercent >= 55 ? "var(--danger)" : tco.maintenanceToCostRatioPercent >= 35 ? "var(--warn)" : "var(--accent)",
                    transition: "width .3s"
                  }}
                />
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: ".72rem", color: "var(--muted)", marginTop: 4 }}>
                <span>0% (Tối ưu)</span>
                <span>35% (Cân nhắc)</span>
                <span>55%+ (Thay mới ngay)</span>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: EDIT ASSET PROFILE */}
      {/* ========================================================================= */}
      {editProfileOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal" style={{ maxWidth: 640 }}>
            <div className="modal-header">
              <h3>✏️ {t("editAssetProfile")}: {asset.name}</h3>
              <button className="modal-close" onClick={() => setEditProfileOpen(false)}>×</button>
            </div>
            <form onSubmit={handleSaveProfile}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div className="form-group">
                  <label>{t("serialNumber")}</label>
                  <input className="form-input" value={editSerial} onChange={(e) => setEditSerial(e.target.value)} />
                </div>
                <div className="form-group">
                  <label>{t("assetType")}</label>
                  <select className="form-select" value={editAssetType} onChange={(e) => setEditAssetType(e.target.value)}>
                    <option value="Laptop">Laptop</option>
                    <option value="Desktop">Desktop / PC</option>
                    <option value="Server">Server / VPS</option>
                    <option value="Workstation">Workstation</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>{t("manufacturer")}</label>
                  <input className="form-input" value={editManufacturer} onChange={(e) => setEditManufacturer(e.target.value)} />
                </div>
                <div className="form-group">
                  <label>{t("model")}</label>
                  <input className="form-input" value={editModel} onChange={(e) => setEditModel(e.target.value)} />
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Cơ sở (Campus)" : "Campus"}</label>
                  <input className="form-input" value={editCampus} onChange={(e) => setEditCampus(e.target.value)} />
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Tòa nhà" : "Building"}</label>
                  <input className="form-input" value={editBuilding} onChange={(e) => setEditBuilding(e.target.value)} />
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Tầng & Phòng" : "Floor & Room"}</label>
                  <div style={{ display: "flex", gap: 6 }}>
                    <input className="form-input" placeholder="Tầng" value={editFloor} onChange={(e) => setEditFloor(e.target.value)} />
                    <input className="form-input" placeholder="Phòng" value={editRoom} onChange={(e) => setEditRoom(e.target.value)} />
                  </div>
                </div>
                <div className="form-group">
                  <label>{t("purchaseCost")} (USD)</label>
                  <input className="form-input" type="number" value={editCost ?? ""} onChange={(e) => setEditCost(e.target.value ? Number(e.target.value) : undefined)} />
                </div>
              </div>

              <div className="form-group">
                <label>{lang === "vi" ? "Lý do cập nhật hồ sơ (Kiểm toán bắt buộc)" : "Reason for change (Required)"}</label>
                <input className="form-input" required value={editReason} onChange={(e) => setEditReason(e.target.value)} />
              </div>

              <label style={{ fontSize: ".85rem" }}>
                <input type="checkbox" checked={editConfirmed} onChange={(e) => setEditConfirmed(e.target.checked)} />
                &nbsp;{lang === "vi" ? "Tôi xác nhận thông tin thay đổi hồ sơ tài sản chính xác." : "I confirm asset profile updates."}
              </label>

              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setEditProfileOpen(false)}>{t("cancel")}</button>
                <button type="submit" className="action" disabled={sending || !editConfirmed || !editReason.trim()}>
                  {sending ? t("loading") : t("save")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: REPORT INCIDENT */}
      {/* ========================================================================= */}
      {reportIncOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>🚨 {t("reportIncident")}: {asset.name}</h3>
              <button className="modal-close" onClick={() => setReportIncOpen(false)}>×</button>
            </div>
            <form onSubmit={handleCreateIncident}>
              <div className="form-group">
                <label>{lang === "vi" ? "Hiện tượng hỏng / Tiêu đề" : "Incident title"}</label>
                <input className="form-input" required value={newIncTitle} onChange={(e) => setNewIncTitle(e.target.value)} placeholder="VD: Pin phồng, quạt kêu to..." />
              </div>
              <div className="form-group">
                <label>{lang === "vi" ? "Mức độ nghiêm trọng" : "Severity"}</label>
                <select className="form-select" value={newIncSeverity} onChange={(e) => setNewIncSeverity(e.target.value)}>
                  <option value="Low">Low - Nhẹ</option>
                  <option value="Medium">Medium - Trung bình</option>
                  <option value="High">High - Cao</option>
                  <option value="Critical">Critical - Khẩn cấp</option>
                </select>
              </div>
              <div className="form-group">
                <label>{lang === "vi" ? "Mô tả chi tiết" : "Description"}</label>
                <textarea className="form-input" rows={3} value={newIncDesc} onChange={(e) => setNewIncDesc(e.target.value)} />
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setReportIncOpen(false)}>{t("cancel")}</button>
                <button type="submit" className="action" disabled={sending || !newIncTitle.trim()}>{sending ? t("loading") : t("save")}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: CREATE WORK ORDER */}
      {/* ========================================================================= */}
      {createWoOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>🔧 {t("createWorkOrder")}: {asset.name}</h3>
              <button className="modal-close" onClick={() => setCreateWoOpen(false)}>×</button>
            </div>
            <form onSubmit={handleCreateWorkOrder}>
              <div className="form-group">
                <label>{lang === "vi" ? "Tên công việc bảo trì" : "Title"}</label>
                <input className="form-input" required value={newWoTitle} onChange={(e) => setNewWoTitle(e.target.value)} placeholder="VD: Thay keo tản nhiệt, vệ sinh định kỳ..." />
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div className="form-group">
                  <label>{lang === "vi" ? "Phân loại" : "Type"}</label>
                  <select className="form-select" value={newWoType} onChange={(e) => setNewWoType(e.target.value)}>
                    <option value="Preventive">{lang === "vi" ? "Bảo trì phòng ngừa (Preventive)" : "Preventive"}</option>
                    <option value="Corrective">{lang === "vi" ? "Sửa chữa khắc phục (Corrective)" : "Corrective"}</option>
                    <option value="Inspection">{lang === "vi" ? "Kiểm định kỹ thuật (Inspection)" : "Inspection"}</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Độ ưu tiên" : "Priority"}</label>
                  <select className="form-select" value={newWoPriority} onChange={(e) => setNewWoPriority(e.target.value)}>
                    <option value="Low">Low</option>
                    <option value="Medium">Medium</option>
                    <option value="High">High</option>
                    <option value="Urgent">Urgent</option>
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label>{lang === "vi" ? "Ghi chú công việc" : "Notes"}</label>
                <textarea className="form-input" rows={2} value={newWoNotes} onChange={(e) => setNewWoNotes(e.target.value)} />
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setCreateWoOpen(false)}>{t("cancel")}</button>
                <button type="submit" className="action" disabled={sending || !newWoTitle.trim()}>{sending ? t("loading") : (lang === "vi" ? "Tạo phiếu" : "Create")}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: COMPLETE WORK ORDER */}
      {/* ========================================================================= */}
      {completeWoOpen && selectedWo && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>✓ {t("completeWorkOrder")}: {selectedWo.workOrderNumber}</h3>
              <button className="modal-close" onClick={() => setCompleteWoOpen(false)}>×</button>
            </div>
            <form onSubmit={handleCompleteWorkOrder}>
              <p className="subtitle" style={{ margin: "0 0 16px" }}>{selectedWo.title}</p>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
                <div className="form-group">
                  <label>{lang === "vi" ? "Giờ công (h)" : "Labor Hours"}</label>
                  <input className="form-input" type="number" step="0.5" min="0" value={compHours} onChange={(e) => setCompHours(Number(e.target.value))} />
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Tiền linh kiện ($)" : "Parts Cost ($)"}</label>
                  <input className="form-input" type="number" min="0" value={compPartsCost} onChange={(e) => setCompPartsCost(Number(e.target.value))} />
                </div>
                <div className="form-group">
                  <label>{lang === "vi" ? "Tiền công ($)" : "Labor Cost ($)"}</label>
                  <input className="form-input" type="number" min="0" value={compLaborCost} onChange={(e) => setCompLaborCost(Number(e.target.value))} />
                </div>
              </div>
              <div className="form-group">
                <label>{lang === "vi" ? "Ghi chú nghiệm thu & xử lý" : "Completion Notes"}</label>
                <textarea className="form-input" rows={3} value={compNotes} onChange={(e) => setCompNotes(e.target.value)} placeholder="Chi tiết linh kiện đã thay, bài test đã kiểm thử..." />
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setCompleteWoOpen(false)}>{t("cancel")}</button>
                <button type="submit" className="action" disabled={sending}>{sending ? t("loading") : (lang === "vi" ? "Nghiệm thu hoàn tất" : "Complete")}</button>
              </div>
            </form>
          </div>
        </div>
      )}
      {/* ========================================================================= */}
      {/* MODAL: ROTATE / GENERATE QR LABEL */}
      {/* ========================================================================= */}
      {rotateModalOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>🔄 {hasActiveQr ? t("rotateQr") : t("generateQr")}</h3>
              <button className="modal-close" onClick={() => setRotateModalOpen(false)}>×</button>
            </div>
            <form onSubmit={handleRotateQr}>
              <p className="subtitle" style={{ margin: "0 0 16px" }}>
                {lang === "vi"
                  ? "Phát hành tem QR mã hóa mới cho thiết bị này. Tem QR cũ (nếu có) sẽ bị vô hiệu hóa ngay lập tức vì lý do bảo mật."
                  : "Issue a new opaque QR code for this device. Any previous QR code will be revoked immediately for security."}
              </p>
              <div className="form-group">
                <label>{t("qrRotateReason")}</label>
                <textarea
                  className="form-input"
                  required
                  minLength={3}
                  rows={2}
                  placeholder={lang === "vi" ? "VD: Dán lại tem vật lý mới sau khi bảo trì định kỳ..." : "e.g., Replaced physical chassis sticker..."}
                  value={rotateReason}
                  onChange={(e) => setRotateReason(e.target.value)}
                />
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setRotateModalOpen(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="action" disabled={rotating || !rotateReason.trim()}>
                  {rotating ? t("saving") : t("confirm")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: REVOKE QR LABEL */}
      {/* ========================================================================= */}
      {revokeModalOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>✕ {t("revokeQr")}</h3>
              <button className="modal-close" onClick={() => setRevokeModalOpen(false)}>×</button>
            </div>
            <p className="subtitle" style={{ margin: "0 0 14px" }}>
              {t("confirmRevokeQr")}
            </p>
            <div className="form-group">
              <label>{t("qrRevokeReason")}</label>
              <textarea
                className="form-input"
                required
                minLength={3}
                rows={2}
                placeholder={lang === "vi" ? "VD: Tem bị rách, mất hoặc nghi ngờ bị chụp trộm..." : "e.g., Physical sticker compromised or peeled off..."}
                value={revokeQrReason}
                onChange={(e) => setRevokeQrReason(e.target.value)}
              />
            </div>
            <div className="btn-row">
              <button type="button" className="action-outline" onClick={() => setRevokeModalOpen(false)}>
                {t("cancel")}
              </button>
              <button
                type="button"
                className="action"
                style={{ background: "var(--danger)" }}
                onClick={handleRevokeQr}
                disabled={revokingQr || !revokeQrReason.trim()}
              >
                {revokingQr ? t("saving") : t("confirm")}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
