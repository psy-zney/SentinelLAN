import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClient, ApiError } from "../src/lib/api-client";

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });

function transport() {
  const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(Response.json([])));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}
function body(fetchMock: ReturnType<typeof transport>, index: number) {
  return JSON.parse(fetchMock.mock.calls[index][1].body);
}

describe("protected API transport boundaries", () => {
  it("shares one refresh across concurrent requests from separate client instances", async () => {
    let refreshCount = 0;
    let refreshed = false;
    const fetchMock = vi.fn().mockImplementation(async (url: string) => {
      if (url.endsWith("/auth/refresh")) {
        refreshCount++;
        await Promise.resolve();
        refreshed = true;
        return Response.json({ role: "Admin" });
      }
      return refreshed ? Response.json([]) : new Response(null, { status: 401 });
    });
    vi.stubGlobal("fetch", fetchMock);
    await Promise.all([new ApiClient().commands(), new ApiClient().policies()]);
    expect(refreshCount).toBe(1);
    expect(fetchMock).toHaveBeenCalledTimes(5);
  });

  it("replays an incident submission with the identical idempotency key and body after session refresh", async () => {
    const fetchMock = transport();
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(Response.json({ role: "Admin" }))
      .mockResolvedValueOnce(Response.json({ id: "incident" }));
    const data = { deviceId: "device", title: "Display failure", severity: "Medium", idempotencyKey: "stable-retry-id" };
    await new ApiClient().createIncident(data);
    expect(body(fetchMock, 0)).toEqual(data);
    expect(fetchMock.mock.calls[2][1].body).toBe(fetchMock.mock.calls[0][1].body);
    expect(fetchMock.mock.calls[2][1].headers["Idempotency-Key"]).toBe(data.idempotencyKey);
    expect(fetchMock.mock.calls[2][1].headers["X-SentinelLAN-CSRF"]).toBe("1");
  });

  it("stops pagination after the final directory page and does not silently return a partial failed directory", async () => {
    const fetchMock = transport();
    const fullPage = Array.from({ length: 200 }, (_, id) => ({ id: `d${id}` }));
    fetchMock.mockResolvedValueOnce(Response.json(fullPage)).mockResolvedValueOnce(Response.json([{ id: "last" }]));
    expect(await new ApiClient().devices()).toHaveLength(201);
    expect(String(fetchMock.mock.calls[1][0])).toContain("offset=200");
    fetchMock.mockResolvedValueOnce(Response.json(fullPage)).mockResolvedValueOnce(Response.json({ title: "Directory unavailable" }, { status: 503 }));
    await expect(new ApiClient().allUsers()).rejects.toThrow("Directory unavailable");
    fetchMock.mockResolvedValueOnce(Response.json(fullPage)).mockResolvedValueOnce(Response.json([]));
    expect(await new ApiClient().allUsers()).toHaveLength(200);
  });

  it("encodes filters and preserves page bounds for incident, work-order, loan and employee histories", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    await api.getIncidents("id&other=foreign", 20, 10);
    await api.getWorkOrders("id&other=foreign", 20, 10);
    await api.getAssetLoans("id&other=foreign", 20, 10);
    for (const [url] of fetchMock.mock.calls) {
      const parsed = new URL(url, 'http://localhost');
      expect(parsed.searchParams.get("deviceId")).toBe("id&other=foreign");
      expect(parsed.searchParams.has("other")).toBe(false);
      expect(parsed.searchParams.get("offset")).toBe("20");
      expect(parsed.searchParams.get("limit")).toBe("10");
    }
    await api.supportRequests(10, 5, true);
    await api.supportMessages("request/other", 10, 5);
    await api.employeeNotifications(10, 5);
    expect(String(fetchMock.mock.calls[3][0])).toContain("active=true&offset=10&limit=5");
    expect(String(fetchMock.mock.calls[4][0])).toContain("request%2Fother/messages?offset=10&limit=5");
    expect(String(fetchMock.mock.calls[5][0])).toContain("notifications?offset=10&limit=5");
    await api.getIncidents(); await api.getWorkOrders(); await api.getAssetLoans();
    expect(String(fetchMock.mock.calls[6][0])).not.toContain("?");
  });

  it("keeps an activation secret in request bodies and preserves explicit rejection of revocation", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    const secret = "test-fixture-only-activation";
    await api.createUser({ email: "user@example.test", displayName: "Employee", role: "Employee", reason: "New colleague", confirmed: true });
    await api.reissueActivationToken("employee", "Lost invitation", true);
    await api.validateActivationToken(secret);
    await api.activateAccount({ token: secret, password: "FixturePasswordOnly123!", confirmPassword: "FixturePasswordOnly123!" });
    await api.revokeActivationToken("employee", "Not confirmed", false);
    await api.setUserStatus("employee", "Locked", "Not confirmed", false);
    for (const [url, init] of fetchMock.mock.calls) {
      expect(String(url)).not.toContain(secret);
      expect(init.credentials).toBe("include");
      expect(init.headers["X-SentinelLAN-CSRF"]).toBe("1");
    }
    expect(body(fetchMock, 2)).toEqual({ token: secret });
    expect(body(fetchMock, 4)).toEqual({ reason: "Not confirmed", confirmed: false });
    expect(body(fetchMock, 5).confirmed).toBe(false);
  });

  it("adds independent nonces and short deadlines to admin changes without overriding a false confirmation", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    const department = { name: "Finance", policyId: null, revision: "original-revision" };
    await api.saveDepartment(department, "Create department");
    await api.saveDepartment(department, "Update department", "department/1");
    await api.assignDepartment("users", "employee/1", null, "previous-revision", "Move employee");
    await api.enrollmentPackages(2, "Linux", "Install authorized machines");
    await api.bulkCommands(["device"], "RefreshPolicy", "Refresh policy");
    const confirmations = fetchMock.mock.calls.map((_, i) => body(fetchMock, i).confirmation);
    expect(new Set(confirmations.map(item => item.nonce)).size).toBe(5);
    for (const confirmation of confirmations) {
      expect(confirmation.confirmed).toBe(true);
      expect(Date.parse(confirmation.expiresAt) - Date.now()).toBeGreaterThan(110_000);
      expect(Date.parse(confirmation.expiresAt) - Date.now()).toBeLessThanOrEqual(120_000);
    }
    expect(String(fetchMock.mock.calls[1][0])).toContain("department%2F1");
    expect(body(fetchMock, 2)).toMatchObject({ departmentId: null, revision: "previous-revision" });
    const rule = { name: "Rule", productCode: null, upgradeCode: null, isActive: true, revision: "v1", reason: "Denied", confirmed: false };
    await api.saveSoftwareRule(rule);
    await api.saveSoftwareRule(rule, "rule/1");
    expect(body(fetchMock, 5).confirmed).toBe(false);
    expect(body(fetchMock, 5).nonce).not.toBe(body(fetchMock, 6).nonce);
    expect(fetchMock.mock.calls[6][1].method).toBe("PUT");
  });

  it("updates catalog approval metadata and announcement receipts through authenticated CSRF-protected operations", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    const app = { name: "Reader", description: "PDF reader", version: "1", packageUrl: "https://packages.example.test/reader.msi",
      sha256: "a".repeat(64), publisherThumbprint: "b".repeat(40), isActive: true, requiresApproval: true, reason: "Publish tested version", confirmed: true };
    await api.saveCatalogApp(app);
    await api.saveCatalogApp(app, "app/1");
    expect(fetchMock.mock.calls[1][1].method).toBe("PATCH");
    expect(body(fetchMock, 0)).toMatchObject(app);
    expect(body(fetchMock, 0).nonce).not.toBe(body(fetchMock, 1).nonce);
    await api.createAnnouncement({ title: "Maintenance", body: "Scheduled maintenance", isOutage: true, requiresAcknowledgement: true,
      startsAt: "2026-12-01T08:00:00Z", endsAt: "2026-12-01T09:00:00Z", reason: "Notify employees", confirmed: true });
    await api.acknowledgeAnnouncement("announcement/1");
    await api.markAnnouncementAffected("announcement/1");
    await api.readEmployeeNotification("notification/1");
    for (const [, init] of fetchMock.mock.calls) {
      expect(init.credentials).toBe("include");
      expect(init.headers["X-SentinelLAN-CSRF"]).toBe("1");
      expect(init.cache).toBe("no-store");
    }
    expect(String(fetchMock.mock.calls[3][0])).toContain("announcement%2F1/acknowledge");
  });

  it("preserves repair and loan measurements, assignment removal and explicit device-revocation confirmation", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    await api.updateAssetProfile("device", { manufacturer: "Vendor", purchaseCost: 0, reason: "Correct inventory", confirmed: false });
    await api.updateIncidentStatus("incident", { status: "Resolved", assignedTechnicianId: null, resolutionNotes: "Cable replaced" });
    await api.createWorkOrder({ deviceId: "device", incidentId: "incident", title: "Replace cable", priority: "High" });
    await api.completeWorkOrder("work", { laborHours: 0.5, partsCost: 0, laborCost: 50, notes: "Verified with employee" });
    await api.createAssetLoan({ deviceId: "device", borrowerUserId: "employee", expectedReturnDate: "2026-12-01T00:00:00Z", conditionBefore: "Good" });
    await api.returnAssetLoan("loan", { conditionAfter: "Good", notes: "Received" });
    await api.assignDevice("device", null, "Return to pool", false);
    await api.revokeDevice("device", "Not confirmed", false);
    expect(body(fetchMock, 0)).toMatchObject({ purchaseCost: 0, confirmed: false });
    expect(body(fetchMock, 1)).toMatchObject({ assignedTechnicianId: null });
    expect(body(fetchMock, 3)).toMatchObject({ laborHours: 0.5, partsCost: 0 });
    expect(body(fetchMock, 6)).toMatchObject({ assignedUserId: null, confirmed: false });
    expect(body(fetchMock, 7).confirmed).toBe(false);
  });

  it("passes AbortSignal through for host reads and never sends the CSRF mutation header on read-only queries", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    const controller = new AbortController();
    await api.hostStatus(controller.signal);
    await api.device("device"); await api.deviceTelemetry("device"); await api.getDeviceTimeline("device");
    await api.getQrLabel("device"); await api.softwareStatus(); await api.softwareStatus("device/1");
    await api.softwareRules(); await api.departments(); await api.helpArticles(); await api.supportTeam();
    await api.supportRequest("request/1"); await api.supportAttachments("request/1");
    await api.catalogApps(); await api.announcements(); await api.organization(); await api.myDeviceIncidents();
    expect(fetchMock.mock.calls[0][1].signal).toBe(controller.signal);
    for (const [, init] of fetchMock.mock.calls) {
      expect(init.headers).not.toHaveProperty("X-SentinelLAN-CSRF");
      expect(init.credentials).toBe("include");
      expect(init.cache).toBe("no-store");
    }
  });

  it.each([
    ["title", "Request denied"], ["message", "Action denied"], ["detail", "Wrong organization"]
  ])("preserves a safe server %s instead of pretending an operation succeeded", async (key, value) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ [key]: value }, { status: 403 })));
    await expect(new ApiClient().commands()).rejects.toThrow(value);
  });

  it("rejects non-JSON failure responses and failed refresh without retry loops", async () => {
    const fetchMock = transport();
    fetchMock.mockResolvedValueOnce(new Response("gateway error", { status: 503 }));
    await expect(new ApiClient().policies()).rejects.toThrow("SentinelLAN API 503");
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 })).mockRejectedValueOnce(new Error("refresh unavailable"));
    await expect(new ApiClient().commands()).rejects.toBeInstanceOf(ApiError);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("refreshes a protected attachment once, then rejects incorrect types, oversize blobs and denied downloads", async () => {
    const fetchMock = transport();
    const api = new ApiClient();
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 })).mockResolvedValueOnce(Response.json({ role: "Employee" }))
      .mockResolvedValueOnce(new Response(new Uint8Array([1]), { headers: { "Content-Type": "image/jpeg; charset=binary" } }));
    expect((await api.downloadSupportAttachment("request/1", "image/1")).size).toBe(1);
    expect(String(fetchMock.mock.calls[2][0])).toContain("request%2F1/attachments/image%2F1");
    fetchMock.mockResolvedValueOnce(new Response("<script>fixture</script>", { headers: { "Content-Type": "text/html" } }));
    await expect(api.downloadSupportAttachment("r", "a")).rejects.toThrow("Định dạng ảnh");
    fetchMock.mockResolvedValueOnce(new Response(new Uint8Array(2 * 1024 * 1024 + 1), { headers: { "Content-Type": "image/png" } }));
    await expect(api.downloadSupportAttachment("r", "a")).rejects.toThrow("Ảnh vượt quá");
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 403 }));
    await expect(api.downloadSupportAttachment("r", "a")).rejects.toMatchObject({ status: 403 });
  });
});
