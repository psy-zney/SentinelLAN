"use client";

import { DeviceTable } from "@/components/device-table";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";

const loadDevices = () => new ApiClient().devices();

export function DevicesView() {
  const { data: devices, error, refresh } = useLiveQuery(loadDevices);
  if (error) return <div role="alert"><p>Unable to load devices. Check your session and API connection.</p><button className="action" onClick={refresh}>Retry</button></div>;
  if (!devices) return <p role="status">Loading managed devices...</p>;
  if (!devices.length) return <p>No managed devices. Enroll an authorized endpoint to get started.</p>;
  return <DeviceTable initialDevices={devices} />;
}
