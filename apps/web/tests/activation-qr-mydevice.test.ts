import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiClient } from "../src/lib/api-client";
import { extractSentinelLanQrCode } from "../src/lib/qr-code-parser";

beforeEach(() => vi.unstubAllGlobals());

describe("Account Activation & Invitation Client", () => {
  it("validates activation token without exposing sensitive secrets", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          valid: true,
          email: "employee@sentinellan.local",
          displayName: "Demo Employee",
          organizationName: "SentinelLAN Corp",
          expiresAt: "2026-09-23T00:00:00Z"
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.validateActivationToken("test-token-123");

    expect(res.valid).toBe(true);
    expect(res.email).toBe("employee@sentinellan.local");
    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/auth/activation/validate?token=test-token-123");
  });

  it("submits password activation request with single-use token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          message: "Account activated successfully.",
          email: "employee@sentinellan.local",
          userId: "user-uuid"
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.activateAccount({
      token: "secret-token-xyz",
      password: "SuperSecretPassword123!",
      confirmPassword: "SuperSecretPassword123!"
    });

    expect(res.success).toBe(true);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/auth/activate");
    expect(init.method).toBe("POST");
    expect(init.body).toContain('"token":"secret-token-xyz"');
    expect(init.body).toContain('"password":"SuperSecretPassword123!"');
  });

  it("reissues activation token with reason and confirmation", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          token: "new-token-456",
          activationUrl: "/activate?token=new-token-456",
          expiresAt: "2026-09-23T12:00:00Z",
          userEmail: "emp@corp.local"
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.reissueActivationToken("user-1", "Lost email link", true);

    expect(res.token).toBe("new-token-456");
    expect(res.activationUrl).toContain("new-token-456");
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/users/user-1/activation-token");
    expect(init.method).toBe("POST");
  });

  it("revokes invitation token with anti-CSRF header", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(null, { status: 204 })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    await client.revokeActivationToken("user-1", "Revoked by admin", true);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/users/user-1/activation-token");
    expect(init.method).toBe("DELETE");
    expect(init.headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });
});

describe("QR Asset Lifecycle & Resolution Client", () => {
  it("accepts only opaque codes and same-origin QR URLs", () => {
    const code = "demo-qr-asset-employee-pc-2026";
    expect(extractSentinelLanQrCode(code, "https://sentinellan.example")).toBe(code);
    expect(extractSentinelLanQrCode(`https://sentinellan.example/qr/${code}`, "https://sentinellan.example")).toBe(code);
    expect(extractSentinelLanQrCode(`https://evil.example/qr/${code}`, "https://sentinellan.example")).toBeNull();
    expect(extractSentinelLanQrCode(`javascript:/qr/${code}`, "https://sentinellan.example")).toBeNull();
    expect(extractSentinelLanQrCode("short", "https://sentinellan.example")).toBeNull();
  });

  it("generates and rotates device QR label with reason", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          code: "new-qr-code-789",
          id: "label-1",
          codePrefix: "qr-new-78",
          qrUrl: "http://localhost:3000/qr/new-qr-code-789",
          createdAt: "2026-09-22T00:00:00Z",
          expiresAt: null
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.generateQrLabel("dev-1", "Replacing peeled chassis sticker");

    expect(res.code).toBe("new-qr-code-789");
    expect(res.codePrefix).toBe("qr-new-78");
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/devices/dev-1/qr-label");
    expect(init.method).toBe("POST");
    expect(init.headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
    expect(JSON.parse(init.body as string)).toEqual({ reason: "Replacing peeled chassis sticker", confirmed: true });
  });

  it("revokes device QR label", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ message: "QR label revoked" }), {
        status: 200,
        headers: { "Content-Type": "application/json" }
      })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    await client.revokeQrLabel("dev-1", "Asset retired");

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/devices/dev-1/qr-label");
    expect(init.method).toBe("DELETE");
    expect(init.headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
    expect(JSON.parse(init.body as string)).toEqual({ reason: "Asset retired", confirmed: true });
  });

  it("fetches minimal public QR resolution protecting privacy", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          deviceName: "FINANCE-LAPTOP-01",
          assetTag: "TAG-DELL-9988",
          assetStatus: "Active",
          contactPolicy: "Contact IT Administrator",
          isOnline: true,
          isAssigned: true
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.resolvePublicQr("demo-qr-asset-employee-pc-2026");

    expect(res.deviceName).toBe("FINANCE-LAPTOP-01");
    expect(res.assetTag).toBe("TAG-DELL-9988");
    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/qr/demo-qr-asset-employee-pc-2026/public");
  });

  it("resolves authenticated QR routing according to actor permissions", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          role: "Technician",
          nextRoute: "/devices/dev-1",
          deviceId: "dev-1",
          deviceName: "FINANCE-LAPTOP-01",
          authorized: true
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.resolveQr("demo-qr-asset-employee-pc-2026");

    expect(res.role).toBe("Technician");
    expect(res.nextRoute).toBe("/devices/dev-1");
  });
});

describe("My Device Personal Management Client", () => {
  it("fetches personal device with transparency manifest", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          device: {
            id: "dev-1",
            name: "MY-WORK-PC",
            osVersion: "Windows 11 24H2",
            agentVersion: "0.1.0",
            isOnline: true,
            lastSeenAt: "2026-09-22T00:00:00Z",
            assignedUserId: "user-1",
            isRevoked: false
          },
          appliedPolicy: "Standard Workstation",
          latestTelemetry: null,
          incidents: [],
          recentActions: [],
          serialNumber: "SN-9988",
          manufacturer: "Dell Inc.",
          model: "Latitude 5420",
          assetType: "Laptop",
          location: "Hanoi HQ - Fl 4",
          assignedAt: "2026-01-15T00:00:00Z",
          privacyManifest: {
            collectedTechnicalData: ["CPU %", "RAM %", "Disk %"],
            strictlyProhibitedData: ["No keystroke logging", "No webcam access"],
            agentPermissions: ["Read hardware sensors"],
            dataRetentionDays: 30
          }
        }),
        { status: 200, headers: { "Content-Type": "application/json" } }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const res = await client.myDevice();

    expect(res.name).toBe("MY-WORK-PC");
    expect(res.transparencyManifest.dataRetentionDays).toBe(30);
    expect(res.transparencyManifest.privacyBoundaries).toContain("No keystroke logging");
  });

  it("fetches personal device telemetry and reports incident", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify([
            {
              id: "telemetry-1",
              deviceId: "dev-1",
              cpuPercent: 12.5,
              ramPercent: 55.0,
              diskPercent: 42.0,
              createdAt: "2026-09-22T00:00:00Z"
            }
          ]),
          { status: 200, headers: { "Content-Type": "application/json" } }
        )
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            id: "inc-101",
            title: "Screen flickering",
            severity: "Medium",
            status: "Open",
            createdAt: "2026-09-22T00:00:00Z"
          }),
          { status: 201, headers: { "Content-Type": "application/json" } }
        )
      );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const telemetry = await client.myDeviceTelemetry(5);
    expect(telemetry[0].cpuPercent).toBe(12.5);

    const incidentRes = await client.reportMyDeviceIncident({
      title: "Screen flickering",
      description: "HDMI port loose",
      severity: "Medium"
    });
    expect(incidentRes.id).toBe("inc-101");
    expect((fetchMock.mock.calls[1][1] as RequestInit).headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });
});
