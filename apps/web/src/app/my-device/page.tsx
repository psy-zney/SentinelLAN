import { AppShell } from "@/components/app-shell";
import { MyDeviceView } from "@/components/my-device-view";

export default function MyDevicePage() {
  return <AppShell title="My device" eyebrow="Employee transparency" allowedRoles={["Employee"]}><MyDeviceView /></AppShell>;
}
