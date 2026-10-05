import type { Metadata } from "next";
import "./globals.css";
import { I18nProvider } from "@/lib/i18n";

export const metadata: Metadata = {
  title: "SentinelLAN · Nhân viên",
  description: "Nhờ IT hỗ trợ và xem thông tin máy tính của bạn."
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="vi">
      <body>
        <I18nProvider>
          {children}
        </I18nProvider>
      </body>
    </html>
  );
}
