import type { SVGProps } from "react";

export type IconName = "overview" | "support" | "device" | "qr" | "settings" | "command" | "alert" | "history" | "users";

const paths: Record<IconName, React.ReactNode> = {
  overview: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
  support: <><path d="M4 13v-1a8 8 0 0 1 16 0v1M20 17v1a3 3 0 0 1-3 3h-4" /><rect x="2" y="12" width="4" height="6" rx="1" /><rect x="18" y="12" width="4" height="6" rx="1" /></>,
  device: <><rect x="3" y="3" width="18" height="13" rx="2" /><path d="M8 21h8M12 16v5" /></>,
  qr: <><path d="M3 8V3h5M16 3h5v5M21 16v5h-5M8 21H3v-5" /><rect x="7" y="7" width="3" height="3" /><rect x="14" y="7" width="3" height="3" /><path d="M7 14h3v3H7zM14 14h3v3h-3" /></>,
  settings: <><path d="M4 6h16M4 12h16M4 18h16" /><circle cx="8" cy="6" r="2" fill="currentColor" /><circle cx="16" cy="12" r="2" fill="currentColor" /><circle cx="10" cy="18" r="2" fill="currentColor" /></>,
  command: <><rect x="3" y="4" width="18" height="16" rx="2" /><path d="m7 8 4 4-4 4M14 16h3" /></>,
  alert: <><path d="m12 3 10 18H2L12 3Z" /><path d="M12 9v5M12 17h.01" /></>,
  history: <><path d="M3 11a9 9 0 1 1 2 7M3 5v6h6M12 7v5l3 2" /></>,
  users: <><circle cx="9" cy="7" r="3" /><path d="M3 21v-2a6 6 0 0 1 12 0v2M16 4a3 3 0 0 1 0 6M17 14a5 5 0 0 1 4 5v2" /></>
};

export function UiIcon({ name, ...props }: SVGProps<SVGSVGElement> & { name: IconName }) {
  return <svg className="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>{paths[name]}</svg>;
}
