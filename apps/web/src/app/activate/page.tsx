import { Suspense } from "react";
import { ActivateView } from "@/components/activate-view";

export default function ActivatePage() {
  return (
    <Suspense fallback={<div style={{ minHeight: "100vh", display: "grid", placeItems: "center" }}><p>Loading...</p></div>}>
      <ActivateView />
    </Suspense>
  );
}
