// The caller owns requests and cancellation; this scheduler never starts overlapping rounds.
export function startVisiblePolling(run: () => Promise<void>, minutes: number, visibility: Document = document) {
  const interval = Math.min(5, Math.max(1, minutes)) * 60_000;
  let stopped = false;
  let running = false;
  let lastFinished = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const schedule = () => {
    clearTimeout(timer);
    if (!stopped && !visibility.hidden) timer = setTimeout(() => { void tick(); }, Math.max(0, lastFinished + interval - Date.now()));
  };
  const tick = async () => {
    if (stopped || running || visibility.hidden) return;
    running = true;
    try { await run(); }
    finally { running = false; lastFinished = Date.now(); schedule(); }
  };
  const changed = () => {
    clearTimeout(timer);
    if (!visibility.hidden && !running) {
      if (Date.now() >= lastFinished + interval) void tick();
      else schedule();
    }
  };
  visibility.addEventListener("visibilitychange", changed);
  queueMicrotask(() => { void tick(); });
  return () => { stopped = true; clearTimeout(timer); visibility.removeEventListener("visibilitychange", changed); };
}
