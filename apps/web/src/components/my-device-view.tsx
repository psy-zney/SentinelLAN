"use client";

import { useEffect, useState } from "react";
import { ApiClient } from "@/lib/api-client";
import type { EmployeeDevice } from "@/types/api";

export function MyDeviceView() {
  const [data, setData] = useState<EmployeeDevice | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    new ApiClient().myDevice()
      .then(value => { if (active) setData(value); })
      .catch(() => { if (active) setError("No assigned device could be loaded. Contact your SentinelLAN administrator."); });
    return () => { active = false; };
  }, []);

  if (error) return <div className="panel" role="alert"><h2>Device unavailable</h2><p className="subtitle">{error}</p></div>;
  if (!data) return <p className="subtitle" role="status">Loading your assigned device…</p>;

  const device = data.device;
  return <><div className="privacy"><strong>What SentinelLAN collects</strong><br />Online status, CPU/RAM/disk utilization, operating-system version, Agent version, and published policy events. It does not collect keystrokes, screen captures, personal content, credentials, or browsing history.</div><section className="metrics" aria-label="Assigned device summary"><div className="metric"><span>Assigned device</span><strong className="metric-text">{device.name}</strong></div><div className="metric"><span>Status</span><strong className="metric-text">{device.isOnline ? "Online" : "Offline"}</strong></div><div className="metric"><span>Applied policy</span><strong className="metric-text">{data.appliedPolicy ?? "Not assigned"}</strong></div><div className="metric"><span>Last heartbeat</span><strong className="metric-text">{device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString() : "Never"}</strong></div></section><div className="panel"><h2>Technical details</h2><dl className="device-facts"><div><dt>Operating system</dt><dd>{device.osVersion}</dd></div><div><dt>Agent version</dt><dd>{device.agentVersion}</dd></div></dl></div><div className="panel action-history"><h2>Recent actions</h2>{data.recentActions.length === 0 ? <p className="subtitle">No administrative actions have affected this device.</p> : <ul>{data.recentActions.map((action, index) => <li key={`${action.createdAt}-${index}`}><strong>{action.action}</strong><span>{action.outcome} · {new Date(action.createdAt).toLocaleString()}</span><small>{action.reason}</small></li>)}</ul>}</div></>;
}
