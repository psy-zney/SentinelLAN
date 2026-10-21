"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { platformApi } from "../../lib/api";

export default function PlatformLoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const companyUrl = process.env.NEXT_PUBLIC_COMPANY_URL ?? (typeof window !== "undefined" ? `${window.location.protocol}//${window.location.hostname}:3000/company` : "http://localhost:3000/company");
  const employeeUrl = process.env.NEXT_PUBLIC_EMPLOYEE_URL ?? (typeof window !== "undefined" ? `${window.location.protocol}//${window.location.hostname}:3001/employee` : "http://localhost:3001/employee");

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");

    try {
      await platformApi("/auth/login", {
        method: "POST",
        body: JSON.stringify({ email: email.trim(), password })
      });
      router.replace("/");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Chưa đăng nhập được. Hãy kiểm tra email và mật khẩu.");
    } finally {
      setBusy(false);
    }
  }

  function fillDevDemo() {
    setEmail("owner@sentinellan.local");
    setPassword("PlatformOwner#2026!SecureKey");
    setError("");
  }

  return (
    <main className="login-wrap">
      <div className="login">
        <div className="login-brand"><div className="brand"><span className="brand-mark" aria-hidden="true">S</span><span>SentinelLAN</span></div></div>
        <div>
          <p className="eyebrow">Quản lý hệ thống</p>
          <h1>Đăng nhập</h1>
          <p className="subtitle">Dành cho chủ hệ thống quản lý công ty và máy chủ.</p>
        </div>

        {error && (
          <div className="alert-box alert-error" role="alert">
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <label className="field">
            Email
            <input
              name="email"
              type="email"
              autoComplete="username"
              required
              placeholder="ten@congty.vn"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>

          <label className="field">
            Mật khẩu
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              placeholder="••••••••••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>

          <button
            type="submit"
            className="primary login-submit"
            disabled={busy || !email || !password}
          >
            {busy ? "Đang đăng nhập…" : "Đăng nhập"}
          </button>
        </form>

        {process.env.NODE_ENV === "development" && <div className="dev-helper">
          <p style={{ margin: "0 0 8px 0", color: "var(--text-muted)", fontSize: "12px" }}>
            Tài khoản thử nghiệm
          </p>
          <button
            type="button"
            className="secondary"
            onClick={fillDevDemo}
            style={{ width: "100%", fontSize: "12px", padding: "6px 12px" }}
          >
            Điền tài khoản chủ hệ thống
          </button>
        </div>}

        <div className="login-footer">
          <div>
            <a href={`${companyUrl}/login`}>Quản trị công ty</a>
            {" · "}
            <a href={`${employeeUrl}/login`}>Nhân viên</a>
          </div>
        </div>
      </div>
    </main>
  );
}
