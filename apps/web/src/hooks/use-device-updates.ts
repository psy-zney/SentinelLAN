"use client";

import { HubConnectionBuilder } from "@microsoft/signalr";
import { useEffect } from "react";

export function useDeviceUpdates(onUpdate: () => void) {
  useEffect(() => {
    const api = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
    const connection = new HubConnectionBuilder()
      .withUrl(`${api}/hubs/updates`, {
        withCredentials: true,
        headers: { "X-SentinelLAN-CSRF": "1" }
      })
      .withAutomaticReconnect()
      .build();
    let disposed = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    connection.on("device-status", onUpdate);
    connection.onreconnected(onUpdate);
    const start = async () => {
      try {
        await connection.start();
        if (disposed) await connection.stop();
        else onUpdate();
      } catch {
        if (!disposed) retry = setTimeout(() => { void start(); }, 5000);
      }
    };
    connection.onclose(() => {
      if (!disposed) retry = setTimeout(() => { void start(); }, 5000);
    });
    const starting = start();
    return () => {
      disposed = true;
      clearTimeout(retry);
      void starting.finally(() => connection.stop());
    };
  }, [onUpdate]);
}
