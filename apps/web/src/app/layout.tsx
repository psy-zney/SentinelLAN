import type { Metadata } from "next";
import "./globals.css";
import { I18nProvider } from "@/lib/i18n";

export const metadata: Metadata = {
  title: "SentinelLAN — Quản lý thiết bị đầu cuối được ủy quyền",
  description: "Đăng ký Agent, giám sát telemetry kỹ thuật và xử lý sự cố cho thiết bị được tổ chức cho phép"
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
