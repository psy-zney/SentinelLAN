import { notFound } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { PoliciesView } from "@/components/policies-view";
import { CommandsView } from "@/components/commands-view";
import { AlertsView } from "@/components/alerts-view";
import { AuditView } from "@/components/audit-view";
import { UsersView } from "@/components/users-view";
import { VpsNodesView } from "@/components/vps-nodes-view";
import type { TranslationKey } from "@/lib/i18n";
import type { Role } from "@/types/api";

type SectionConfig = {
  title: string;
  eyebrow: string;
  subtitleKey?: TranslationKey;
  allowedRoles: Role[];
  render: () => React.ReactNode;
};

const sections: Record<string, SectionConfig> = {
  "vps-nodes": {
    title: "Cloud VPS",
    eyebrow: "Optional extension",
    subtitleKey: "subtitleVps",
    allowedRoles: ["Admin", "Technician"],
    render: () => <VpsNodesView />
  },
  policies: {
    title: "Policies",
    eyebrow: "Governance",
    allowedRoles: ["Admin", "Technician"],
    render: () => <PoliciesView />
  },
  commands: {
    title: "Command Center",
    eyebrow: "Operations",
    allowedRoles: ["Admin", "Technician"],
    render: () => <CommandsView />
  },
  alerts: {
    title: "Alerts & Incidents",
    eyebrow: "Monitoring",
    allowedRoles: ["Admin", "Technician"],
    render: () => <AlertsView />
  },
  "audit-logs": {
    title: "Audit Trail",
    eyebrow: "Compliance",
    allowedRoles: ["Admin"],
    render: () => <AuditView />
  },
  users: {
    title: "People & Access",
    eyebrow: "Directory",
    allowedRoles: ["Admin"],
    render: () => <UsersView />
  }
};

export default async function SectionPage({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const config = sections[section];
  if (!config) notFound();

  return (
    <AppShell title={config.title} eyebrow={config.eyebrow} subtitleKey={config.subtitleKey} allowedRoles={config.allowedRoles}>
      {config.render()}
    </AppShell>
  );
}
