"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { AuditEvent } from "@/types/api";

export function AuditView() {
  const { t, lang } = useTranslation();
  const [actionFilter, setActionFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");

  const loadLogs = useCallback(() => {
    return new ApiClient().auditLogs(undefined, actionFilter || undefined);
  }, [actionFilter]);

  const { data: logs, error, refresh } = useLiveQuery<AuditEvent[]>(loadLogs);

  if (error) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{t("auditLogs")}</h2></div>
        <p className="subtitle">{t("error")}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  if (!logs) return <p className="subtitle" role="status">{t("loading")}</p>;

  const displayLogs = searchTerm.trim()
    ? logs.filter((l) =>
        (l.action?.toLowerCase() ?? "").includes(searchTerm.toLowerCase()) ||
        (l.reason?.toLowerCase() ?? "").includes(searchTerm.toLowerCase()) ||
        (l.actorName?.toLowerCase() ?? "").includes(searchTerm.toLowerCase()) ||
        (l.deviceName?.toLowerCase() ?? "").includes(searchTerm.toLowerCase())
      )
    : logs;

  const getOutcomeBadge = (outcome: string) => {
    const o = outcome.toLowerCase();
    if (o === "success" || o === "resolved") return <span className="badge badge-success">{t("succeeded")}</span>;
    if (o === "failed" || o === "error") return <span className="badge badge-danger">{t("failed")}</span>;
    if (o === "pending" || o === "open") return <span className="badge badge-warn">{t("pending")}</span>;
    return <span className="badge badge-neutral">{outcome}</span>;
  };

  return (
    <>
      <section className="metrics" aria-label="Audit summary">
        <div className="metric">
          <span>{lang === "vi" ? "Tổng sự kiện kiểm toán" : "Logged Events"}</span>
          <strong>{logs.length}</strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Lệnh & Chính sách" : "Commands & Policies"}</span>
          <strong>{logs.filter((l) => l.action.startsWith("Command") || l.action.startsWith("Policy")).length}</strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Sự kiện cảnh báo" : "Alert Events"}</span>
          <strong>{logs.filter((l) => l.action.startsWith("Alert")).length}</strong>
        </div>
        <div className="metric">
          <span>{lang === "vi" ? "Toàn vẹn dữ liệu" : "Audit Integrity"}</span>
          <strong style={{ color: "var(--accent)" }}>Append-Only</strong>
        </div>
      </section>

      <div className="panel">
        <div className="panel-head">
          <div>
            <h2>{t("auditTrail")}</h2>
            <p className="subtitle" style={{ fontSize: ".82rem" }}>
              {t("subtitleAudit")}
            </p>
          </div>
        </div>

        <div className="filter-bar">
          <input
            className="form-input"
            style={{ maxWidth: 280 }}
            placeholder={lang === "vi" ? "Tìm theo người thao tác, từ khóa..." : "Search by actor, keyword or device..."}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          <select
            className="form-select"
            style={{ maxWidth: 220 }}
            value={actionFilter}
            onChange={(e) => setActionFilter(e.target.value)}
          >
            <option value="">{lang === "vi" ? "Tất cả loại hành động" : "All Action Types"}</option>
            <option value="CommandCreated">{t("commands")}</option>
            <option value="Policy">{t("policies")}</option>
            <option value="Alert">{t("alerts")}</option>
            <option value="Enrollment">{lang === "vi" ? "Ghi danh Agent" : "Enrollments"}</option>
          </select>
          <button className="action-outline" onClick={refresh}>{t("refresh")}</button>
        </div>

        {displayLogs.length === 0 ? (
          <div className="empty-state">
            <p>{t("noAuditLogs")}</p>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>{t("timeUtc")}</th>
                  <th>{t("auditActor")}</th>
                  <th>{t("auditAction")}</th>
                  <th>{t("auditDevice")}</th>
                  <th>{t("auditReason")}</th>
                  <th>{t("auditOutcome")}</th>
                </tr>
              </thead>
              <tbody>
                {displayLogs.map((l) => (
                  <tr key={l.id}>
                    <td style={{ whiteSpace: "nowrap", fontSize: ".82rem" }}>
                      {new Date(l.createdAt).toISOString().replace("T", " ").substring(0, 19)}
                    </td>
                    <td>
                      <strong>{l.actorName ?? (lang === "vi" ? "Hệ thống" : "System")}</strong>
                    </td>
                    <td>
                      <span className="badge badge-info">{l.action}</span>
                    </td>
                    <td>
                      {l.deviceName ? <span>{l.deviceName}</span> : <span style={{ color: "var(--muted)" }}>—</span>}
                    </td>
                    <td style={{ maxWidth: 320 }}>{l.reason}</td>
                    <td>{getOutcomeBadge(l.outcome)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
