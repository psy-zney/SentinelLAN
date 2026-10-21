import { afterEach, describe, expect, it, vi } from "vitest";
import { vpsApi } from "../src/lib/api";

afterEach(() => vi.unstubAllGlobals());
describe("Super Admin VPS transport", () => {
  it("uses platform cookies, CSRF, fresh nonces and short expiries for mutations", async () => {
    const fetch = vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify({ success: true, message: "Scheduled" }), { status: 200 })));
    vi.stubGlobal("fetch", fetch);
    await vpsApi.operate("vps", "Reboot", null, null, "Maintenance");
    await vpsApi.operate("vps", "EnableServiceStartup", "docker", null, "Startup configuration");
    const requests = fetch.mock.calls.map(([url, init]) => {
      expect(url).toContain("/api/v1/platform/vps-nodes/vps/operations");
      expect(init.credentials).toBe("include"); expect(init.headers["X-SentinelLAN-CSRF"]).toBe("1");
      const body = JSON.parse(init.body);
      expect(body.confirmed).toBe(true); expect(body.nonce).toMatch(/^[0-9a-f-]{36}$/);
      expect(new Date(body.expiresAt).getTime() - Date.now()).toBeLessThanOrEqual(5 * 60_000);
      return body;
    });
    expect(requests[0].nonce).not.toBe(requests[1].nonce);
    expect(requests[0]).toMatchObject({ action: "Reboot", target: null, value: null });
  });
  it("passes cancellation to monitoring and displays the VPS rejection message", async () => {
    const controller = new AbortController();
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ success: false, message: "Yêu cầu đã được sử dụng." }), { status: 400 }));
    vi.stubGlobal("fetch", fetch);
    await expect(vpsApi.refresh("vps", controller.signal)).rejects.toThrow("Yêu cầu đã được sử dụng.");
    expect(fetch.mock.calls[0][1].signal).toBe(controller.signal);
    expect(fetch.mock.calls[0][0]).toContain("/api/v1/platform/vps-nodes/vps/refresh-metrics");
  });
});
