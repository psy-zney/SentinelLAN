"use client";

import { useCallback } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { OrganizationItem, UserItem } from "@/types/api";

export function UsersView() {
  const { t, lang } = useTranslation();
  const loadUsersAndOrg = useCallback(async () => {
    const client = new ApiClient();
    const [users, org] = await Promise.all([
      client.users().catch(() => [] as UserItem[]),
      client.organization().catch(() => null as OrganizationItem | null)
    ]);
    return { users, org };
  }, []);

  const { data, error, refresh } = useLiveQuery(loadUsersAndOrg);

  if (error) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>{t("users")}</h2></div>
        <p className="subtitle">{t("error")}</p>
        <button className="action" onClick={refresh}>{t("retry")}</button>
      </div>
    );
  }

  if (!data) return <p className="subtitle" role="status">{t("loading")}</p>;

  const { users, org } = data;

  const getRoleBadge = (role: string) => {
    if (role === "Admin") return <span className="badge" style={{ background: "#0c2626", color: "#eaf5f2" }}>Admin</span>;
    if (role === "Technician") return <span className="badge badge-info">Technician</span>;
    return <span className="badge badge-neutral">Employee</span>;
  };

  return (
    <>
      {org && (
        <section className="metrics" aria-label="Organization identity">
          <div className="metric">
            <span>{t("orgName")}</span>
            <strong className="metric-text">{org.name}</strong>
          </div>
          <div className="metric">
            <span>{t("orgCode")}</span>
            <strong className="metric-text">{org.code}</strong>
          </div>
          <div className="metric">
            <span>{lang === "vi" ? "Tổng tài khoản" : "Total Accounts"}</span>
            <strong>{users.length}</strong>
          </div>
          <div className="metric">
            <span>{lang === "vi" ? "Khởi tạo ngày" : "Tenant Established"}</span>
            <strong className="metric-text">{new Date(org.createdAt).toLocaleDateString()}</strong>
          </div>
        </section>
      )}

      <div className="panel" style={{ marginBottom: "24px" }}>
        <div className="panel-head">
          <div>
            <h2>{t("userDirectory")}</h2>
            <p className="subtitle" style={{ fontSize: ".82rem" }}>
              {t("subtitleUsers")}
            </p>
          </div>
        </div>

        <div style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr>
                <th>{t("userDisplayName")}</th>
                <th>{t("userEmail")}</th>
                <th>{t("userRole")}</th>
                <th>{t("created")}</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id}>
                  <td>
                    <strong>{u.displayName}</strong>
                  </td>
                  <td>{u.email}</td>
                  <td>{getRoleBadge(u.role)}</td>
                  <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="panel">
        <div className="panel-head">
          <h2>{t("rbacTitle")}</h2>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 16, marginTop: 12 }}>
          <div style={{ border: "1px solid #e7eeeb", borderRadius: 12, padding: 16 }}>
            <h3 style={{ margin: "0 0 6px", fontSize: ".95rem" }}>Admin</h3>
            <p className="subtitle" style={{ fontSize: ".8rem" }}>
              {lang === "vi"
                ? "Toàn quyền quản trị: cấu hình chính sách bảo vệ, phát lệnh điều khiển, quản lý người dùng và tra cứu nhật ký kiểm toán."
                : "Full control over organization endpoints, policy creation and assignment, command issuance, user directory, and audit inspection."}
            </p>
          </div>
          <div style={{ border: "1px solid #e7eeeb", borderRadius: 12, padding: 16 }}>
            <h3 style={{ margin: "0 0 6px", fontSize: ".95rem" }}>Technician</h3>
            <p className="subtitle" style={{ fontSize: ".8rem" }}>
              {lang === "vi"
                ? "Vận hành kỹ thuật: theo dõi telemetry trực tiếp, tiếp nhận và xử lý cảnh báo sự cố, phát lệnh an toàn trong allow-list."
                : "Operational management: views live telemetry, acknowledges and resolves alerts, and dispatches allow-listed commands."}
            </p>
          </div>
          <div style={{ border: "1px solid #e7eeeb", borderRadius: 12, padding: 16 }}>
            <h3 style={{ margin: "0 0 6px", fontSize: ".95rem" }}>Employee</h3>
            <p className="subtitle" style={{ fontSize: ".8rem" }}>
              {lang === "vi"
                ? "Minh bạch quyền riêng tư: tự kiểm tra máy được giao, xem chính sách đang áp dụng và lịch sử IT thao tác. Không thể xem máy của đồng nghiệp."
                : "Privacy-first transparency view: inspects own assigned device status, active policies, and recent audit events."}
            </p>
          </div>
        </div>
      </div>
    </>
  );
}
