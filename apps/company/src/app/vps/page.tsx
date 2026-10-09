import { AppShell } from "@/components/app-shell";
import { VpsStatusView } from "@/components/vps-status-view";

export default function VpsStatusPage() {
  return <AppShell title="VPS Status" subtitleKey="subtitleVpsStatus"><VpsStatusView /></AppShell>;
}
