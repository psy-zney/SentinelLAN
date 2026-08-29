import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiClient, demoDashboard } from "../src/lib/api-client";
import { homePathForRole } from "../src/lib/auth-routing";

beforeEach(() => vi.unstubAllGlobals());

describe("dashboard model", () => {
  it("keeps online and offline totals consistent", () => {
    expect(demoDashboard.onlineDevices + demoDashboard.offlineDevices).toBe(demoDashboard.totalDevices);
    expect(demoDashboard.devices.filter(device => device.isOnline)).toHaveLength(demoDashboard.onlineDevices);
  });
});

describe("cookie session client", () => {
  it("routes Employee to the assigned-device view and operators to the dashboard", () => {
    expect(homePathForRole("Employee")).toBe("/my-device");
    expect(homePathForRole("Admin")).toBe("/dashboard");
    expect(homePathForRole("Technician")).toBe("/dashboard");
  });

  it("logs in without exposing or storing an access token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ expiresIn: 900, role: "Admin", displayName: "Demo Admin" }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    const session = await new ApiClient().login("demo", "admin@sentinellan.local", "password");

    expect(session).toEqual({ expiresIn: 900, role: "Admin", displayName: "Demo Admin" });
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/auth/login");
    expect(init.credentials).toBe("include");
    expect(init.headers).not.toHaveProperty("Authorization");
    expect(init.body).toContain('"organizationCode":"demo"');
  });

  it("refreshes once after an expired access cookie", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ expiresIn: 900, role: "Admin", displayName: "Demo Admin" }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify(demoDashboard), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(new ApiClient().dashboard()).resolves.toEqual(demoDashboard);
    expect(fetchMock.mock.calls.map(call => String(call[0]))).toEqual([
      expect.stringContaining("/api/v1/dashboard"),
      expect.stringContaining("/api/v1/auth/refresh"),
      expect.stringContaining("/api/v1/dashboard")
    ]);
    expect((fetchMock.mock.calls[1][1] as RequestInit).headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });

  it("adds the anti-CSRF header to command requests", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: "command" }), { status: 201, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    await new ApiClient().createCommand("device", "SimulateLock", "Authorized test");

    expect((fetchMock.mock.calls[0][1] as RequestInit).headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });

  it("loads the current role after one transparent refresh", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ expiresIn: 900, role: "Employee", displayName: "Demo Employee" }), { status: 200, headers: { "Content-Type": "application/json" } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ role: "Employee", displayName: "Demo Employee" }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(new ApiClient().session()).resolves.toEqual({ role: "Employee", displayName: "Demo Employee" });
    expect(fetchMock.mock.calls.map(call => String(call[0]))).toEqual([
      expect.stringContaining("/api/v1/auth/session"),
      expect.stringContaining("/api/v1/auth/refresh"),
      expect.stringContaining("/api/v1/auth/session")
    ]);
  });

  it("logs out with credentials and the anti-CSRF header", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await new ApiClient().logout();

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/auth/logout");
    expect(init.credentials).toBe("include");
    expect(init.headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });
});
