"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useDeviceUpdates } from "@/hooks/use-device-updates";

export function useLiveQuery<T>(load: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState(false);
  const generation = useRef(0);
  const active = useRef(false);
  const refresh = useCallback(() => {
    const current = ++generation.current;
    void load().then(value => {
      if (active.current && current === generation.current) { setData(value); setError(false); }
    }).catch(() => {
      if (active.current && current === generation.current) setError(true);
    });
  }, [load]);

  useEffect(() => {
    active.current = true;
    refresh();
    return () => { active.current = false; };
  }, [refresh]);
  useDeviceUpdates(refresh);
  return { data, error, refresh };
}
