import type { Metadata } from "next";
import { connection } from "next/server";
import "./globals.css";
import { I18nProvider } from "@/lib/i18n";
import { NavigationHistory, PublicBackNavigation } from "@/components/back-navigation";

export const metadata: Metadata = {
  title: "SentinelLAN · Quản trị nội bộ",
  description: "Quản lý thiết bị và hỗ trợ nhân viên."
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  await connection();
  return (
    <html lang="vi">
      <body>
        <I18nProvider>
          <NavigationHistory><PublicBackNavigation />{children}</NavigationHistory>
        </I18nProvider>
      </body>
    </html>
  );
}
