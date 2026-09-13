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
            {policies.reduce((acc, p) => acc + (p.assignedDevicesCount || 0), 0)}
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
                        {p.assignedDevicesCount || 0} {lang === "vi" ? "thiết bị" : "devices"}
                      </span>
                    </td>
                    <td>{new Date(p.createdAt).toLocaleDateString()}</td>
                    <td>
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
                <small>{lang === "vi" ? "Tự động khóa màn hình sau thời gian không hoạt động này." : "Screen locks automatically after this period of inactivity."}</small>
              </div>
              <div className="form-group">
                <label htmlFor="p-usb">{t("usbModeLabel")}</label>
                <select
                  id="p-usb"
                  className="form-select"
                  value={usbMode}
                  onChange={(e) => setUsbMode(e.target.value)}
                >
                  <option value="Blocked">{lang === "vi" ? "Bị chặn (Chặn hoàn toàn đọc/ghi)" : "Blocked (Full restriction)"}</option>
                  <option value="ReadOnly">{lang === "vi" ? "Chỉ đọc (Ngăn chặn sao chép dữ liệu ra ngoài)" : "ReadOnly (Prevent data exfiltration)"}</option>
                  <option value="FullAccess">{lang === "vi" ? "Toàn quyền (Dành riêng cho máy phòng Lab)" : "FullAccess (Authorized lab devices only)"}</option>
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
    </>
  );
}
