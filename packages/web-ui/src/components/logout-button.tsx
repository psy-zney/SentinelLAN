"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";

export function LogoutButton() {
  const router = useRouter();
  const { t, lang } = useTranslation();
  const [busy, setBusy] = useState(false);

  async function logout() {
    setBusy(true);
    try {
      await new ApiClient().logout();
    } finally {
      router.push("/login");
      router.refresh();
      setBusy(false);
    }
  }

  const label = busy ? (lang === "vi" ? "Đang đăng xuất..." : "Signing out...") : t("logout");

  return (
    <button className="logout" type="button" onClick={logout} disabled={busy}>
      {label}
    </button>
  );
}
