"use client";

import { useEffect, useState } from "react";
import { DeviceTable } from "@/components/device-table";
import { ApiClient, demoDashboard } from "@/lib/api-client";
import type { Dashboard } from "@/types/api";

export function DashboardView() {
  const [data, setData] = useState<Dashboard>(demoDashboard);
  const [source, setSource] = useState("Demo preview — sign in to load the API organization.");
  useEffect(() => {
    new ApiClient().dashboard().then(value => { setData(value); setSource("Connected to the SentinelLAN API and realtime update hub."); }).catch(() => setSource("Sign in to load your organization — showing the non-sensitive demo preview."));
  }, []);
  return <><p className="subtitle">{source}</p><section className="metrics" aria-label="Device summary"><div className="metric"><span>Total devices</span><strong>{data.totalDevices}</strong></div><div className="metric"><span>Online</span><strong>{data.onlineDevices}</strong></div><div className="metric"><span>Offline</span><strong>{data.offlineDevices}</strong></div><div className="metric"><span>Open alerts</span><strong>{data.openAlerts}</strong></div></section><DeviceTable initialDevices={data.devices} key={data.devices.map(device => device.id).join()} /></>;
}
