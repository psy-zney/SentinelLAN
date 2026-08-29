"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";

export function LoginForm() {
  const router = useRouter();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    const data = new FormData(event.currentTarget);
    try {
      await new ApiClient().login(String(data.get("organizationCode")), String(data.get("email")), String(data.get("password")));
      router.push("/dashboard");
      router.refresh();
    } catch { setError("Sign-in failed. Verify the API and development credentials."); setBusy(false); }
  }
  return <form className="login" onSubmit={submit}><p className="eyebrow">SentinelLAN access</p><h1>Welcome back</h1><p className="subtitle">Use a seeded development account. Credentials come from environment variables and are never committed.</p><label className="field">Organization code<input name="organizationCode" type="text" autoComplete="organization" defaultValue="demo" required /></label><label className="field">Email<input name="email" type="email" autoComplete="username" required /></label><label className="field">Password<input name="password" type="password" autoComplete="current-password" required /></label>{error && <p role="alert" className="subtitle">{error}</p>}<button className="action" type="submit" disabled={busy}>{busy ? "Signing in…" : "Sign in securely"}</button></form>;
}
