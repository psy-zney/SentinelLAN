import { AppShell } from "@/components/app-shell";
import { SupportCenterView } from "@/components/support-center-view";

export default async function SupportPage({ searchParams }: { searchParams: Promise<{ requestId?: string }> }) {
  const { requestId } = await searchParams;
  const initialRequestId = requestId && /^[0-9a-f-]{36}$/i.test(requestId) ? requestId : "";
  return <AppShell title="Hỗ trợ IT" eyebrow="Hỗ trợ công việc" allowedRoles={["Admin", "Technician", "Employee"]}><SupportCenterView initialRequestId={initialRequestId} /></AppShell>;
}
