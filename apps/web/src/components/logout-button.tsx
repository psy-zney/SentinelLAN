"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";

export function LogoutButton() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);

  async function logout() {
    setBusy(true);
    try { await new ApiClient().logout(); }
    finally {
      router.push("/login");
      router.refresh();
      setBusy(false);
    }
  }

  return <button className="logout" type="button" onClick={logout} disabled={busy}>{busy ? "Signing out…" : "Sign out"}</button>;
}
