import { NextResponse, type NextRequest } from "next/server";
import { contentSecurityPolicy } from "./content-security-policy";

export function portalProxy(request: NextRequest) {
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");
  const policy = contentSecurityPolicy({
    nonce,
    development: process.env.NODE_ENV === "development",
    apiUrl: process.env.NEXT_PUBLIC_API_URL ?? "",
    pageOrigin: request.nextUrl.origin
  });
  const headers = new Headers(request.headers);
  // Overwrite caller-provided values; Next extracts the nonce from the request CSP during SSR.
  headers.set("x-nonce", nonce);
  headers.set("Content-Security-Policy", policy);
  const response = NextResponse.next({ request: { headers } });
  response.headers.set("Content-Security-Policy", policy);
  response.headers.set("Cache-Control", "private, no-store");
  return response;
}
