import type { NextRequest } from "next/server";
import { portalProxy } from "@/lib/portal-proxy";

export function proxy(request: NextRequest) { return portalProxy(request); }

export const config = {
  matcher: [{
    source: "/((?!api|hubs|_next/static|_next/image|favicon.ico).*)",
    missing: [
      { type: "header", key: "next-router-prefetch" },
      { type: "header", key: "purpose", value: "prefetch" }
    ]
  }]
};
