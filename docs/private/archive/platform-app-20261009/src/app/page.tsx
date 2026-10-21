"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { VpsMonitor } from "../components/vps-monitor";
import { HostVpsMonitor } from "../components/host-vps-monitor";
import { PlatformDialog } from "../components/platform-dialog";
import { UiIcon } from "@/components/ui-icon";
import {
  platformApi,
  PlatformSessionExpired,
  confirmation,
  type Company,
  type CompanyDetail,
  type Invitation,
  type PlatformSystemStatus
} from "../lib/api";

type ActiveTab = "companies" | "system" | "create";

export default function PlatformDashboardPage() {
  const router = useRouter();

  // State
  const [activeTab, setActiveTab] = useState<ActiveTab>("companies");
  const [companies, setCompanies] = useState<Company[] | null>(null);
  const [systemStatus, setSystemStatus] = useState<PlatformSystemStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [copyError, setCopyError] = useState("");
  const [search, setSearch] = useState("");

  // Modals & Inspection state
  const [inspectedCompany, setInspectedCompany] = useState<CompanyDetail | null>(null);
  const [loadingInspection, setLoadingInspection] = useState(false);
  const [statusTarget, setStatusTarget] = useState<Company | null>(null);
  const [reissueTarget, setReissueTarget] = useState<Company | null>(null);
  const [invitation, setInvitation] = useState<Invitation | null>(null);

  // Load companies
  const loadCompanies = useCallback(async () => {
    const list = await platformApi<Company[]>("/companies");
    setCompanies(list);
  }, []);

  // Load system status
  const loadSystem = useCallback(async () => {
    try {
      const status = await platformApi<PlatformSystemStatus>("/system/status");
      setSystemStatus(status);
    } catch {
      // system status endpoint might be loading
    }
  }, []);

  useEffect(() => {
    let active = true;
    void Promise.all([
      platformApi<Company[]>("/companies"),
      platformApi<PlatformSystemStatus>("/system/status").catch(() => null)
    ])
      .then(([companyList, sys]) => {
        if (active) {
          setCompanies(companyList);
          if (sys) setSystemStatus(sys);
        }
      })
      .catch((cause) => {
        if (active) {
          if (cause instanceof PlatformSessionExpired) {
            router.replace("/login");
          } else {
            setError(cause instanceof Error ? cause.message : "Lỗi kết nối máy chủ");
          }
        }
      });

    return () => {
      active = false;
    };
  }, [router]);

  async function perform(action: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await action();
      await loadCompanies();
      await loadSystem();
    } catch (cause) {
      if (cause instanceof PlatformSessionExpired) {
        router.replace("/login");
      } else {
        setError(cause instanceof Error ? cause.message : "Thao tác không hoàn tất được.");
      }
    } finally {
      setBusy(false);
    }
  }

  // Open deep inspection for a company
  async function inspectCompany(companyId: string) {
    setLoadingInspection(true);
    setError("");
    try {
      const detail = await platformApi<CompanyDetail>(`/companies/${companyId}`);
      setInspectedCompany(detail);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể tải chi tiết công ty.");
    } finally {
      setLoadingInspection(false);
    }
  }

  // Filtered companies
  const filteredCompanies = (companies ?? []).filter((c) => {
    if (!search.trim()) return true;
    const term = search.toLowerCase();
    return c.name.toLowerCase().includes(term) || c.code.toLowerCase().includes(term);
  });

  // Calculate totals
  const totalCompaniesCount = companies?.length ?? 0;
  const activeCompaniesCount = companies?.filter((c) => !c.isSuspended).length ?? 0;
  const totalDevicesCount = companies?.reduce((acc, c) => acc + c.devices, 0) ?? 0;
  const onlineDevicesCount = companies?.reduce((acc, c) => acc + c.onlineDevices, 0) ?? 0;
  const totalUsersCount = companies?.reduce((acc, c) => acc + c.users, 0) ?? 0;

  return (
    <div className="platform-shell">
      <aside className="platform-sidebar">
        <a className="brand" href="/platform"><span className="brand-mark" aria-hidden="true">S</span><span>SentinelLAN</span></a>
        <p className="brand-caption">Quản lý hệ thống</p>
        <nav className="platform-nav" aria-label="Menu chính">
          <button type="button" aria-current={activeTab === "companies" ? "page" : undefined} onClick={() => setActiveTab("companies")}><UiIcon name="users" />Công ty</button>
          <button type="button" aria-current={activeTab === "system" ? "page" : undefined} onClick={() => { setActiveTab("system"); void loadSystem(); }}><UiIcon name="device" />Máy chủ</button>
          <button type="button" aria-current={activeTab === "create" ? "page" : undefined} onClick={() => setActiveTab("create")}><UiIcon name="overview" />Thêm công ty</button>
        </nav>
        <div className="platform-account"><span className="avatar" aria-hidden="true">S</span><div><strong>Chủ hệ thống</strong><small>Quản lý các công ty</small></div></div>
      </aside>
    <main className="app-shell">
      {/* Top Header */}
      <header className="app-header">
        <div>
          <div className="brand-titles">
            <p className="eyebrow">Quản lý hệ thống</p>
            <h1>{activeTab === "companies" ? "Công ty" : activeTab === "system" ? "Máy chủ" : "Thêm công ty"}</h1>
          </div>
        </div>

        <div className="header-actions">
          {systemStatus && (
            <div
              className={`badge ${systemStatus.databaseConnected ? "badge-success" : "badge-danger"}`}
              title={`Thời gian phản hồi dữ liệu: ${systemStatus.databaseLatencyMs} ms`}
            >
              <span className="pulse-dot" />
              <span>{systemStatus.databaseConnected ? "Dữ liệu: Đã kết nối" : "Dữ liệu: Mất kết nối"}</span>
            </div>
          )}

          <button
            className="secondary"
            disabled={busy}
            onClick={() =>
              void perform(async () => {
                await platformApi("/auth/logout", { method: "POST" });
                router.replace("/login");
              })
            }
          >
            Đăng xuất
          </button>
        </div>
      </header>

      {/* Global Alerts */}
      {error && (
        <div className="alert-box alert-error" role="alert">
          <span>{error}</span>
        </div>
      )}
      {notice && (
        <div className="alert-box alert-success" role="status">
          <span>✓</span>
          <span>{notice}</span>
        </div>
      )}

      {/* Top KPI Cards Overview */}
      {activeTab === "companies" && <div className="kpi-grid">
        <div className="kpi-card">
          <div className="kpi-info">
            <span className="kpi-label">Công ty</span>
            <span className="kpi-value">{totalCompaniesCount}</span>
            <span className="kpi-sub">
              {activeCompaniesCount} hoạt động · {totalCompaniesCount - activeCompaniesCount} tạm ngưng
            </span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-info">
            <span className="kpi-label">Thiết bị</span>
            <span className="kpi-value">{totalDevicesCount}</span>
            <span className="kpi-sub" style={{ color: "var(--accent)" }}>
              {onlineDevicesCount} máy đang kết nối
            </span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-info">
            <span className="kpi-label">Tài khoản</span>
            <span className="kpi-value">{totalUsersCount}</span>
            <span className="kpi-sub">Quản trị viên, IT và nhân viên</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-info">
            <span className="kpi-label">Đang kết nối</span>
            <span className="kpi-value">
              {onlineDevicesCount}
            </span>
            <span className="kpi-sub">
              Thiết bị gửi dữ liệu về hệ thống
            </span>
          </div>
        </div>
      </div>}

      {/* Tabs Navigation */}

      {/* TAB 1: COMPANIES & DEEP INSPECTION */}
      {activeTab === "companies" && (
        <section className="card">
          <div className="card-header">
            <div>
              <h2>Danh sách công ty</h2>
              <p>Xem tài khoản và thiết bị của từng công ty.</p>
            </div>
            <div className="header-actions"><button className="secondary" disabled={busy} onClick={() => void perform(loadCompanies)}>Làm mới</button><button className="primary" onClick={() => setActiveTab("create")}>Thêm công ty</button></div>
          </div>

          <div className="search-bar">
            <input
              type="text"
              aria-label="Tìm công ty"
              placeholder="Tìm theo tên hoặc mã công ty"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {!companies ? (
            <p style={{ textAlign: "center", padding: "40px 0", color: "var(--text-muted)" }}>
              Đang tải danh sách công ty từ máy chủ…
            </p>
          ) : filteredCompanies.length === 0 ? (
            <p style={{ textAlign: "center", padding: "40px 0", color: "var(--text-muted)" }}>
              {search ? "Không tìm thấy công ty phù hợp với từ khóa." : "Chưa có công ty. Chọn “Thêm công ty” để bắt đầu."}
            </p>
          ) : (
            <div className="table-wrap">
              <table className="company-table">
                <thead>
                  <tr>
                    <th>Công ty</th>
                    <th>Trạng thái</th>
                    <th>Tài khoản</th>
                    <th>Thiết bị</th>
                    <th>Kết nối gần nhất</th>
                    <th style={{ textAlign: "right" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCompanies.map((c) => (
                    <tr key={c.id}>
                      <td>
                        <strong>{c.name}</strong>
                        <br />
                        <code style={{ fontSize: "11.5px", color: "var(--accent-cyan)" }}>{c.code}</code>
                      </td>
                      <td>
                        <span className={`badge ${c.isSuspended ? "badge-warning" : "badge-success"}`}>
                          <span className="pulse-dot" />
                          {c.isSuspended ? "Tạm ngưng" : "Đang hoạt động"}
                        </span>
                      </td>
                      <td>{c.users} tài khoản</td>
                      <td>
                        <span style={{ fontWeight: 600, color: c.onlineDevices > 0 ? "var(--accent)" : "inherit" }}>
                          {c.onlineDevices}
                        </span>
                        /{c.devices} đang kết nối
                      </td>
                      <td style={{ fontSize: "12.5px", color: "var(--text-muted)" }}>
                        {c.lastSeenAt ? new Date(c.lastSeenAt).toLocaleString("vi-VN") : "Chưa có kết nối"}
                      </td>
                      <td style={{ textAlign: "right" }}>
                        <div style={{ display: "inline-flex", gap: "8px" }}>
                          <button
                            className="primary"
                            style={{ padding: "6px 12px", fontSize: "12.5px" }}
                            onClick={() => void inspectCompany(c.id)}
                            disabled={loadingInspection}
                            title="Xem tài khoản và thiết bị"
                          >
                            Xem chi tiết
                          </button>
                          <button
                            className={c.isSuspended ? "secondary" : "danger"}
                            style={{ padding: "6px 12px", fontSize: "12.5px" }}
                            onClick={() => setStatusTarget(c)}
                            title={c.isSuspended ? "Mở lại quyền sử dụng cho công ty" : "Tạm ngưng quyền sử dụng của công ty"}
                          >
                            {c.isSuspended ? "Mở lại" : "Tạm ngưng"}
                          </button>
                          <button
                            className="secondary"
                            style={{ padding: "6px 12px", fontSize: "12.5px" }}
                            disabled={c.isSuspended}
                            onClick={() => setReissueTarget(c)}
                            title="Tạo lại liên kết cho quản trị viên chưa kích hoạt tài khoản"
                          >
                            Cấp lại lời mời
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* TAB 2: VPS MONITORING & BACKEND SYSTEM STATUS */}
      {activeTab === "system" && (
        <section className="system-section">
          <div className="card-header">
            <div>
              <h2>Tình trạng hệ thống</h2>
              <p>Kiểm tra dữ liệu, ứng dụng và các máy chủ đã kết nối.</p>
            </div>
          </div>

          <HostVpsMonitor />
          <details className="host-details"><summary>Ứng dụng và dữ liệu</summary>
          {!systemStatus ? (
            <p style={{ textAlign: "center", padding: "40px 0", color: "var(--text-muted)" }}>
              Đang kiểm tra hệ thống…
            </p>
          ) : (
            <div>
              {/* Database & Host Metrics */}
              <div className="card grid-2 system-summary">
                <div className="system-facts">
                  <h3 style={{ margin: "0 0 12px 0", fontSize: "15px", color: "var(--accent-cyan)" }}>
                    Dữ liệu
                  </h3>
                  <div style={{ display: "grid", gap: "8px", fontSize: "13.5px" }}>
                    <div>
                      <strong>Trạng thái: </strong>
                      <span className={`badge ${systemStatus.databaseConnected ? "badge-success" : "badge-danger"}`}>
                        {systemStatus.databaseConnected ? "Kết nối thành công" : "Mất kết nối"}
                      </span>
                    </div>
                    <div>
                      <strong>Thời gian phản hồi: </strong>
                      <span>{systemStatus.databaseLatencyMs} ms</span>
                    </div>
                    <div>
                      <strong>Hệ quản trị dữ liệu: </strong>
                      <code>{systemStatus.databaseProvider}</code>
                    </div>
                  </div>
                </div>

                <div className="system-facts">
                  <h3 style={{ margin: "0 0 12px 0", fontSize: "15px", color: "var(--accent-cyan)" }}>
                    Ứng dụng SentinelLAN
                  </h3>
                  <div style={{ display: "grid", gap: "8px", fontSize: "13.5px" }}>
                    <div>
                      <strong>Tên máy chạy ứng dụng: </strong>
                      <code>{systemStatus.hostName}</code>
                    </div>
                    <div>
                      <strong>Hệ điều hành: </strong>
                      <span>{systemStatus.osDescription}</span>
                    </div>
                    <div>
                      <strong>Phiên bản .NET: </strong>
                      <code>{systemStatus.runtimeVersion}</code>
                    </div>
                    <div>
                      <strong>Đã chạy liên tục: </strong>
                      <span>{Math.floor(systemStatus.uptimeSeconds / 3600)} giờ {Math.floor((systemStatus.uptimeSeconds % 3600) / 60)} phút</span>
                    </div>
                    <div>
                      <strong>Bộ nhớ ứng dụng sử dụng: </strong>
                      <span>{systemStatus.memoryUsageMb} MB</span>
                    </div>
                  </div>
                </div>
              </div>


            </div>
          )}
          </details>
          <VpsMonitor refreshBackend={loadSystem} />
        </section>
      )}

      {/* TAB 3: PROVISION NEW COMPANY */}
      {activeTab === "create" && (
        <section className="card create-company">
          <div className="card-header">
            <div>
              <h2>Thông tin công ty</h2>
              <p>Thêm công ty và tạo lời mời cho quản trị viên đầu tiên.</p>
            </div>
          </div>

          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = event.currentTarget;
              const data = new FormData(form);
              void perform(async () => {
                const result = await platformApi<Invitation>("/companies", {
                  method: "POST",
                  body: JSON.stringify({
                    code: data.get("code"),
                    name: data.get("name"),
                    adminEmail: data.get("adminEmail"),
                    adminName: data.get("adminName"),
                    confirmation: confirmation(String(data.get("reason")))
                  })
                });
                setInvitation(result);
                form.reset();
                setNotice(`Đã tạo thành công công ty ${result.organizationCode}`);
              });
            }}
          >
            <div className="grid-2">
              <label>
                Mã công ty (viết thường, không dấu)
                <input
                  name="code"
                  pattern="[a-z][a-z0-9-]{2,49}"
                  minLength={3}
                  maxLength={50}
                  required
                  placeholder="vd: acme-corp"
                />
              </label>

              <label>
                Tên công ty
                <input
                  name="name"
                  minLength={2}
                  maxLength={100}
                  required
                  placeholder="vd: Tập đoàn Acme Việt Nam"
                />
              </label>

              <label>
                Email quản trị viên
                <input
                  name="adminEmail"
                  type="email"
                  required
                  placeholder="admin@acme.vn"
                />
              </label>

              <label>
                Tên quản trị viên
                <input
                  name="adminName"
                  minLength={2}
                  maxLength={100}
                  required
                  placeholder="vd: Nguyễn Văn Quản Trị"
                />
              </label>
            </div>

            <label>
              Lý do thêm công ty
              <input
                name="reason"
                minLength={3}
                maxLength={1000}
                required
                placeholder="vd: Hợp đồng triển khai hệ thống SentinelLAN số HD-2026/09..."
              />
            </label>

            <label className="check-label">
              <input type="checkbox" required />
              <span>Tôi được phép tạo công ty và tài khoản quản trị này.</span>
            </label>

            <button type="submit" className="primary" disabled={busy} style={{ marginTop: "12px" }}>
              {busy ? "Đang tạo công ty…" : "Tạo công ty"}
            </button>
          </form>
        </section>
      )}

      {/* INVITATION GENERATED MODAL / NOTICE */}
      {invitation && (
        <PlatformDialog title="Liên kết kích hoạt" close={() => { setInvitation(null); setCopyError(""); }}>
          <div className="modal-dialog">
            <div className="card-header">
              <h2 style={{ color: "var(--accent)" }}>Đã tạo công ty {invitation.organizationCode}</h2>
            </div>
            <p style={{ color: "var(--text-muted)", fontSize: "14px" }}>
              Đã tạo tài khoản quản trị cho <strong>{invitation.adminEmail}</strong>. Gửi riêng liên kết bên dưới cho quản trị viên để họ đặt mật khẩu.
            </p>

            <div style={{ background: "#f5f6f7", padding: "16px", borderRadius: "6px", margin: "16px 0" }}>
              <label style={{ margin: 0 }}>
                Liên kết dùng một lần, hết hạn lúc {new Date(invitation.expiresAt).toLocaleString("vi-VN")}:
                <input
                  readOnly
                  value={
                    new URL(
                      `${process.env.NEXT_PUBLIC_COMPANY_URL ?? "/company"}${invitation.activationUrl.replace(/^\/company/, "")}`,
                      typeof window !== "undefined" ? window.location.origin : "http://localhost"
                    ).href
                  }
                  onFocus={(e) => e.target.select()}
                  style={{ marginTop: "8px", fontWeight: 600, color: "var(--accent-cyan)" }}
                />
              </label>
            </div>

            {copyError && <p className="alert-box alert-error" role="alert">{copyError}</p>}
            <div className="header-actions"><button
              type="button"
              className="primary"
              onClick={async () => {
                const url = new URL(
                  `${process.env.NEXT_PUBLIC_COMPANY_URL ?? "/company"}${invitation.activationUrl.replace(/^\/company/, "")}`,
                  typeof window !== "undefined" ? window.location.origin : "http://localhost"
                ).href;
                try {
                  await navigator.clipboard.writeText(url);
                  setNotice("Đã sao chép liên kết kích hoạt.");
                  setCopyError("");
                  setInvitation(null);
                } catch {
                  setCopyError("Chưa sao chép được. Bạn có thể chọn và sao chép liên kết trong ô bên trên.");
                }
              }}
            >
              Sao chép liên kết
            </button><button type="button" className="secondary" onClick={() => { setInvitation(null); setCopyError(""); }}>Đóng</button></div>
          </div>
        </PlatformDialog>
      )}

      {/* DEEP COMPANY INSPECTION MODAL */}
      {inspectedCompany && (
        <PlatformDialog title={`Chi tiết công ty ${inspectedCompany.name}`} close={() => setInspectedCompany(null)}>
          <div className="modal-dialog wide">
            <div className="card-header">
              <div>
                <h2>Chi tiết công ty: {inspectedCompany.name}</h2>
                <p>
                  Mã công ty: <code>{inspectedCompany.code}</code> · Trạng thái:{" "}
                  <span className={`badge ${inspectedCompany.isSuspended ? "badge-warning" : "badge-success"}`}>
                    {inspectedCompany.isSuspended ? "Tạm ngưng" : "Đang hoạt động"}
                  </span>
                </p>
              </div>
              <button className="secondary" onClick={() => setInspectedCompany(null)}>
                ✕ Đóng
              </button>
            </div>

            {/* Sub-sections inside inspector */}
            <div style={{ display: "grid", gap: "20px", marginTop: "16px" }}>
              {/* Users list */}
              <div>
                <h3 style={{ fontSize: "15px", color: "var(--accent-cyan)", marginBottom: "8px" }}>
                  Tài khoản ({inspectedCompany.users.length})
                </h3>
                {inspectedCompany.users.length === 0 ? (
                  <p style={{ color: "var(--text-muted)", fontSize: "13px" }}>Chưa có tài khoản nào được tạo.</p>
                ) : (
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Tên hiển thị</th>
                          <th>Email</th>
                          <th>Vai trò</th>
                          <th>Trạng thái</th>
                          <th>Ngày tạo</th>
                        </tr>
                      </thead>
                      <tbody>
                        {inspectedCompany.users.map((u) => (
                          <tr key={u.id}>
                            <td><strong>{u.displayName}</strong></td>
                            <td><code>{u.email}</code></td>
                            <td>
                              <span className="badge badge-info">{({ Admin: "Quản trị viên", Technician: "Nhân viên IT", Employee: "Nhân viên" } as Record<string, string>)[u.role] ?? u.role}</span>
                            </td>
                            <td>
                              <span className={`badge ${u.status === "Active" ? "badge-success" : "badge-warning"}`}>
                                {u.status === "Active" ? "Đã kích hoạt" : u.status === "PendingActivation" ? "Chờ kích hoạt" : "Đã khóa"}
                              </span>
                            </td>
                            <td style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                              {new Date(u.createdAt).toLocaleDateString("vi-VN")}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Devices list */}
              <div>
                <h3 style={{ fontSize: "15px", color: "var(--accent-cyan)", marginBottom: "8px" }}>
                  Thiết bị ({inspectedCompany.devices.length})
                </h3>
                {inspectedCompany.devices.length === 0 ? (
                  <p style={{ color: "var(--text-muted)", fontSize: "13px" }}>Chưa có thiết bị kết nối với công ty này.</p>
                ) : (
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Tên máy tính</th>
                          <th>Hệ điều hành</th>
                          <th>Phiên bản SentinelLAN</th>
                          <th>Trạng thái</th>
                          <th>Người sử dụng</th>
                          <th>Kết nối gần nhất</th>
                        </tr>
                      </thead>
                      <tbody>
                        {inspectedCompany.devices.map((d) => (
                          <tr key={d.id}>
                            <td><strong>{d.name}</strong></td>
                            <td>{d.osVersion}</td>
                            <td><code>v{d.agentVersion}</code></td>
                            <td>
                              <span className={`badge ${d.isRevoked ? "badge-danger" : d.isOnline ? "badge-success" : "badge-warning"}`}>
                                {d.isRevoked ? "Đã thu hồi" : d.isOnline ? "Đang kết nối" : "Chưa kết nối"}
                              </span>
                            </td>
                            <td>{d.assignedUserDisplayName ?? <span style={{ color: "var(--text-dim)" }}>Chưa giao máy</span>}</td>
                            <td style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                              {d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleString("vi-VN") : "Chưa kết nối"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Recent Alerts */}
              <div>
                <h3 style={{ fontSize: "15px", color: "var(--accent-cyan)", marginBottom: "8px" }}>
                  Cảnh báo & Sự cố ({inspectedCompany.alerts.length})
                </h3>
                {inspectedCompany.alerts.length === 0 ? (
                  <p style={{ color: "var(--text-muted)", fontSize: "13px" }}>Không có cảnh báo nào được ghi nhận.</p>
                ) : (
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Mức độ</th>
                          <th>Nội dung cảnh báo</th>
                          <th>Thiết bị</th>
                          <th>Trạng thái</th>
                          <th>Thời gian</th>
                        </tr>
                      </thead>
                      <tbody>
                        {inspectedCompany.alerts.map((a) => (
                          <tr key={a.id}>
                            <td>
                              <span className={`badge ${a.severity === "Critical" ? "badge-danger" : "badge-warning"}`}>
                                {({ Critical: "Nghiêm trọng", High: "Cao", Medium: "Vừa", Low: "Thấp", Warning: "Cảnh báo", Info: "Thông tin" } as Record<string, string>)[a.severity] ?? a.severity}
                              </span>
                            </td>
                            <td>{a.message}</td>
                            <td>{a.deviceName ?? "Hệ thống"}</td>
                            <td>{a.isOpen ? "Đang mở" : "Đã xử lý"}</td>
                            <td style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                              {new Date(a.createdAt).toLocaleString("vi-VN")}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Recent Audit Logs */}
              <div>
                <h3 style={{ fontSize: "15px", color: "var(--accent-cyan)", marginBottom: "8px" }}>
                  Hoạt động gần đây ({inspectedCompany.recentAudits.length})
                </h3>
                {inspectedCompany.recentAudits.length === 0 ? (
                  <p style={{ color: "var(--text-muted)", fontSize: "13px" }}>Chưa có nhật ký hoạt động nào.</p>
                ) : (
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Hành động</th>
                          <th>Lý do</th>
                          <th>Kết quả</th>
                          <th>Thời gian</th>
                        </tr>
                      </thead>
                      <tbody>
                        {inspectedCompany.recentAudits.map((l) => (
                          <tr key={l.id}>
                            <td><code>{l.action}</code></td>
                            <td>{l.reason}</td>
                            <td>
                              <span className={`badge ${l.outcome === "Success" ? "badge-success" : "badge-danger"}`}>
                                {l.outcome === "Success" ? "Hoàn tất" : l.outcome === "Failure" || l.outcome === "Failed" ? "Chưa hoàn tất" : l.outcome}
                              </span>
                            </td>
                            <td style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                              {new Date(l.createdAt).toLocaleString("vi-VN")}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>

            <div style={{ marginTop: "24px", textAlign: "right" }}>
              <button className="primary" onClick={() => setInspectedCompany(null)}>
                Đóng
              </button>
            </div>
          </div>
        </PlatformDialog>
      )}

      {/* SUSPEND / RESUME CONFIRMATION MODAL */}
      {statusTarget && (
        <PlatformDialog title={statusTarget.isSuspended ? "Mở lại công ty" : "Tạm ngưng công ty"} busy={busy} close={() => setStatusTarget(null)}>
          <div className="modal-dialog">
            <div className="card-header">
              <h2>{statusTarget.isSuspended ? "Mở lại quyền sử dụng" : "Tạm ngưng quyền sử dụng"}</h2>
            </div>
            <p style={{ color: "var(--text-muted)", fontSize: "14px" }}>
              Công ty: <strong>{statusTarget.name}</strong> (<code>{statusTarget.code}</code>).
              <br />
              {statusTarget.isSuspended
                ? "Nhân viên có thể đăng nhập và các thiết bị có thể kết nối trở lại."
                : "Mọi người trong công ty sẽ bị đăng xuất. Thiết bị cũng ngừng gửi dữ liệu về hệ thống."}
            </p>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                const data = new FormData(event.currentTarget);
                void perform(async () => {
                  await platformApi(`/companies/${statusTarget.id}/status`, {
                    method: "PUT",
                    body: JSON.stringify({
                      isSuspended: !statusTarget.isSuspended,
                      confirmation: confirmation(String(data.get("reason")))
                    })
                  });
                  setStatusTarget(null);
                  setNotice(`Đã cập nhật trạng thái cho ${statusTarget.name}`);
                });
              }}
            >
              <label>
                Lý do thay đổi
                <input
                  name="reason"
                  minLength={3}
                  maxLength={1000}
                  required
                  placeholder="vd: Yêu cầu tạm ngưng dịch vụ theo thỏa thuận..."
                />
              </label>

              <label className="check-label">
                <input type="checkbox" required />
                <span>Tôi xác nhận thay đổi quyền hoạt động của công ty này.</span>
              </label>

              <div style={{ display: "flex", gap: "10px", marginTop: "16px" }}>
                <button
                  type="submit"
                  className={statusTarget.isSuspended ? "primary" : "danger"}
                  disabled={busy}
                >
                  {statusTarget.isSuspended ? "Xác nhận Mở lại" : "Xác nhận Tạm ngưng"}
                </button>
                <button type="button" className="secondary" onClick={() => setStatusTarget(null)}>
                  Hủy
                </button>
              </div>
            </form>
          </div>
        </PlatformDialog>
      )}

      {/* REISSUE ACTIVATION LINK MODAL */}
      {reissueTarget && (
        <PlatformDialog title="Cấp lại lời mời" busy={busy} close={() => setReissueTarget(null)}>
          <div className="modal-dialog">
            <div className="card-header">
              <h2>Cấp lại lời mời — {reissueTarget.name}</h2>
            </div>
            <p style={{ color: "var(--text-muted)", fontSize: "14px" }}>
              Dùng khi quản trị viên chưa kích hoạt tài khoản. Liên kết cũ sẽ hết hiệu lực; liên kết mới dùng được trong 24 giờ.
            </p>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                const data = new FormData(event.currentTarget);
                void perform(async () => {
                  const result = await platformApi<Invitation>(`/companies/${reissueTarget.id}/invitation`, {
                    method: "POST",
                    body: JSON.stringify(confirmation(String(data.get("reason"))))
                  });
                  setReissueTarget(null);
                  setInvitation(result);
                });
              }}
            >
              <label>
                Lý do cấp lại lời mời
                <input
                  name="reason"
                  minLength={3}
                  maxLength={1000}
                  required
                  placeholder="Ví dụ: Quản trị viên làm mất liên kết cũ"
                />
              </label>

              <label className="check-label">
                <input type="checkbox" required />
                <span>Tôi xác nhận tạo lại lời mời cho quản trị viên này.</span>
              </label>

              <div style={{ display: "flex", gap: "10px", marginTop: "16px" }}>
                <button type="submit" className="primary" disabled={busy}>
                  Tạo lời mời mới
                </button>
                <button type="button" className="secondary" onClick={() => setReissueTarget(null)}>
                  Hủy
                </button>
              </div>
            </form>
          </div>
        </PlatformDialog>
      )}
    </main>
    </div>
  );
}
