import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "SentinelLAN · Chủ hệ thống", robots: { index: false, follow: false } };
export default function Layout({ children }: { children: React.ReactNode }) {
  return <html lang="vi"><body>{children}</body></html>;
}
