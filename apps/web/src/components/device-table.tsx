"use client";

import Link from "next/link";
import type { Device } from "@/types/api";

export function DeviceTable({ initialDevices }: { initialDevices: Device[] }) {
  const devices = initialDevices;
  return <div className="panel"><div className="panel-head"><h2>Managed devices</h2><Link href="/devices">View inventory →</Link></div><table><thead><tr><th>Device</th><th>Status</th><th>Operating system</th><th>Agent</th><th>Last seen</th></tr></thead><tbody>{devices.map(device => <tr key={device.id}><td><Link href={`/devices/${device.id}`}>{device.name}</Link></td><td><span className={`status ${device.isOnline ? "" : "offline"}`}><span className="dot" />{device.isOnline ? "Online" : "Offline"}</span></td><td>{device.osVersion}</td><td>{device.agentVersion}</td><td>{device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleTimeString() : "Never"}</td></tr>)}</tbody></table></div>;
}
