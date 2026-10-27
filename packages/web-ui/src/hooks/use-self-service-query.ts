"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export function useSelfServiceQuery<T>(load: () => Promise<T>, interval = 5000) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const active = useRef(false);
  const pending = useRef(false);
  const invalidate = useCallback(() => { generation.current += 1; }, []);
  const refresh = useCallback(async () => {
    if (!active.current) return;
    pending.current = true;
    // A mutation refresh must supersede an older poll, even while it is pending.
    const current = ++generation.current;
    try {
      const result = await load();
      if (active.current && current === generation.current) { setData(result); setError(""); }
    } catch {
      if (active.current && current === generation.current) setError("Chưa kết nối được với IT. Kiểm tra mạng rồi thử lại; yêu cầu chưa gửi thành công cần được gửi lại.");
    } finally { if (current === generation.current) pending.current = false; }
  }, [load]);
  useEffect(() => {
    active.current = true;
    const initial = window.setTimeout(() => void refresh(), 0);
    const tick = () => { if (document.visibilityState === "visible" && !pending.current) void refresh(); };
    const timer = window.setInterval(tick, interval);
    document.addEventListener("visibilitychange", tick);
    window.addEventListener("online", tick);
    window.addEventListener("focus", tick);
    return () => {
      active.current = false;
      invalidate();
      pending.current = false;
      window.clearTimeout(initial);
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", tick);
      window.removeEventListener("online", tick);
      window.removeEventListener("focus", tick);
    };
  }, [refresh, interval, invalidate]);
  return { data, error, refresh };
}
