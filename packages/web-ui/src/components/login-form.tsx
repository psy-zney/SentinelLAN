"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation } from "@/lib/i18n";
import { portal, portalAllows, portalBaseUrl, portalHomeUrl, portalTitle, platformBaseUrl } from "@/lib/portal";

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
  const otherPortalLabel = portal === "employee" ? (lang === "vi" ? "🏢 Cổng Quản Trị Công Ty (Admin/IT) →" : "🏢 Company Management Portal →") : (lang === "vi" ? "💻 Cổng Nhân Viên Tự Phục Vụ →" : "💻 Employee Portal →");
  const platformLoginUrl = `${platformBaseUrl()}/login`;

  return (
    <form className="login" onSubmit={submit}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
        <p className="eyebrow" style={{ margin: 0 }}>SentinelLAN</p>
        <button
          type="button"
          className="lang-btn"
          onClick={() => setLang(lang === "vi" ? "en" : "vi")}
          style={{ fontSize: ".75rem", padding: "4px 8px" }}
        >
          🌐 {lang === "vi" ? "EN" : "VI"}
        </button>
      </div>
      <h1 style={{ fontSize: "1.8rem" }}>{portalTitle}</h1>
      <p className="subtitle" style={{ fontSize: ".85rem", marginTop: 6, marginBottom: 20 }}>
        {t("signInSubtitle")}
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
          placeholder="vd: demo"
        />
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

      {error && <p role="alert" className="subtitle" style={{ color: "var(--danger)" }}>{error}</p>}

      <button className="action" type="submit" disabled={busy} style={{ width: "100%", marginTop: 12 }}>
        {busy ? t("signingIn") : t("signInBtn")}
      </button>

      {/* Dev helper buttons */}
      <div style={{ marginTop: 16, padding: "10px", background: "rgba(0,0,0,0.15)", borderRadius: 8, textAlign: "center" }}>
        <p style={{ margin: "0 0 6px", fontSize: ".75rem", color: "var(--text-muted)" }}>
          {lang === "vi" ? "Thử nghiệm nhanh (Demo):" : "Quick test credentials:"}
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
                ⚡ Admin
              </button>
              <button
                type="button"
                className="action-outline"
                style={{ padding: "3px 8px", fontSize: ".75rem" }}
                onClick={() => fillDemo("technician")}
              >
                ⚡ IT / Tech
              </button>
            </>
          ) : (
            <button
              type="button"
              className="action-outline"
              style={{ padding: "3px 8px", fontSize: ".75rem" }}
              onClick={() => fillDemo("employee")}
            >
              ⚡ Nhân viên
            </button>
          )}
        </div>
      </div>

      {/* Reciprocal cross-links */}
      <div style={{ marginTop: 20, paddingTop: 14, borderTop: "1px solid var(--border)", fontSize: ".8rem", textAlign: "center", display: "grid", gap: 8 }}>
        <a href={otherPortalUrl} style={{ color: "var(--accent)", textDecoration: "none", fontWeight: 500 }}>
          {otherPortalLabel}
        </a>
        <a href={platformLoginUrl} style={{ color: "#38bdf8", textDecoration: "none", fontWeight: 600 }}>
          🛡️ {lang === "vi" ? "Cổng Chủ Hệ Thống (Platform Owner) →" : "Platform Root Authority Portal →"}
        </a>
      </div>
    </form>
  );
}
