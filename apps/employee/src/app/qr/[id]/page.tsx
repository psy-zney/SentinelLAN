import { QrPortalView } from "@/components/qr-portal-view";

export default async function QrFieldPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <QrPortalView id={id} />;
}
