"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation } from "@/lib/i18n";
import { portal, portalAllows, portalBaseUrl, portalHomeUrl } from "@/lib/portal";

export function LoginForm() {
  const router = useRouter();
  const { lang, setLang, t } = useTranslation();
  const [orgCode, setOrgCode] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      const session = await new ApiClient().login(
        orgCode.trim(),
        email.trim(),
        password
      );
      if (!portalAllows(session.role)) {
        window.location.assign(portalHomeUrl(session.role));
        return;
      }
      router.push(homePathForRole(session.role));
      router.refresh();
    } catch {
      setError(t("loginError"));
      setBusy(false);
    }
  }

  function fillDemo(roleType: "admin" | "technician" | "employee") {
    setOrgCode("demo");
    setPassword("local-demo-only");
    setError("");
    if (roleType === "admin") {
      setEmail("admin@sentinellan.local");
    } else if (roleType === "technician") {
      setEmail("technician@sentinellan.local");
    } else {
      setEmail("employee@sentinellan.local");
    }
  }

  const otherPortalUrl = portal === "employee" ? `${portalBaseUrl("Admin")}/login` : `${portalBaseUrl("Employee")}/login`;
  const otherPortalLabel = portal === "employee" ? (lang === "vi" ? "Đăng nhập cho quản trị viên và IT" : "Administrator and IT sign in") : (lang === "vi" ? "Đăng nhập cho nhân viên" : "Employee sign in");

  return (
    <form className="login" onSubmit={submit}>
      <div className="login-brand">
        <div className="brand"><span className="brand-mark" aria-hidden="true">S</span><span>SentinelLAN</span></div>
        <button
          type="button"
          className="lang-btn"
          onClick={() => setLang(lang === "vi" ? "en" : "vi")}
          aria-label={lang === "vi" ? "Chuyển sang tiếng Anh" : "Switch to Vietnamese"}
        >
          {lang === "vi" ? "EN" : "VI"}
        </button>
      </div>
      <p className="eyebrow">{portal === "employee" ? (lang === "vi" ? "Dành cho nhân viên" : "For employees") : (lang === "vi" ? "Quản trị công ty" : "Company management")}</p>
      <h1>{t("signInBtn")}</h1>
      <p className="subtitle">
        {portal === "employee" ? (lang === "vi" ? "Nhờ IT hỗ trợ và xem máy tính của bạn." : "Get IT help and view your computer.") : (lang === "vi" ? "Quản lý thiết bị và hỗ trợ nhân viên." : "Manage devices and help your team.")}
      </p>

      <label className="field">
        {t("orgCodePrompt")}
        <input
          name="organizationCode"
          type="text"
          autoComplete="organization"
          required
          value={orgCode}
          onChange={(e) => setOrgCode(e.target.value)}
          placeholder={lang === "vi" ? "Ví dụ: sentinellan" : "Example: sentinellan"}
        />
        <small>{lang === "vi" ? "Mã do quản trị viên công ty cung cấp." : "Provided by your company administrator."}</small>
      </label>
      <label className="field">
        {t("emailPrompt")}
        <input
          name="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="user@congty.vn"
        />
      </label>
      <label className="field">
        {t("passwordPrompt")}
        <input
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="••••••••"
        />
      </label>

      {error && <p role="alert" className="login-error">{error}</p>}

      <button className="action login-submit" type="submit" disabled={busy}>
        {busy ? t("signingIn") : t("signInBtn")}
      </button>

      {/* Dev helper buttons */}
      {process.env.NODE_ENV === "development" && <div className="dev-helper">
        <p>
          {lang === "vi" ? "Tài khoản thử nghiệm" : "Test accounts"}
        </p>
        <div style={{ display: "flex", gap: 6, justifyContent: "center", flexWrap: "wrap" }}>
          {portal === "company" ? (
            <>
              <button
                type="button"
                className="action-outline"
                style={{ padding: "3px 8px", fontSize: ".75rem" }}
                onClick={() => fillDemo("admin")}
              >
                {lang === "vi" ? "Quản trị viên" : "Administrator"}
              </button>
              <button
                type="button"
                className="action-outline"
                style={{ padding: "3px 8px", fontSize: ".75rem" }}
                onClick={() => fillDemo("technician")}
              >
                IT
              </button>
            </>
          ) : (
            <button
              type="button"
              className="action-outline"
              style={{ padding: "3px 8px", fontSize: ".75rem" }}
              onClick={() => fillDemo("employee")}
            >
              {lang === "vi" ? "Nhân viên" : "Employee"}
            </button>
          )}
        </div>
      </div>}

      {/* Reciprocal cross-links */}
      <div className="login-footer">
        <a href={otherPortalUrl}>
          {otherPortalLabel}
        </a>
      </div>
    </form>
  );
}
