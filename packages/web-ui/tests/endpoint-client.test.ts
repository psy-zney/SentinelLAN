import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClient } from "../src/lib/api-client";

afterEach(() => vi.unstubAllGlobals());

describe("endpoint observations and organization settings", () => {
  it("loads employee observations without allowing a caller to choose another device", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ inventory: null, security: null, securityStale: true }));
    vi.stubGlobal("fetch", fetchMock);
    const data = await new ApiClient().endpointObservations();
    expect(fetchMock.mock.calls[0][0]).toContain("/api/v1/my-device/observations");
    expect(fetchMock.mock.calls[0][1].credentials).toBe("include");
    expect(data.securityStale).toBe(true);
    expect(data.security).toBeNull();
  });

  it("submits confirmation, revision, a fresh nonce and bounded expiry for each settings change", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(Response.json({ revision: "updated" })));
    vi.stubGlobal("fetch", fetchMock);
    const settings = { cpuThresholdPercent: 20, ramThresholdPercent: 90, diskThresholdPercent: 95, offlineMinutes: 5,
      sustainedMinutes: 0, maintenancePauseMinutes: 15, preventiveIntervalMonths: 6, warrantyNoticeDays: 30,
      securityAlertsEnabled: true, revision: "previous-revision" };
    await new ApiClient().saveMonitoringSettings(settings, "Thử cảnh báo", true);
    await new ApiClient().saveMonitoringSettings(settings, "Thử cảnh báo", true);
    const input = JSON.parse(fetchMock.mock.calls[0][1].body);
    const next = JSON.parse(fetchMock.mock.calls[1][1].body);
    expect(input.settings.revision).toBe("previous-revision");
    expect(input.settings.sustainedMinutes).toBe(0);
    expect(input.confirmed).toBe(true);
    expect(input.nonce).not.toBe(next.nonce);
    expect(Date.parse(input.expiresAt) - Date.now()).toBeLessThanOrEqual(120_000);
    expect(fetchMock.mock.calls[0][1].headers["X-SentinelLAN-CSRF"]).toBe("1");
  });
});
