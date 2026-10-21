"use client";

import { useEffect, useRef, type ReactNode } from "react";

export function PlatformDialog({ title, busy = false, close, children }: { title: string; busy?: boolean; close: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const previous = document.activeElement;
    ref.current?.showModal();
    return () => { if (previous instanceof HTMLElement) previous.focus(); };
  }, []);
  return <dialog ref={ref} className="platform-dialog" aria-label={title} onCancel={event => { event.preventDefault(); if (!busy) close(); }}>{children}</dialog>;
}
