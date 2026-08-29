import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = { title: "SentinelLAN", description: "Transparent endpoint management for local networks" };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="en"><body>{children}</body></html>; }
