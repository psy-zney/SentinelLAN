import { AppShell } from "@/components/app-shell"; import { DeviceDetail } from "@/components/device-detail";
export default async function DevicePage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <AppShell title="Device detail" eyebrow="Managed endpoint"><DeviceDetail id={id} /></AppShell>; }
