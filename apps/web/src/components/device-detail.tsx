"use client";

import { useCallback, useState } from "react";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";

export function DeviceDetail({ id }: { id: string }) {
  const [message, setMessage] = useState("");
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [sending, setSending] = useState(false);
  const load = useCallback(async () => {
    const client = new ApiClient();
    const [device, telemetry] = await Promise.all([client.device(id), client.deviceTelemetry(id)]);
    return { device, telemetry };
  }, [id]);
  const { data, error, refresh } = useLiveQuery(load);
  const device = data?.device;
  const telemetry = data?.telemetry ?? [];

  async function simulateLock() {
    if (!device || !reason.trim() || !confirmed || sending) return;
    setSending(true);
    try {
      await new ApiClient().createCommand(device.id, "SimulateLock", reason.trim(), confirmed);
      setMessage("Simulated lock command queued and audited.");
      setConfirmed(false);
    } catch {
      setMessage("Command could not be created. Check permission and API availability.");
    } finally { setSending(false); }
  }

  if (!data && !error) {
    return <p className="subtitle" role="status">Loading device details and telemetry...</p>;
  }

  if (error || !device) {
    return (
      <div className="panel" role="alert">
        <div className="panel-head"><h2>Device unavailable</h2></div>
        <p className="subtitle" style={{ padding: "0.5rem 0" }}>Unable to load device or telemetry. Check your permissions and API connection.</p>
        <button className="action" onClick={refresh}>Retry</button>
      </div>
    );
  }

  const latestTelemetry = telemetry.length > 0 ? telemetry[0] : null;

  return (
    <>
      <p className="subtitle">
        {device.name} · {device.osVersion} · Agent {device.agentVersion}
      </p>

      <section className="metrics" aria-label="Device health metrics">
        <div className="metric">
          <span>Status</span>
          <strong style={{ color: device.isOnline ? "var(--accent)" : "var(--danger)" }}>
            {device.isOnline ? "Online" : "Offline"}
          </strong>
        </div>
        <div className="metric">
          <span>CPU utilization</span>
          <strong>{latestTelemetry ? `${latestTelemetry.cpuPercent.toFixed(1)}%` : "N/A"}</strong>
        </div>
        <div className="metric">
          <span>RAM utilization</span>
          <strong>{latestTelemetry ? `${latestTelemetry.ramPercent.toFixed(1)}%` : "N/A"}</strong>
        </div>
        <div className="metric">
          <span>Disk utilization</span>
          <strong>{latestTelemetry ? `${latestTelemetry.diskPercent.toFixed(1)}%` : "N/A"}</strong>
        </div>
      </section>

      <section className="device-facts" style={{ margin: "20px 0" }}>
        <div>
          <dt>Operating system</dt>
          <dd>{device.osVersion}</dd>
        </div>
        <div>
          <dt>Agent version</dt>
          <dd>{device.agentVersion}</dd>
        </div>
        <div>
          <dt>Last heartbeat</dt>
          <dd>{device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString() : "Never seen"}</dd>
        </div>
        <div>
          <dt>Telemetry collection</dt>
          <dd>Minimal technical health metrics only</dd>
        </div>
      </section>

      <div className="panel" style={{ marginTop: "24px" }}>
        <div className="panel-head">
          <h2>Safe operations</h2>
        </div>
        <p className="subtitle">
          Commands require authorization, an explicit reason, short validity, and are signed and audited.
          Device locking and network isolation remain simulations.
        </p>
        <label>Command reason<input value={reason} onChange={event => setReason(event.target.value)} maxLength={1000} required /></label>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I confirm this simulated action on {device.name}</label>
        <button className="action" onClick={simulateLock} type="button" disabled={sending || !confirmed || !reason.trim()}>
          Queue simulated lock
        </button>
        {message && <p role="status" className="subtitle" style={{ marginTop: "12px" }}>{message}</p>}
      </div>

      <div className="privacy" style={{ marginTop: "24px" }}>
        <strong>Privacy disclosure:</strong> SentinelLAN collects only hardware utilization, operating system metadata,
        and agent availability. No screen contents, keystrokes, personal files, browsing activity, camera/microphone,
        or device credentials are ever collected.
      </div>
    </>
  );
}
