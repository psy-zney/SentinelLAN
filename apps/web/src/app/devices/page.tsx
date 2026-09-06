import { AppShell } from "@/components/app-shell";
import { DevicesView } from "@/components/devices-view";

export default function DevicesPage() {
  return (
    <AppShell title="Device inventory">
      <p className="subtitle">Enrollment, availability, Agent version, and minimal health telemetry.</p>
      <DevicesView />
    </AppShell>
  );
}
