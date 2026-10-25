import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { contentSecurityPolicy } from "../src/lib/content-security-policy";
import { portalProxy } from "../src/lib/portal-proxy";

afterEach(() => vi.unstubAllEnvs());

function policy(overrides: Partial<Parameters<typeof contentSecurityPolicy>[0]> = {}) {
  return contentSecurityPolicy({
    nonce: "cmV2aWV3LW5vbmNl",
    development: false,
    apiUrl: "https://api.example.test",
    pageOrigin: "https://portal.example.test",
    ...overrides
  });
}

describe("portal CSP", () => {
  it("uses the page origin without allowing localhost when the API override is absent", () => {
    vi.stubEnv("NEXT_PUBLIC_API_URL", undefined);
    const response = portalProxy(new NextRequest("https://portal.example.test/company/login"));
    const csp = response.headers.get("Content-Security-Policy");
    expect(csp).toContain("wss://portal.example.test");
    expect(csp).not.toContain("localhost");
  });

  it("restricts scripts with a nonce and rejects inline script and eval in production", () => {
    const directives = policy().split("; ");
    expect(directives).toContain("default-src 'self'");
    expect(directives.find(value => value.startsWith("script-src "))).toBe("script-src 'self' 'nonce-cmV2aWV3LW5vbmNl' 'strict-dynamic'");
    expect(directives).toContain("object-src 'none'");
    expect(directives).toContain("frame-ancestors 'none'");
  });

  it("permits only the configured API and page origins for HTTP and realtime traffic", () => {
    const directives = policy().split("; ");
    expect(directives.find(value => value.startsWith("connect-src "))).toBe("connect-src 'self' https://portal.example.test wss://portal.example.test https://api.example.test wss://api.example.test");
    expect(directives.find(value => value.startsWith("img-src "))).toContain("https://api.example.test");
    expect(policy({ apiUrl: "" })).not.toContain("api.example.test");
    expect(policy({ apiUrl: "/" })).not.toContain("api.example.test");
  });

  it("keeps development eval and local websockets out of the production policy", () => {
    expect(policy({ development: true, pageOrigin: "http://localhost:3000" })).toContain("'unsafe-eval'");
    expect(policy({ development: true, pageOrigin: "http://localhost:3000" })).toContain("ws://localhost:3000");
    expect(policy()).not.toContain("'unsafe-eval'");
  });

  it("rejects header injection, credential-bearing origins and executable URL schemes", () => {
    expect(() => policy({ nonce: "nonce'; script-src *" })).toThrow("Invalid CSP nonce");
    expect(() => policy({ apiUrl: "https://user:password@example.test" })).toThrow("without credentials");
    expect(() => policy({ apiUrl: "javascript:alert(1)" })).toThrow("HTTP(S)");
  });

  it("replaces supplied SSR headers, gives every document a fresh nonce, and prevents shared caching", () => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "");
    const request = () => new NextRequest("https://portal.example.test/company/login", { headers: {
      "x-nonce": "attacker-nonce", "Content-Security-Policy": "script-src * 'unsafe-inline'"
    } });
    const first = portalProxy(request());
    const second = portalProxy(request());
    const csp = first.headers.get("Content-Security-Policy");
    expect(csp).toBeTruthy();
    expect(csp).not.toContain("attacker-nonce");
    expect(csp).not.toBe(second.headers.get("Content-Security-Policy"));
    expect(first.headers.get("x-middleware-request-content-security-policy")).toBe(csp);
    expect(first.headers.get("x-middleware-request-x-nonce")).toMatch(/^[A-Za-z0-9+/]+={0,2}$/);
    expect(first.headers.get("Cache-Control")).toBe("private, no-store");
  });
});
