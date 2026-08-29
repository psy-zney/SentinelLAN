"use client";

import Link from "next/link";
import type { Route } from "next";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { LogoutButton } from "@/components/logout-button";
import { ApiClient } from "@/lib/api-client";
import { homePathForRole } from "@/lib/auth-routing";
import type { CurrentSession, Role } from "@/types/api";

const operatorRoles: readonly Role[] = ["Admin", "Technician"];
const linksByRole: Record<Exclude<Role, "Agent">, readonly (readonly [string, string])[]> = {
  Admin: [["Dashboard", "/dashboard"], ["Devices", "/devices"], ["Policies", "/policies"], ["Commands", "/commands"], ["Alerts", "/alerts"], ["Audit logs", "/audit-logs"], ["Users", "/users"]],
  Technician: [["Dashboard", "/dashboard"], ["Devices", "/devices"], ["Policies", "/policies"], ["Commands", "/commands"], ["Alerts", "/alerts"]],
  Employee: [["My device", "/my-device"]]
};

export function AppShell({ title, eyebrow = "Operations", allowedRoles = operatorRoles, children }: { title: string; eyebrow?: string; allowedRoles?: readonly Role[]; children: React.ReactNode }) {
  const router = useRouter();
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
      .catch(() => { if (active) router.replace("/login"); });
    return () => { active = false; };
  }, [allowedRolesKey, allowedRoles, router]);

  if (!session || session.role === "Agent") return <main className="shell-loading" role="status">Checking your SentinelLAN session…</main>;

  const links = linksByRole[session.role];
  return <div className="shell"><aside className="sidebar"><Link className="brand" href={homePathForRole(session.role)}><span className="brand-mark">S</span>SentinelLAN</Link><nav className="nav" aria-label="Primary">{links.map(([label, href]) => <Link key={href} href={href as Route}>{label}</Link>)}</nav><div className="sidebar-note">Authorized endpoint management only. Technical telemetry is minimal and lock/isolation actions are simulated in the MVP.</div></aside><main className="content"><header className="topbar"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p className="session-name">{session.displayName} · {session.role}</p></div><LogoutButton /></header>{children}</main></div>;
}
