"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation } from "@/lib/i18n";

export function LoginForm() {
  const router = useRouter();
  const { lang, setLang, t } = useTranslation();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const data = new FormData(event.currentTarget);
    try {
      const session = await new ApiClient().login(
        String(data.get("organizationCode")),
        String(data.get("email")),
        String(data.get("password"))
      );
      router.push(homePathForRole(session.role));
      router.refresh();
    } catch {
      setError(t("loginError"));
      setBusy(false);
    }
  }

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
      <h1 style={{ fontSize: "1.8rem" }}>{t("signInTitle")}</h1>
      <p className="subtitle" style={{ fontSize: ".85rem", marginTop: 6, marginBottom: 20 }}>
        {t("signInSubtitle")}
      </p>

      <label className="field">
        {t("orgCodePrompt")}
        <input name="organizationCode" type="text" autoComplete="organization" required />
      </label>
      <label className="field">
        {t("emailPrompt")}
        <input name="email" type="email" autoComplete="username" required />
      </label>
      <label className="field">
        {t("passwordPrompt")}
        <input name="password" type="password" autoComplete="current-password" required />
      </label>

      {error && <p role="alert" className="subtitle" style={{ color: "var(--danger)" }}>{error}</p>}

      <button className="action" type="submit" disabled={busy} style={{ width: "100%", marginTop: 12 }}>
        {busy ? t("signingIn") : t("signInBtn")}
      </button>
    </form>
  );
}
