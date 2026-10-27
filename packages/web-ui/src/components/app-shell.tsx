"use client";

import Link from "next/link";
import type { Route } from "next";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useContext, useEffect, useState } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import { PageTransition } from "@/components/page-transition";
import { BackNavigation } from "@/components/back-navigation";
import { LogoutButton } from "@/components/logout-button";
import { UiIcon, type IconName } from "@/components/ui-icon";
import { BrandMark } from "@/components/brand-mark";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import { useTranslation, type TranslationKey } from "@/lib/i18n";
import type { CurrentSession, Role } from "@/types/api";
import { portalAllows, portalHomeUrl } from "@/lib/portal";
import { userDisplayName } from "@/lib/user-presentation";

const operatorRoles: readonly Role[] = ["Admin"];
const navIcons: Record<string, IconName> = { "/dashboard": "overview", "/vps": "server", "/support": "support", "/devices": "device", "/my-device": "device", "/scan": "qr", "/policies": "settings", "/commands": "command", "/alerts": "alert", "/audit-logs": "history", "/users": "users" };

const SessionContext = createContext<CurrentSession | null>(null);

export function useCurrentSession() {
  return useContext(SessionContext);
}

const linkDefs: Record<Exclude<Role, "Agent">, readonly (readonly [TranslationKey, string])[]> = {
  Admin: [
    ["dashboard", "/dashboard"],
    ["vpsStatus", "/vps"],
    ["selfService", "/support"],
    ["devices", "/devices"],
    ["scanQr", "/scan"],
    ["policies", "/policies"],
    ["commands", "/commands"],
    ["alerts", "/alerts"],
    ["auditLogs", "/audit-logs"],
    ["users", "/users"]
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
  "VPS Status": { titleKey: "vpsStatus", eyebrowKey: "eyebrowMonitoring" },
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
  const [menuOpen, setMenuOpen] = useState(false);
  const allowedRolesKey = allowedRoles.join("|");

  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 801px)");
    const closeOnDesktop = () => { if (desktop.matches) setMenuOpen(false); };
    desktop.addEventListener("change", closeOnDesktop);
    return () => desktop.removeEventListener("change", closeOnDesktop);
  }, []);

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
      <main className="shell-loading" role="status" aria-live="polite">
        <span className="brand-mark"><BrandMark /></span>
        <span>{t("loading")}</span>
        <div className="loading-preview" aria-hidden="true"><span /><span /><span /></div>
      </main>
    );
  }

  const links = linkDefs[session.role];
  const currentPath = pathname.replace(/^\/(company|employee)(?=\/|$)/, "");
  const roleLabel = session.role === "Admin" ? (lang === "vi" ? "Quản trị viên" : "Administrator") : (lang === "vi" ? "Nhân viên" : "Employee");
  const areaLabel = session.role === "Employee" ? (lang === "vi" ? "Dành cho nhân viên" : "Employee workspace") : (lang === "vi" ? "Quản trị nội bộ" : "Internal administration");
  const displayName = userDisplayName(session.displayName, session.role, lang);
  const employee = session.role === "Employee";
  const navigation = (mobile = false) => (
    <nav className="nav" aria-label={lang === "vi" ? "Menu chính" : "Main navigation"}>
      {links.map(([key, href], index) => (
        <div key={href} className="nav-item">
          {!employee && [0, 2, 5].includes(index) && <p className="nav-group">{index === 0 ? (lang === "vi" ? "Tổng quan" : "Overview") : index === 2 ? (lang === "vi" ? "Công việc hằng ngày" : "Daily work") : (lang === "vi" ? "Quản lý & bảo vệ" : "Manage & protect")}</p>}
          <Link href={href as Route} onClick={mobile ? () => setMenuOpen(false) : undefined} aria-current={currentPath === href || currentPath.startsWith(`${href}/`) ? "page" : undefined}>
            <UiIcon name={navIcons[href]} /><span>{t(key)}</span>
            <UiIcon name="chevron" className="nav-chevron" />
          </Link>
        </div>
      ))}
    </nav>
  );

  return (
    <div className={`shell${employee ? " employee-shell" : ""}`}>
      <a className="skip-link" href="#main-content">{lang === "vi" ? "Đến nội dung chính" : "Skip to content"}</a>
      <aside className="sidebar">
        <Link className="brand" href={homePathForRole(session.role)}>
          <span className="brand-mark"><BrandMark /></span>
          <span>SentinelLAN</span>
        </Link>
        <p className="brand-caption">{areaLabel}</p>
        {navigation()}
        <div className="sidebar-account">
          <span className="avatar" aria-hidden="true">{displayName.slice(0, 1).toUpperCase()}</span>
          <div><strong>{displayName}</strong><small>{roleLabel}</small></div>
        </div>
      </aside>
      <SessionContext.Provider value={session}>
      <main className="content" id="main-content" tabIndex={-1}>
        <BackNavigation employee={employee} />
        <header className="topbar">
          {!employee && <Dialog.Root open={menuOpen} onOpenChange={setMenuOpen}>
            <Dialog.Trigger asChild><button className="mobile-menu action-outline" type="button" aria-label={lang === "vi" ? "Mở menu" : "Open menu"}><UiIcon name="menu" /></button></Dialog.Trigger>
            <Dialog.Portal>
              <Dialog.Overlay className="navigation-overlay" />
              <Dialog.Content className="navigation-drawer">
                <div className="drawer-heading"><Dialog.Title className="brand"><span className="brand-mark"><BrandMark /></span>SentinelLAN</Dialog.Title>
                  <Dialog.Close asChild><button className="action-outline icon-button" aria-label={lang === "vi" ? "Đóng menu" : "Close menu"}><UiIcon name="close" /></button></Dialog.Close>
                </div>
                <Dialog.Description className="subtitle">{areaLabel}</Dialog.Description>
                {navigation(true)}
              </Dialog.Content>
            </Dialog.Portal>
          </Dialog.Root>}
          <div className="topbar-title">
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
        <PageTransition key={currentPath}>
          {subtitleKey ? <p className="subtitle page-description">{t(subtitleKey)}</p> : null}
          {children}
        </PageTransition>
      </main>
      </SessionContext.Provider>
      {employee && <nav className="mobile-bottom-nav" aria-label={lang === "vi" ? "Menu trên điện thoại" : "Mobile navigation"}>
        {links.map(([key, href]) => <Link key={href} href={href as Route} aria-current={currentPath === href || currentPath.startsWith(`${href}/`) ? "page" : undefined}><UiIcon name={navIcons[href]} /><span>{t(key)}</span></Link>)}
      </nav>}
    </div>
  );
}
