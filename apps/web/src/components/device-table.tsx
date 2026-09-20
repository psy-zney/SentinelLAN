"use client";

import Link from "next/link";
import type { Device } from "@/types/api";
import { useTranslation } from "@/lib/i18n";

export function DeviceTable({ initialDevices }: { initialDevices: Device[] }) {
  const { t, lang } = useTranslation();
  const devices = initialDevices;

  return (
    <div className="panel">
      <div className="panel-head">
        <h2>{t("managedDevices")}</h2>
        <Link href="/devices" style={{ fontSize: ".85rem", color: "var(--accent)", fontWeight: 700 }}>
          {lang === "vi" ? "Xem danh mục chi tiết →" : "View inventory →"}
        </Link>
      </div>
      <table>
        <thead>
          <tr>
            <th>{t("deviceName")}</th>
            <th>{t("status")}</th>
            <th>{lang === "vi" ? "Phân công" : "Assignment"}</th>
            <th>{t("osVersion")}</th>
            <th>{t("agentVersion")}</th>
            <th>{t("lastSeen")}</th>
          </tr>
        </thead>
        <tbody>
          {devices.map(device => (
            <tr key={device.id}>
              <td>
                <Link href={`/devices/${device.id}`} style={{ fontWeight: 600 }}>
                  {device.name}
                </Link>
              </td>
              <td>
                <span className={`status ${device.isOnline ? "" : "offline"}`}>
                  <span className="dot" />
                  {device.isOnline ? t("online") : t("offline")}
                </span>
              </td>
              <td>{device.isRevoked ? <span className="badge badge-danger">{lang === "vi" ? "Đã thu hồi" : "Revoked"}</span> : device.assignedUserId ? <span className="badge badge-info">{lang === "vi" ? "Đã gán" : "Assigned"}</span> : <span className="badge badge-neutral">{lang === "vi" ? "Chưa gán" : "Unassigned"}</span>}</td>
              <td>{device.osVersion}</td>
              <td><code>{device.agentVersion}</code></td>
              <td>
                {device.lastSeenAt
                  ? new Date(device.lastSeenAt).toLocaleTimeString()
                  : t("none")}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
