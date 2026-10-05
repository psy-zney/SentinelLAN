"use client";

import Link from "next/link";
import type { Route } from "next";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useContext, useEffect, useState } from "react";
import { LogoutButton } from "@/components/logout-button";
import { UiIcon, type IconName } from "@/components/ui-icon";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation, type TranslationKey } from "@/lib/i18n";
import type { CurrentSession, Role } from "@/types/api";
import { portalAllows, portalHomeUrl } from "@/lib/portal";

const operatorRoles: readonly Role[] = ["Admin", "Technician"];
const navIcons: Record<string, IconName> = { "/dashboard": "overview", "/support": "support", "/devices": "device", "/my-device": "device", "/scan": "qr", "/policies": "settings", "/commands": "command", "/alerts": "alert", "/audit-logs": "history", "/users": "users" };

const SessionContext = createContext<CurrentSession | null>(null);

export function useCurrentSession() {
  return useContext(SessionContext);
}

const linkDefs: Record<Exclude<Role, "Agent">, readonly (readonly [TranslationKey, string])[]> = {
  Admin: [
    ["dashboard", "/dashboard"],
    ["selfService", "/support"],
    ["devices", "/devices"],
    ["scanQr", "/scan"],
    ["policies", "/policies"],
    ["commands", "/commands"],
    ["alerts", "/alerts"],
    ["auditLogs", "/audit-logs"],
    ["users", "/users"]
  ],
  Technician: [
    ["dashboard", "/dashboard"],
    ["selfService", "/support"],
    ["devices", "/devices"],
    ["scanQr", "/scan"],
    ["policies", "/policies"],
    ["commands", "/commands"],
    ["alerts", "/alerts"]
  ],
  Employee: [
    ["selfService", "/support"],
    ["myDevice", "/my-device"],
    ["scanQr", "/scan"]
  ]
};

const sectionMetaMap: Record<string, { titleKey: TranslationKey; eyebrowKey: TranslationKey }> = {
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
  const pathname = usePathname();
  const { lang, setLang, t } = useTranslation();
  const [session, setSession] = useState<CurrentSession | null>(null);
  const allowedRolesKey = allowedRoles.join("|");

  useEffect(() => {
    let active = true;
    new ApiClient().session()
      .then(value => {
        if (!active) return;
        if (!portalAllows(value.role)) {
          window.location.assign(portalHomeUrl(value.role));
          return;
        }
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
  const currentPath = pathname.replace(/^\/(company|employee)(?=\/|$)/, "");
  const roleLabel = session.role === "Admin" ? (lang === "vi" ? "Quản trị viên" : "Administrator") : session.role === "Technician" ? (lang === "vi" ? "Nhân viên IT" : "IT staff") : (lang === "vi" ? "Nhân viên" : "Employee");
  const areaLabel = session.role === "Employee" ? (lang === "vi" ? "Dành cho nhân viên" : "Employee workspace") : (lang === "vi" ? "Quản trị công ty" : "Company management");

  return (
    <div className="shell">
      <aside className="sidebar">
        <Link className="brand" href={homePathForRole(session.role)}>
          <span className="brand-mark">S</span>
          <span>SentinelLAN</span>
        </Link>
        <p className="brand-caption">{areaLabel}</p>
        <nav className="nav" aria-label={lang === "vi" ? "Menu chính" : "Main navigation"}>
          {links.map(([key, href]) => (
            <Link key={href} href={href as Route} aria-current={currentPath === href || currentPath.startsWith(`${href}/`) ? "page" : undefined}>
              <UiIcon name={navIcons[href]} />
              {t(key)}
            </Link>
          ))}
        </nav>
        <div className="sidebar-account">
          <span className="avatar" aria-hidden="true">{session.displayName.slice(0, 1).toUpperCase()}</span>
          <div><strong>{session.displayName}</strong><small>{roleLabel}</small></div>
        </div>
      </aside>
      <SessionContext.Provider value={session}>
      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">{areaLabel}</p>
            <h1>{sectionMetaMap[title] ? t(sectionMetaMap[title].titleKey) : title}</h1>
          </div>
          <div className="topbar-actions">
            <button
              type="button"
              className="lang-btn"
              onClick={() => setLang(lang === "vi" ? "en" : "vi")}
              title={lang === "vi" ? "Chuyển sang tiếng Anh" : "Switch to Vietnamese"}
              aria-label={lang === "vi" ? "Chuyển sang tiếng Anh" : "Switch to Vietnamese"}
            >
              {lang === "vi" ? "EN" : "VI"}
            </button>
            <LogoutButton />
          </div>
        </header>
        {subtitleKey ? <p className="subtitle page-description">{t(subtitleKey)}</p> : null}
        {children}
      </main>
      </SessionContext.Provider>
    </div>
  );
}
