"use client";

import { createContext, useContext, useEffect, useRef } from "react";
import { usePathname, useRouter } from "next/navigation";
import type { Route } from "next";
import { UiIcon } from "@/components/ui-icon";
import { useTranslation } from "@/lib/i18n";
import { portal } from "@/lib/portal";

const BackContext = createContext<((fallback: string) => void) | null>(null);
const publicPath = (path: string) => /^\/(login|activate|qr)(\/|$)/.test(path);
const normalize = (path: string) => path.replace(/^\/(company|employee)(?=\/|$)/, "") || "/";

export function backDestination(path: string, employee = false): string {
  const current = normalize(path);
  if (current.startsWith("/devices/")) return "/devices";
  if (publicPath(current)) return current === "/login" ? "/" : "/login";
  if (employee) return current === "/my-device" ? "/support" : "/my-device";
  return current === "/dashboard" ? "/devices" : "/dashboard";
}

export function NavigationHistory({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const visited = useRef<string[]>([]);
  const popped = useRef(false);

  useEffect(() => {
    const onPop = () => { popped.current = true; };
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
  }, []);

  useEffect(() => {
    const path = normalize(pathname);
    const previous = visited.current.at(-1);
    // Sign-in/activation is a boundary: Back must not reopen a credential-bearing page.
    if (publicPath(path) || previous && publicPath(normalize(previous))) visited.current = [];
    if (popped.current) {
      const index = visited.current.lastIndexOf(pathname);
      visited.current = index < 0 ? [] : visited.current.slice(0, index + 1);
    }
    if (visited.current.at(-1) !== pathname) visited.current.push(pathname);
    popped.current = false;
  }, [pathname]);

  const goBack = (fallback: string) => {
    if (visited.current.length > 1) router.back();
    else {
      // Replacing a direct-entry page creates no browser history entry to return to.
      visited.current = [];
      router.replace(fallback as Route);
    }
  };
  return <BackContext.Provider value={goBack}>{children}</BackContext.Provider>;
}

export function BackNavigation({ employee = portal === "employee" }: { employee?: boolean }) {
  const pathname = usePathname();
  const router = useRouter();
  const goBack = useContext(BackContext);
  const { lang } = useTranslation();
  return <nav className="page-back-bar" aria-label={lang === "vi" ? "Điều hướng trang" : "Page navigation"}>
    <button type="button" className="action-outline page-back-button" onClick={() => {
      const fallback = backDestination(pathname, employee);
      if (goBack) goBack(fallback);
      else router.replace(fallback as Route);
    }}><UiIcon name="back" /><span>{lang === "vi" ? "Quay lại" : "Back"}</span></button>
  </nav>;
}

export function PublicBackNavigation() {
  const pathname = usePathname();
  return publicPath(normalize(pathname)) ? <div className="public-back-navigation"><BackNavigation /></div> : null;
}
