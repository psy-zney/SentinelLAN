import type { SVGProps } from "react";
import { ArrowLeft, ArrowRight, Bell, BookOpen, ChevronRight, CircleCheck, Clock3, Download,
  Eye, EyeOff, Headset, History, LayoutDashboard, LoaderCircle, LogOut, Menu,
  MessageSquare, Monitor, QrCode, Server, ShieldCheck, SlidersHorizontal,
  TerminalSquare, TriangleAlert, Users, X, type LucideIcon } from "lucide-react";

const icons = {
  overview: LayoutDashboard, support: Headset, device: Monitor, qr: QrCode,
  settings: SlidersHorizontal, command: TerminalSquare, alert: TriangleAlert,
  history: History, users: Users, menu: Menu, close: X, shield: ShieldCheck,
  logout: LogOut, arrow: ArrowRight, back: ArrowLeft, chevron: ChevronRight, download: Download,
  message: MessageSquare, check: CircleCheck, clock: Clock3, help: BookOpen,
  notification: Bell, eye: Eye, "eye-off": EyeOff, loading: LoaderCircle, server: Server
} satisfies Record<string, LucideIcon>;

export type IconName = keyof typeof icons;

export function UiIcon({ name, className = "", ...props }: SVGProps<SVGSVGElement> & { name: IconName }) {
  const Icon = icons[name];
  return <Icon className={`ui-icon ${className}`} strokeWidth={1.8} aria-hidden="true" focusable="false" {...props} />;
}
