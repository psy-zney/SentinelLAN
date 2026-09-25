"use client";

import Link from "next/link";
import type { Route } from "next";
import { useRouter } from "next/navigation";
import { createContext, useContext, useEffect, useState } from "react";
import { LogoutButton } from "@/components/logout-button";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation, type TranslationKey } from "@/lib/i18n";
import type { CurrentSession, Role } from "@/types/api";

const operatorRoles: readonly Role[] = ["Admin", "Technician"];

const SessionContext = createContext<CurrentSession | null>(null);

export function useCurrentSession() {
  return useContext(SessionContext);
}

const linkDefs: Record<Exclude<Role, "Agent">, readonly (readonly [TranslationKey, string])[]> = {
  Admin: [
    ["dashboard", "/dashboard"],
    ["devices", "/devices"],
    ["scanQr", "/scan"],
    ["policies", "/policies"],
    ["commands", "/commands"],
    ["alerts", "/alerts"],
    ["auditLogs", "/audit-logs"],
    ["users", "/users"],
    ["vpsNodes", "/vps-nodes"]
  ],
  Technician: [
    ["dashboard", "/dashboard"],
    ["devices", "/devices"],
    ["scanQr", "/scan"],
    ["policies", "/policies"],
    ["commands", "/commands"],
    ["alerts", "/alerts"],
    ["vpsNodes", "/vps-nodes"]
  ],
  Employee: [
    ["myDevice", "/my-device"],
    ["scanQr", "/scan"]
  ]
};

const sectionMetaMap: Record<string, { titleKey: TranslationKey; eyebrowKey: TranslationKey }> = {
  "Cloud VPS": { titleKey: "vpsNodes", eyebrowKey: "eyebrowCloud" },
  "Policies": { titleKey: "policies", eyebrowKey: "eyebrowGovernance" },
  "Command Center": { titleKey: "commands", eyebrowKey: "eyebrowOperations" },
  "Alerts & Incidents": { titleKey: "alerts", eyebrowKey: "eyebrowMonitoring" },
  "Audit Trail": { titleKey: "auditLogs", eyebrowKey: "eyebrowCompliance" },
  "People & Access": { titleKey: "users", eyebrowKey: "eyebrowDirectory" },
  "Dashboard": { titleKey: "dashboard", eyebrowKey: "eyebrowOperations" },
  "Devices": { titleKey: "devices", eyebrowKey: "eyebrowOperations" },
  "Device Details": { titleKey: "devices", eyebrowKey: "eyebrowOperations" },
  "Your Assigned Device": { titleKey: "myDevice", eyebrowKey: "eyebrowTransparency" },
  "QR Scanner": { titleKey: "scanQr", eyebrowKey: "eyebrowOperations" }
};

export function AppShell({
  title,
  eyebrow = "Operations",
  subtitleKey,
  allowedRoles = operatorRoles,
  children
}: {
  title: string;
  eyebrow?: string;
  subtitleKey?: TranslationKey;
  allowedRoles?: readonly Role[];
  children: React.ReactNode;
}) {
  const router = useRouter();
  const { lang, setLang, t } = useTranslation();
  const [session, setSession] = useState<CurrentSession | null>(null);
  const allowedRolesKey = allowedRoles.join("|");

  useEffect(() => {
    let active = true;
    new ApiClient().session()
      .then(value => {
        if (!active) return;
        if (!allowedRoles.includes(value.role)) {
          router.replace(homePathForRole(value.role));
          return;
        }
        setSession(value);
      })
      .catch(() => {
        if (active) router.replace("/login");
      });
    return () => {
      active = false;
    };
  }, [allowedRolesKey, allowedRoles, router]);

  if (!session || session.role === "Agent") {
    return (
      <main className="shell-loading" role="status">
        {t("loading")}
      </main>
    );
  }

  const links = linkDefs[session.role];

  return (
    <div className="shell">
      <aside className="sidebar">
        <Link className="brand" href={homePathForRole(session.role)}>
          <span className="brand-mark">S</span>
          <span>{t("appName")}</span>
        </Link>
        <nav className="nav" aria-label="Primary">
          {links.map(([key, href]) => (
            <Link key={href} href={href as Route}>
              {t(key)}
            </Link>
          ))}
        </nav>
        <div className="sidebar-note">
          {lang === "vi"
            ? "Hệ thống quản trị thiết bị đầu cuối an toàn. Chỉ thu thập dữ liệu kỹ thuật tối thiểu, không giám sát xâm phạm quyền riêng tư."
            : "Authorized endpoint management only. Technical telemetry is minimal and privacy-first by design."}
        </div>
      </aside>
      <SessionContext.Provider value={session}>
      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">{sectionMetaMap[title] ? t(sectionMetaMap[title].eyebrowKey) : eyebrow}</p>
            <h1>{sectionMetaMap[title] ? t(sectionMetaMap[title].titleKey) : title}</h1>
            <p className="session-name">
              {session.displayName} · {session.role}
            </p>
          </div>
          <div className="topbar-actions">
            <button
              type="button"
              className="lang-btn"
              onClick={() => setLang(lang === "vi" ? "en" : "vi")}
              title={lang === "vi" ? "Chuyển sang tiếng Anh" : "Switch to Vietnamese"}
              aria-label="Toggle language"
            >
              🌐 <span>{lang === "vi" ? "Tiếng Việt" : "English"}</span>
              <span className="active-tag">{lang.toUpperCase()}</span>
            </button>
            <LogoutButton />
          </div>
        </header>
        {subtitleKey ? <p className="subtitle" style={{ marginBottom: 24 }}>{t(subtitleKey)}</p> : null}
        {children}
      </main>
      </SessionContext.Provider>
    </div>
  );
}
