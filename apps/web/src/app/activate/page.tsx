import { Suspense } from "react";
import type { Metadata } from "next";
import { ActivateView } from "@/components/activate-view";

export const metadata: Metadata = {
  referrer: "no-referrer"
};

export default function ActivatePage() {
  return (
    <Suspense fallback={<div style={{ minHeight: "100vh", display: "grid", placeItems: "center" }}><p>Loading...</p></div>}>
      <ActivateView />
    </Suspense>
  );
}
