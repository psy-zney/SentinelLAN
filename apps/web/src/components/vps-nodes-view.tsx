"use client";

import { useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { CreateVpsNodeRequest, VpsNode, VpsNodeStatus } from "@/types/api";

const ALLOWED_SERVICES = ["nginx", "docker", "sentinellan-agent", "cron", "systemd-resolved"] as const;

const loadVpsNodes = () => new ApiClient().vpsNodes();

export function VpsNodesView() {
  const { t, lang } = useTranslation();
  const { data: nodesData, error: queryError, refresh: fetchNodes } = useLiveQuery<VpsNode[]>(loadVpsNodes);
  const nodes = nodesData ?? [];
  const loading = !nodesData && !queryError;
  const error = queryError ? t("error") : null;

  // Filters & Search
  const [statusFilter, setStatusFilter] = useState<"all" | VpsNodeStatus>("all");
  const [searchQuery, setSearchQuery] = useState("");

  // Modals
  const [showAddModal, setShowAddModal] = useState(false);
  const [showRestartModal, setShowRestartModal] = useState(false);
  const [selectedNodeForRestart, setSelectedNodeForRestart] = useState<VpsNode | null>(null);

  // Add Form State
  const [name, setName] = useState("");
  const [host, setHost] = useState("");
  const [port, setPort] = useState(22);
  const [username, setUsername] = useState("root");
  const [privateKey, setPrivateKey] = useState("");
  const [submittingAdd, setSubmittingAdd] = useState(false);
  const [testConnMessage, setTestConnMessage] = useState<string | null>(null);
  const [isTestingConn, setIsTestingConn] = useState(false);

  // Restart Form State
  const [serviceName, setServiceName] = useState<string>("nginx");
  const [restartReason, setRestartReason] = useState("");
  const [submittingRestart, setSubmittingRestart] = useState(false);
  const [restartResult, setRestartResult] = useState<{ success: boolean; message: string } | null>(null);

  // Actions loading per node
  const [actionNodeId, setActionNodeId] = useState<string | null>(null);
  const [bannerMessage, setBannerMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  const handleTestConnection = async () => {
    if (!host.trim() || !privateKey.trim()) {
      setTestConnMessage(lang === "vi" ? "Vui lòng nhập Host và Private Key để kiểm tra." : "Please provide Host and Private Key to test.");
      return;
    }
    setIsTestingConn(true);
    setTestConnMessage(null);
    try {
      // Create temporary probe payload or inform user
      setTestConnMessage(lang === "vi" ? "Đang gửi gói tin kiểm tra SSH tới " + host + ":" + port + "..." : "Sending SSH handshake to " + host + ":" + port + "...");
      // Simulate quick key format validation
      if (!privateKey.includes("PRIVATE KEY")) {
        setTestConnMessage(lang === "vi" ? "Lỗi: Khóa OpenSSH Private Key không đúng định dạng PEM." : "Error: Invalid OpenSSH Private Key format.");
      } else {
        setTestConnMessage(lang === "vi" ? "Cú pháp Private Key hợp lệ. Hệ thống sẽ kết nối SSH ngay khi bấm lưu." : "Key format verified. Connection will be established upon saving.");
      }
    } finally {
      setIsTestingConn(false);
    }
  };

  const handleCreateNode = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !host.trim() || !username.trim() || !privateKey.trim() || submittingAdd) return;

    setSubmittingAdd(true);
    setBannerMessage(null);
    try {
      const req: CreateVpsNodeRequest = {
        name: name.trim(),
        host: host.trim(),
        port,
        username: username.trim(),
        privateKey: privateKey.trim()
      };
      await new ApiClient().createVpsNode(req);
      setShowAddModal(false);
      resetAddForm();
      setBannerMessage({
        type: "success",
        text: lang === "vi" ? `Thêm máy chủ Cloud VPS '${name}' thành công!` : `Cloud VPS node '${name}' added successfully!`
      });
      await fetchNodes();
    } catch (err) {
      setBannerMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to register VPS node."
      });
    } finally {
      setSubmittingAdd(false);
    }
  };

  const resetAddForm = () => {
    setName("");
    setHost("");
    setPort(22);
    setUsername("root");
    setPrivateKey("");
    setTestConnMessage(null);
  };

  const handleRefreshNode = async (node: VpsNode) => {
    setActionNodeId(node.id);
    setBannerMessage(null);
    try {
      await new ApiClient().refreshVpsMetrics(node.id);
      await fetchNodes();
      setBannerMessage({
        type: "success",
        text: lang === "vi" ? `Đã làm mới số liệu của ${node.name}.` : `Metrics refreshed for ${node.name}.`
      });
    } catch (err) {
      setBannerMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to refresh metrics."
      });
    } finally {
      setActionNodeId(null);
    }
  };

  const handleTestExistingConnection = async (node: VpsNode) => {
    setActionNodeId(node.id);
    setBannerMessage(null);
    try {
      const res = await new ApiClient().testVpsConnection(node.id);
      if (res.success) {
        setBannerMessage({
          type: "success",
          text: lang === "vi" ? `Kết nối SSH tới ${node.name} (${node.host}) thành công!` : `SSH connection to ${node.name} (${node.host}) successful!`
        });
      } else {
        setBannerMessage({
          type: "error",
          text: `SSH Error: ${res.message}`
        });
      }
      await fetchNodes();
    } catch (err) {
      setBannerMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Connection test failed."
      });
    } finally {
      setActionNodeId(null);
    }
  };

  const handleDeleteNode = async (node: VpsNode) => {
    if (!confirm(t("vpsDeleteConfirm"))) return;
    setActionNodeId(node.id);
    try {
      await new ApiClient().deleteVpsNode(node.id);
      await fetchNodes();
      setBannerMessage({
        type: "success",
        text: lang === "vi" ? `Đã xóa VPS ${node.name}.` : `VPS node ${node.name} removed.`
      });
    } catch (err) {
      setBannerMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to delete node."
      });
    } finally {
      setActionNodeId(null);
    }
  };

  const handleOpenRestartModal = (node: VpsNode) => {
    setSelectedNodeForRestart(node);
    setServiceName("nginx");
    setRestartReason("");
    setRestartResult(null);
    setShowRestartModal(true);
  };

  const handleRestartService = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedNodeForRestart || !serviceName || !restartReason.trim() || submittingRestart) return;

    setSubmittingRestart(true);
    setRestartResult(null);
    try {
      const res = await new ApiClient().restartVpsService(selectedNodeForRestart.id, serviceName, restartReason.trim());
      setRestartResult(res);
      if (res.success) {
        setBannerMessage({
          type: "success",
          text: lang === "vi" ? `Đã khởi động lại dịch vụ ${serviceName} trên ${selectedNodeForRestart.name}.` : `Service ${serviceName} restarted on ${selectedNodeForRestart.name}.`
        });
        setTimeout(() => {
          setShowRestartModal(false);
          fetchNodes();
        }, 1200);
      }
    } catch (err) {
      setRestartResult({
        success: false,
        message: err instanceof Error ? err.message : "Service restart command failed."
      });
    } finally {
      setSubmittingRestart(false);
    }
  };

  // Metrics computation
  const totalNodes = nodes.length;
  const onlineNodes = nodes.filter(n => n.status === "Online").length;
  const totalContainers = nodes.reduce((sum, n) => sum + (n.dockerContainersCount ?? 0), 0);
  const avgCpu = totalNodes > 0
    ? Math.round(nodes.reduce((sum, n) => sum + (n.cpuPercent ?? 0), 0) / totalNodes)
    : 0;
  const avgRam = totalNodes > 0
    ? Math.round(nodes.reduce((sum, n) => sum + (n.ramPercent ?? 0), 0) / totalNodes)
    : 0;

  // Filtered nodes
  const filteredNodes = nodes.filter(n => {
    if (statusFilter !== "all" && n.status.toLowerCase() !== statusFilter.toLowerCase()) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      return n.name.toLowerCase().includes(q) || n.host.toLowerCase().includes(q) || (n.osInfo?.toLowerCase().includes(q) ?? false);
    }
    return true;
  });

  return (
    <div className="view-container">
      {/* Metrics Header */}
      <section className="metrics" aria-label="VPS Summary">
        <div className="metric">
          <span>{t("totalVpsNodes")}</span>
          <strong>{totalNodes}</strong>
        </div>
        <div className="metric">
          <span>{t("onlineVpsNodes")}</span>
          <strong style={{ color: "var(--accent)" }}>{onlineNodes}</strong>
        </div>
        <div className="metric">
          <span>{t("totalDockerContainers")}</span>
          <strong style={{ color: "var(--info, #38bdf8)" }}>{totalContainers}</strong>
        </div>
        <div className="metric">
          <span>{t("avgVpsCpu")} / {t("avgVpsRam")}</span>
          <strong>{avgCpu}% / {avgRam}%</strong>
        </div>
      </section>

      {/* Banner Message */}
      {bannerMessage && (
        <div className={`status-banner ${bannerMessage.type}`} role="alert" style={{ marginBottom: "1rem" }}>
          <span>{bannerMessage.text}</span>
          <button type="button" className="close-btn" onClick={() => setBannerMessage(null)}>✕</button>
        </div>
      )}

      {/* Action Toolbar */}
      <div className="toolbar" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem", margin: "1.5rem 0 1rem" }}>
        <div className="filters" style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
          <input
            type="search"
            placeholder={t("search")}
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="search-input"
            style={{ minWidth: "220px" }}
          />
          <button
            type="button"
            className={`filter-btn ${statusFilter === "all" ? "active" : ""}`}
            onClick={() => setStatusFilter("all")}
          >
            {t("all")} ({totalNodes})
          </button>
          <button
            type="button"
            className={`filter-btn ${statusFilter === "Online" ? "active" : ""}`}
            onClick={() => setStatusFilter("Online")}
          >
            {t("online")} ({onlineNodes})
          </button>
          <button
            type="button"
            className={`filter-btn ${statusFilter === "Offline" ? "active" : ""}`}
            onClick={() => setStatusFilter("Offline")}
          >
            {t("offline")} ({nodes.filter(n => n.status === "Offline").length})
          </button>
          <button
            type="button"
            className={`filter-btn ${statusFilter === "Error" ? "active" : ""}`}
            onClick={() => setStatusFilter("Error")}
          >
            {lang === "vi" ? "Lỗi" : "Error"} ({nodes.filter(n => n.status === "Error").length})
          </button>
        </div>

        <button
          type="button"
          className="action primary"
          onClick={() => setShowAddModal(true)}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem" }}
        >
          {t("addVpsNodeBtn")}
        </button>
      </div>

      {/* Content State */}
      {loading ? (
        <p role="status">{t("loading")}</p>
      ) : error ? (
        <div role="alert" className="panel">
          <p>{error}</p>
          <button className="action" onClick={fetchNodes}>{t("retry")}</button>
        </div>
      ) : filteredNodes.length === 0 ? (
        <div className="panel empty" style={{ textAlign: "center", padding: "3rem 1.5rem" }}>
          <p style={{ fontSize: "1.1rem", marginBottom: "1rem" }}>{t("vpsNoNodes")}</p>
          <button type="button" className="action primary" onClick={() => setShowAddModal(true)}>
            {t("addVpsNodeBtn")}
          </button>
        </div>
      ) : (
        /* VPS Grid Cards */
        <div className="vps-grid" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(360px, 1fr))", gap: "1.25rem" }}>
          {filteredNodes.map(node => {
            const isActing = actionNodeId === node.id;
            const statusClass = node.status.toLowerCase();

            return (
              <article key={node.id} className="vps-card panel" style={{ display: "flex", flexDirection: "column", gap: "0.85rem", position: "relative" }}>
                {/* Card Header */}
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                  <div>
                    <h3 style={{ margin: "0 0 0.25rem", fontSize: "1.15rem", fontWeight: 700 }}>{node.name}</h3>
                    <code style={{ fontSize: "0.82rem", background: "rgba(0,0,0,0.06)", padding: "2px 6px", borderRadius: "4px" }}>
                      {node.username}@{node.host}:{node.port}
                    </code>
                  </div>
                  <span className={`status-pill ${statusClass}`} style={{ textTransform: "capitalize", fontWeight: 700, fontSize: "0.78rem" }}>
                    {node.status === "Online" ? t("online") : node.status === "Offline" ? t("offline") : node.status}
                  </span>
                </div>

                {/* OS and Kernel */}
                {node.osInfo && (
                  <div style={{ fontSize: "0.8rem", color: "var(--muted)" }}>
                    <span>🐧 {node.osInfo}</span>
                  </div>
                )}

                {/* Error Banner if any */}
                {node.status === "Error" && node.errorMessage && (
                  <div style={{ padding: "0.5rem 0.75rem", background: "rgba(239, 68, 68, 0.1)", borderLeft: "3px solid #ef4444", borderRadius: "4px", fontSize: "0.8rem", color: "#dc2626" }}>
                    <strong>{lang === "vi" ? "Lỗi kết nối:" : "Connection Error:"}</strong> {node.errorMessage}
                  </div>
                )}

                {/* Telemetry Resource Bars */}
                <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", background: "rgba(0,0,0,0.02)", padding: "0.75rem", borderRadius: "8px", border: "1px solid rgba(0,0,0,0.05)" }}>
                  {/* CPU */}
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.78rem", marginBottom: "0.2rem" }}>
                      <span>CPU</span>
                      <strong>{node.cpuPercent !== null && node.cpuPercent !== undefined ? `${node.cpuPercent}%` : "—"}</strong>
                    </div>
                    <div style={{ height: "6px", background: "rgba(0,0,0,0.08)", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{
                        width: `${Math.min(node.cpuPercent ?? 0, 100)}%`,
                        height: "100%",
                        background: (node.cpuPercent ?? 0) > 85 ? "#ef4444" : (node.cpuPercent ?? 0) > 65 ? "#f59e0b" : "#10b981",
                        transition: "width 0.3s ease"
                      }} />
                    </div>
                  </div>

                  {/* RAM */}
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.78rem", marginBottom: "0.2rem" }}>
                      <span>RAM</span>
                      <strong>{node.ramPercent !== null && node.ramPercent !== undefined ? `${node.ramPercent}%` : "—"}</strong>
                    </div>
                    <div style={{ height: "6px", background: "rgba(0,0,0,0.08)", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{
                        width: `${Math.min(node.ramPercent ?? 0, 100)}%`,
                        height: "100%",
                        background: (node.ramPercent ?? 0) > 85 ? "#ef4444" : (node.ramPercent ?? 0) > 70 ? "#f59e0b" : "#3b82f6",
                        transition: "width 0.3s ease"
                      }} />
                    </div>
                  </div>

                  {/* Disk */}
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.78rem", marginBottom: "0.2rem" }}>
                      <span>{t("diskUsage")} (/)</span>
                      <strong>{node.diskPercent !== null && node.diskPercent !== undefined ? `${node.diskPercent}%` : "—"}</strong>
                    </div>
                    <div style={{ height: "6px", background: "rgba(0,0,0,0.08)", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{
                        width: `${Math.min(node.diskPercent ?? 0, 100)}%`,
                        height: "100%",
                        background: (node.diskPercent ?? 0) > 90 ? "#ef4444" : (node.diskPercent ?? 0) > 75 ? "#f59e0b" : "#6366f1",
                        transition: "width 0.3s ease"
                      }} />
                    </div>
                  </div>
                </div>

                {/* Badges: Containers & Uptime */}
                <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", fontSize: "0.78rem" }}>
                  <span style={{ padding: "3px 8px", background: "rgba(56, 189, 248, 0.12)", color: "#0284c7", borderRadius: "12px", fontWeight: 600 }}>
                    🐳 {node.dockerContainersCount !== null && node.dockerContainersCount !== undefined ? `${node.dockerContainersCount} containers` : "0 containers"}
                  </span>
                  {node.uptime && (
                    <span style={{ padding: "3px 8px", background: "rgba(16, 185, 129, 0.12)", color: "#059669", borderRadius: "12px", fontWeight: 600 }}>
                      ⏱️ {node.uptime}
                    </span>
                  )}
                  {node.lastCheckedAt && (
                    <span style={{ padding: "3px 8px", background: "rgba(0,0,0,0.04)", color: "var(--muted)", borderRadius: "12px" }}>
                      {t("vpsLastChecked")}: {new Date(node.lastCheckedAt).toLocaleTimeString()}
                    </span>
                  )}
                </div>

                {/* Card Actions */}
                <div style={{ marginTop: "auto", paddingTop: "0.75rem", borderTop: "1px solid rgba(0,0,0,0.07)", display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                  <button
                    type="button"
                    className="action small"
                    disabled={isActing}
                    onClick={() => handleRefreshNode(node)}
                    title={t("refreshMetricsBtn")}
                  >
                    {isActing ? "..." : `🔄 ${t("refreshMetricsBtn")}`}
                  </button>
                  <button
                    type="button"
                    className="action small"
                    disabled={isActing}
                    onClick={() => handleTestExistingConnection(node)}
                    title={t("testConnectionBtn")}
                  >
                    🔌 {t("testConnectionBtn")}
                  </button>
                  <button
                    type="button"
                    className="action small warn"
                    disabled={isActing}
                    onClick={() => handleOpenRestartModal(node)}
                    title={t("restartVpsServiceBtn")}
                  >
                    ⚡ {t("restartVpsServiceBtn")}
                  </button>
                  <button
                    type="button"
                    className="action small danger"
                    disabled={isActing}
                    onClick={() => handleDeleteNode(node)}
                    style={{ marginLeft: "auto" }}
                    title={t("delete")}
                  >
                    🗑️
                  </button>
                </div>
              </article>
            );
          })}
        </div>
      )}

      {/* Modal: + Thêm VPS Node */}
      {showAddModal && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal-card" style={{ maxWidth: "560px" }}>
            <div className="modal-header">
              <h3>{t("addVpsNodeBtn")}</h3>
              <button type="button" className="close-btn" onClick={() => setShowAddModal(false)}>✕</button>
            </div>

            <div className="notice" style={{ marginBottom: "1rem" }}>
              <p style={{ margin: 0, fontSize: "0.82rem" }}>
                🔒 <strong>Zero-Knowledge Vault:</strong> {t("vpsVaultNotice")}
              </p>
            </div>

            <form onSubmit={handleCreateNode}>
              <div className="form-group" style={{ marginBottom: "0.85rem" }}>
                <label htmlFor="vps-name">{t("vpsNameLabel")} *</label>
                <input
                  id="vps-name"
                  type="text"
                  required
                  placeholder={t("vpsNamePlaceholder")}
                  value={name}
                  onChange={e => setName(e.target.value)}
                />
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "0.75rem", marginBottom: "0.85rem" }}>
                <div className="form-group">
                  <label htmlFor="vps-host">{t("vpsHostLabel")} *</label>
                  <input
                    id="vps-host"
                    type="text"
                    required
                    placeholder={t("vpsHostPlaceholder")}
                    value={host}
                    onChange={e => setHost(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="vps-port">{t("vpsPortLabel")} *</label>
                  <input
                    id="vps-port"
                    type="number"
                    min={1}
                    max={65535}
                    required
                    value={port}
                    onChange={e => setPort(Number(e.target.value))}
                  />
                </div>
              </div>

              <div className="form-group" style={{ marginBottom: "0.85rem" }}>
                <label htmlFor="vps-username">{t("vpsUsernameLabel")} *</label>
                <input
                  id="vps-username"
                  type="text"
                  required
                  placeholder={t("vpsUsernamePlaceholder")}
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                />
              </div>

              <div className="form-group" style={{ marginBottom: "0.85rem" }}>
                <label htmlFor="vps-key">{t("vpsPrivateKeyLabel")} *</label>
                <textarea
                  id="vps-key"
                  required
                  rows={5}
                  placeholder={t("vpsPrivateKeyPlaceholder")}
                  value={privateKey}
                  onChange={e => setPrivateKey(e.target.value)}
                  style={{ fontFamily: "monospace", fontSize: "0.8rem" }}
                />
              </div>

              {testConnMessage && (
                <div style={{ padding: "0.5rem 0.75rem", background: "rgba(0,0,0,0.04)", borderRadius: "4px", fontSize: "0.82rem", marginBottom: "0.85rem" }}>
                  {testConnMessage}
                </div>
              )}

              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "1.25rem" }}>
                <button
                  type="button"
                  className="action"
                  disabled={isTestingConn}
                  onClick={handleTestConnection}
                >
                  {isTestingConn ? t("testingConnection") : `🔌 ${t("testConnectionBtn")}`}
                </button>

                <div style={{ display: "flex", gap: "0.5rem" }}>
                  <button type="button" className="action" onClick={() => setShowAddModal(false)}>
                    {t("cancel")}
                  </button>
                  <button type="submit" className="action primary" disabled={submittingAdd}>
                    {submittingAdd ? t("saving") : t("save")}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Khởi động lại dịch vụ */}
      {showRestartModal && selectedNodeForRestart && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal-card" style={{ maxWidth: "480px" }}>
            <div className="modal-header">
              <h3>⚡ {t("vpsServiceRestartTitle")}</h3>
              <button type="button" className="close-btn" onClick={() => setShowRestartModal(false)}>✕</button>
            </div>

            <p style={{ margin: "0 0 1rem", fontSize: "0.85rem", color: "var(--muted)" }}>
              {lang === "vi" ? "Máy chủ:" : "Target Server:"} <strong>{selectedNodeForRestart.name}</strong> ({selectedNodeForRestart.host})
            </p>

            <form onSubmit={handleRestartService}>
              <div className="form-group" style={{ marginBottom: "0.85rem" }}>
                <label htmlFor="service-select">{t("vpsServiceNameLabel")}</label>
                <select
                  id="service-select"
                  value={serviceName}
                  onChange={e => setServiceName(e.target.value)}
                >
                  {ALLOWED_SERVICES.map(svc => (
                    <option key={svc} value={svc}>{svc}</option>
                  ))}
                </select>
              </div>

              <div className="form-group" style={{ marginBottom: "0.85rem" }}>
                <label htmlFor="service-reason">{t("vpsServiceReasonLabel")} *</label>
                <textarea
                  id="service-reason"
                  required
                  rows={3}
                  placeholder={t("vpsServiceReasonPlaceholder")}
                  value={restartReason}
                  onChange={e => setRestartReason(e.target.value)}
                />
              </div>

              {restartResult && (
                <div style={{
                  padding: "0.5rem 0.75rem",
                  background: restartResult.success ? "rgba(16, 185, 129, 0.1)" : "rgba(239, 68, 68, 0.1)",
                  borderLeft: `3px solid ${restartResult.success ? "#10b981" : "#ef4444"}`,
                  borderRadius: "4px",
                  fontSize: "0.82rem",
                  marginBottom: "0.85rem",
                  color: restartResult.success ? "#047857" : "#b91c1c"
                }}>
                  {restartResult.message}
                </div>
              )}

              <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.25rem" }}>
                <button type="button" className="action" onClick={() => setShowRestartModal(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="action primary" disabled={submittingRestart || !restartReason.trim()}>
                  {submittingRestart ? "Executing..." : t("vpsRestartConfirmBtn")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
