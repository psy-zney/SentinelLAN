import { AppShell } from "@/components/app-shell";
import { MyDeviceView } from "@/components/my-device-view";
import { SupportCenterView } from "@/components/support-center-view";

export default function MyDevicePage() {
  return <AppShell title="Hỗ trợ công việc của bạn" eyebrow="SentinelLAN" allowedRoles={["Employee"]}><SupportCenterView /><details open className="panel" style={{ marginTop: 24 }}><summary>Thông tin máy tính và dữ liệu kỹ thuật</summary><MyDeviceView /></details></AppShell>;
}
