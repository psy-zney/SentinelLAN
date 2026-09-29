import { AppShell } from "@/components/app-shell";
import { QrScannerView } from "@/components/qr-scanner-view";
import type { Role } from "@/types/api";

const allRoles: readonly Role[] = ["Admin", "Technician", "Employee"];

export default function ScanPage() {
  return (
    <AppShell title="QR Scanner" eyebrow="Operations" allowedRoles={allRoles}>
      <QrScannerView />
    </AppShell>
  );
}
