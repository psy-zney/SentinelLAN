"use client";

import { DeviceTable } from "@/components/device-table";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";

const loadDashboard = () => new ApiClient().dashboard();

export function DashboardView() {
  const { data, error, refresh } = useLiveQuery(loadDashboard);
  if (error) return <div role="alert"><p>Unable to load dashboard. Check your session and API connection.</p><button className="action" onClick={refresh}>Retry</button></div>;
  if (!data) return <p role="status">Loading dashboard...</p>;
  return <><section className="metrics" aria-label="Device summary"><div className="metric"><span>Total devices</span><strong>{data.totalDevices}</strong></div><div className="metric"><span>Online</span><strong>{data.onlineDevices}</strong></div><div className="metric"><span>Offline</span><strong>{data.offlineDevices}</strong></div><div className="metric"><span>Open alerts</span><strong>{data.openAlerts}</strong></div></section><DeviceTable initialDevices={data.devices} /></>;
}
