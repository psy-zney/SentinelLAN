"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { Device, Policy } from "@/types/api";

const loadPolicies = () => new ApiClient().policies();

export function PoliciesView() {
  const { t, lang } = useTranslation();
  const { data: policies, error, refresh } = useLiveQuery(loadPolicies);
  const [isCreating, setIsCreating] = useState(false);
  const [assigningPolicy, setAssigningPolicy] = useState<Policy | null>(null);
  const [editingPolicy, setEditingPolicy] = useState<Policy | null>(null);
  const [devices, setDevices] = useState<Device[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState("");
  const [loadingDevices, setLoadingDevices] = useState(false);

  // Form state
  const [name, setName] = useState("");
  const [idleTimeout, setIdleTimeout] = useState(15);
  const [usbMode, setUsbMode] = useState("Blocked");
  const [submitting, setSubmitting] = useState(false);
  const [statusMessage, setStatusMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

  const handleOpenAssign = useCallback(async (policy: Policy) => {
    setAssigningPolicy(policy);
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

  const handleCreatePolicy = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || submitting) return;
    setSubmitting(true);
    setStatusMessage(null);
    try {
      await new ApiClient().createPolicy({
        name: name.trim(),
        idleTimeoutMinutes: Number(idleTimeout),
        usbMode
      });
      setStatusMessage({ text: t("policyCreatedSuccess"), type: "success" });
      setIsCreating(false);
      setName("");
      setIdleTimeout(15);
      setUsbMode("Blocked");
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to create policy.", type: "error" });
    } finally {
      setSubmitting(false);
    }
  };

  const handleUpdatePolicy = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingPolicy || !name.trim() || submitting) return;
    setSubmitting(true); setStatusMessage(null);
    try {
      await new ApiClient().updatePolicy(editingPolicy.id, { name: name.trim(), idleTimeoutMinutes: Number(idleTimeout), usbMode });
      setStatusMessage({ text: lang === "vi" ? "Đã cập nhật chính sách." : "Policy updated successfully.", type: "success" });
      setEditingPolicy(null); setName(""); refresh();
    } catch { setStatusMessage({ text: lang === "vi" ? "Không thể cập nhật chính sách." : "Failed to update policy.", type: "error" }); }
    finally { setSubmitting(false); }
  };

  const handleAssignPolicy = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assigningPolicy || !selectedDeviceId || submitting) return;
    setSubmitting(true);
    setStatusMessage(null);
    try {
      await new ApiClient().assignPolicy(assigningPolicy.id, selectedDeviceId);
      setStatusMessage({ text: t("policyAssignedSuccess"), type: "success" });
      setAssigningPolicy(null);
      refresh();
    } catch {
      setStatusMessage({ text: "Failed to assign policy to device.", type: "error" });
    } finally {
      setSubmitting(false);
    }
  };

  if (error) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{t("policies")}</h2></div>
        <p className="subtitle">{t("error")}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  if (!policies) return <p className="subtitle" role="status">{t("loading")}</p>;

  return (
    <>
      <section className="metrics" aria-label="Policy overview">
        <div className="metric">
          <span>{lang === "vi" ? "Chính sách hoạt động" : "Active policies"}</span>
          <strong>{policies.length}</strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Thiết bị đã áp dụng" : "Devices assigned"}</span>
          <strong style={{ color: "var(--accent)" }}>
            {policies.reduce((acc, p) => acc + (p.assignedDeviceCount || 0), 0)}
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
            <h2>{t("policyList")}</h2>
            <p className="subtitle" style={{ fontSize: ".82rem" }}>
              {t("subtitlePolicies")}
            </p>
          </div>
          <button className="action" onClick={() => setIsCreating(true)}>
            {t("createPolicyBtn")}
          </button>
        </div>

        {policies.length === 0 ? (
          <div className="empty-state">
            <p>{lang === "vi" ? "Chưa có chính sách nào được định nghĩa." : "No policies defined."}</p>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>{t("policyNameLabel")}</th>
                  <th>{t("idleTimeoutLabel")}</th>
                  <th>{t("usbModeLabel")}</th>
                  <th>{t("assignedCountLabel")}</th>
                  <th>{t("created")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {policies.map((p) => (
                  <tr key={p.id}>
                    <td>
                      <strong>{p.name}</strong>
                    </td>
                    <td>{p.idleTimeoutMinutes} {lang === "vi" ? "phút" : "min"}</td>
                    <td>
                      <span className={`badge ${p.usbMode === "Blocked" ? "badge-danger" : p.usbMode === "ReadOnly" ? "badge-warn" : "badge-success"}`}>
                        {p.usbMode === "Blocked" ? t("blocked") : p.usbMode === "ReadOnly" ? t("readOnly") : t("fullAccess")}
                      </span>
                    </td>
                    <td>
                      <span className="badge badge-neutral">
                        {p.assignedDeviceCount || 0} {lang === "vi" ? "thiết bị" : "devices"}
                      </span>
                    </td>
                    <td>{new Date(p.createdAt).toLocaleDateString()}</td>
                    <td>
                      <button className="action-sm action-outline" onClick={() => { setEditingPolicy(p); setName(p.name); setIdleTimeout(p.idleTimeoutMinutes); setUsbMode(p.usbMode); }}>
                        {lang === "vi" ? "Sửa" : "Edit"}
                      </button>{" "}
                      <button className="action-sm action-outline" onClick={() => handleOpenAssign(p)}>
                        {t("assignPolicyBtn")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="privacy" style={{ marginTop: 18 }} role="note">
        <strong>{lang === "vi" ? "Minh bạch cấu hình:" : "Configuration disclosure:"}</strong>{" "}
        {lang === "vi" ? "Chính sách trong MVP này lưu cấu hình và phát sự kiện mô phỏng. Giao diện chưa tuyên bố đã chặn USB hoặc tự khóa hệ điều hành trên thiết bị." : "MVP policies store configuration and publish simulated events. This interface does not claim to block USB or lock an operating system on the endpoint."}
      </div>

      {/* Create Policy Modal */}
      {isCreating && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("createPolicyModalTitle")}</h3>
              <button className="modal-close" onClick={() => setIsCreating(false)} aria-label="Close">×</button>
            </div>
            <form onSubmit={handleCreatePolicy}>
              <div className="form-group">
                <label htmlFor="p-name">{t("policyNameLabel")}</label>
                <input
                  id="p-name"
                  className="form-input"
                  required
                  placeholder={lang === "vi" ? "VD: Chính sách máy văn phòng tiêu chuẩn" : "e.g. Standard Workstation Policy"}
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="p-timeout">{t("idleTimeoutLabel")}</label>
                <input
                  id="p-timeout"
                  type="number"
                  min="1"
                  max="1440"
                  className="form-input"
                  required
                  value={idleTimeout}
                  onChange={(e) => setIdleTimeout(Number(e.target.value))}
                />
                <small>{lang === "vi" ? "Cấu hình được lưu; Agent MVP chưa áp dụng khóa máy tự động." : "Configuration is stored; the MVP Agent does not apply an automatic device lock."}</small>
              </div>
              <div className="form-group">
                <label htmlFor="p-usb">{t("usbModeLabel")}</label>
                <select
                  id="p-usb"
                  className="form-select"
                  value={usbMode}
                  onChange={(e) => setUsbMode(e.target.value)}
                >
                  <option value="Blocked">{lang === "vi" ? "Blocked (cấu hình mô phỏng)" : "Blocked (configuration only)"}</option>
                  <option value="ReadOnly">{lang === "vi" ? "ReadOnly (cấu hình mô phỏng)" : "ReadOnly (configuration only)"}</option>
                  <option value="FullAccess">{lang === "vi" ? "FullAccess (cấu hình mô phỏng)" : "FullAccess (configuration only)"}</option>
                </select>
              </div>
              <div className="btn-row">
                <button type="button" className="action-outline" onClick={() => setIsCreating(false)}>{t("cancel")}</button>
                <button type="submit" className="action" disabled={submitting}>
                  {submitting ? t("saving") : t("save")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Assign Policy Modal */}
      {assigningPolicy && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <div className="modal-header">
              <h3>{t("assignPolicyModalTitle")}: {assigningPolicy.name}</h3>
              <button className="modal-close" onClick={() => setAssigningPolicy(null)} aria-label="Close">×</button>
            </div>
            {loadingDevices ? (
              <p className="subtitle">{t("loading")}</p>
            ) : devices.length === 0 ? (
              <p className="subtitle">{lang === "vi" ? "Không tìm thấy thiết bị nào để gán." : "No devices found to assign."}</p>
            ) : (
              <form onSubmit={handleAssignPolicy}>
                <div className="form-group">
                  <label htmlFor="assign-device">{t("selectDevicePrompt")}</label>
                  <select
                    id="assign-device"
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
                <div className="btn-row">
                  <button type="button" className="action-outline" onClick={() => setAssigningPolicy(null)}>{t("cancel")}</button>
                  <button type="submit" className="action" disabled={submitting}>
                    {submitting ? t("saving") : t("confirm")}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
      {editingPolicy && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal"><div className="modal-header"><h3>{lang === "vi" ? "Sửa chính sách" : "Edit policy"}: {editingPolicy.name}</h3><button className="modal-close" onClick={() => setEditingPolicy(null)} aria-label={t("cancel")}>×</button></div>
            <form onSubmit={handleUpdatePolicy}><div className="form-group"><label htmlFor="edit-p-name">{t("policyNameLabel")}</label><input id="edit-p-name" className="form-input" minLength={2} maxLength={100} required value={name} onChange={e => setName(e.target.value)} /></div><div className="form-group"><label htmlFor="edit-p-timeout">{t("idleTimeoutLabel")}</label><input id="edit-p-timeout" className="form-input" type="number" min="1" max="1440" required value={idleTimeout} onChange={e => setIdleTimeout(Number(e.target.value))} /></div><div className="form-group"><label htmlFor="edit-p-usb">{t("usbModeLabel")}</label><select id="edit-p-usb" className="form-select" value={usbMode} onChange={e => setUsbMode(e.target.value)}>{!(["Blocked", "ReadOnly", "FullAccess"] as string[]).includes(usbMode) && <option value={usbMode}>{usbMode} ({lang === "vi" ? "giá trị cũ; chọn lại" : "legacy value; choose again"})</option>}<option value="Blocked">Blocked</option><option value="ReadOnly">ReadOnly</option><option value="FullAccess">FullAccess</option></select></div><div className="btn-row"><button type="button" className="action-outline" onClick={() => setEditingPolicy(null)}>{t("cancel")}</button><button type="submit" className="action" disabled={submitting}>{submitting ? t("saving") : t("save")}</button></div></form>
          </div>
        </div>
      )}
    </>
  );
}
