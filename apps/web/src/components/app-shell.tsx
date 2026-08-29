import Link from "next/link";
import { LogoutButton } from "@/components/logout-button";

const links = [["Dashboard", "/dashboard"], ["Devices", "/devices"], ["Policies", "/policies"], ["Commands", "/commands"], ["Alerts", "/alerts"], ["Audit logs", "/audit-logs"], ["Users", "/users"], ["My device", "/my-device"]] as const;
export function AppShell({ title, eyebrow = "Operations", children }: { title: string; eyebrow?: string; children: React.ReactNode }) {
  return <div className="shell"><aside className="sidebar"><Link className="brand" href="/dashboard"><span className="brand-mark">S</span>SentinelLAN</Link><nav className="nav" aria-label="Primary">{links.map(([label, href]) => <Link key={href} href={href}>{label}</Link>)}</nav><div className="sidebar-note">Authorized endpoint management only. Technical telemetry is minimal and lock/isolation actions are simulated in the MVP.</div></aside><main className="content"><header className="topbar"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1></div><LogoutButton /></header>{children}</main></div>;
}
