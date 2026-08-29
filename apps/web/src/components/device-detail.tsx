"use client";

import { useEffect, useState } from "react";
import { ApiClient, demoDashboard } from "@/lib/api-client";
import type { Device } from "@/types/api";

export function DeviceDetail({ id }: { id: string }) {
  const [device, setDevice] = useState<Device>(demoDashboard.devices.find(item => item.id === id) ?? demoDashboard.devices[0]);
  const [message, setMessage] = useState("");
  useEffect(() => { void new ApiClient().device(id).then(setDevice).catch(() => undefined); }, [id]);
  async function simulateLock() {
    try { await new ApiClient().createCommand(device.id, "SimulateLock", "Authorized dashboard demonstration"); setMessage("Simulated lock command queued and audited."); }
    catch { setMessage("Command could not be created. Check permission and API availability."); }
  }
  return <><p className="subtitle">{device.osVersion} · Agent {device.agentVersion}</p><section className="metrics"><div className="metric"><span>Status</span><strong>{device.isOnline ? "Online" : "Offline"}</strong></div><div className="metric"><span>Collection</span><strong>Minimal</strong></div><div className="metric"><span>Agent</span><strong>{device.agentVersion}</strong></div></section><div className="panel"><h2>Safe actions</h2><p className="subtitle">Commands require a reason, expire quickly, are signed and audited. Lock and isolation remain simulations.</p><button className="action" onClick={simulateLock}>Queue simulated lock</button>{message && <p role="status" className="subtitle">{message}</p>}</div></>;
}
