"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export function useSelfServiceQuery<T>(load: () => Promise<T>, interval = 15000) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const active = useRef(false);
  const pending = useRef(false);
  const refresh = useCallback(async () => {
    if (pending.current || !active.current) return;
    pending.current = true;
    const current = generation.current;
    try {
      const result = await load();
      if (active.current && current === generation.current) { setData(result); setError(""); }
    } catch {
      if (active.current && current === generation.current) setError("Chưa kết nối được với IT. Kiểm tra mạng rồi thử lại; yêu cầu chưa gửi thành công cần được gửi lại.");
    } finally { if (current === generation.current) pending.current = false; }
  }, [load]);
  useEffect(() => {
    active.current = true;
    const currentGeneration = generation.current;
    const initial = window.setTimeout(() => void refresh(), 0);
    const tick = () => { if (document.visibilityState === "visible") void refresh(); };
    const timer = window.setInterval(tick, interval);
    document.addEventListener("visibilitychange", tick);
    window.addEventListener("online", tick);
    return () => {
      active.current = false;
      generation.current = currentGeneration + 1;
      pending.current = false;
      window.clearTimeout(initial);
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", tick);
      window.removeEventListener("online", tick);
    };
  }, [refresh, interval]);
  return { data, error, refresh };
}
