import { notFound } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { PoliciesView } from "@/components/policies-view";
import { CommandsView } from "@/components/commands-view";
import { AlertsView } from "@/components/alerts-view";
import { AuditView } from "@/components/audit-view";
import { UsersView } from "@/components/users-view";
import { VpsNodesView } from "@/components/vps-nodes-view";
import type { Role } from "@/types/api";

type SectionConfig = {
  title: string;
  eyebrow: string;
  subtitle: string;
  allowedRoles: Role[];
  render: () => React.ReactNode;
};

const sections: Record<string, SectionConfig> = {
  "vps-nodes": {
    title: "Cloud VPS",
    eyebrow: "Cloud Infrastructure",
    subtitle: "Remote Linux Cloud VPS management via AES-256 Vault encrypted SSH private keys.",
    allowedRoles: ["Admin", "Technician"],
    render: () => <VpsNodesView />
  },
  policies: {
    title: "Policies",
    eyebrow: "Governance",
    subtitle: "Working hours, idle timeouts, USB mode, and endpoint assignments.",
    allowedRoles: ["Admin", "Technician"],
    render: () => <PoliciesView />
  },
  commands: {
    title: "Command Center",
    eyebrow: "Operations",
    subtitle: "Short-lived cryptographically signed commands with reason, allow-list verification, and audit trail.",
    allowedRoles: ["Admin", "Technician"],
    render: () => <CommandsView />
  },
  alerts: {
    title: "Alerts & Incidents",
    eyebrow: "Monitoring",
    subtitle: "Realtime connectivity anomalies, metric violations, and security events requiring intervention.",
    allowedRoles: ["Admin", "Technician"],
    render: () => <AlertsView />
  },
  "audit-logs": {
    title: "Audit Trail",
    eyebrow: "Compliance",
    subtitle: "Append-only accountability logs for sensitive operations, command dispatch, and policy adjustments.",
    allowedRoles: ["Admin"],
    render: () => <AuditView />
  },
  users: {
    title: "People & Access",
    eyebrow: "Directory",
    subtitle: "Multi-tenant user identity, role assignments, and organizational access boundaries.",
    allowedRoles: ["Admin"],
    render: () => <UsersView />
  }
};

export default async function SectionPage({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const config = sections[section];
  if (!config) notFound();

  return (
    <AppShell title={config.title} eyebrow={config.eyebrow} allowedRoles={config.allowedRoles}>
      <p className="subtitle" style={{ marginBottom: 24 }}>
        {config.subtitle}
      </p>
      {config.render()}
    </AppShell>
  );
}
