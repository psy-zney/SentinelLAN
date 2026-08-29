"use client";

import { HubConnectionBuilder } from "@microsoft/signalr";
import { useEffect } from "react";

export function useDeviceUpdates(onUpdate: (id: string, online: boolean) => void) {
  useEffect(() => {
    const api = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
    const connection = new HubConnectionBuilder().withUrl(`${api}/hubs/updates`, { withCredentials: true }).withAutomaticReconnect().build();
    connection.on("device-status", (event: { id: string; online: boolean }) => onUpdate(event.id, event.online));
    void connection.start().catch(() => undefined);
    return () => { void connection.stop(); };
  }, [onUpdate]);
}
