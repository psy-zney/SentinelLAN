import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiClient } from "../src/lib/api-client";
import { generateQrMatrix, generateQrSvgPath } from "../src/lib/qr-generator";

beforeEach(() => vi.unstubAllGlobals());

describe("QR Code Generator", () => {
  it("generates a valid QR matrix with correct dimensions", () => {
    const matrix = generateQrMatrix("http://localhost:3000/qr/test-device-id");
    expect(matrix).toBeDefined();
    expect(matrix.length).toBeGreaterThanOrEqual(21);
    expect(matrix[0].length).toBe(matrix.length);

    // Top-left finder pattern check: (0,0) to (6,6) should have black boundary
    expect(matrix[0][0]).toBe(true);
    expect(matrix[0][6]).toBe(true);
    expect(matrix[6][0]).toBe(true);
    expect(matrix[6][6]).toBe(true);
  });

  it("generates SVG path strings", () => {
    const matrix = generateQrMatrix("test");
    const { path, size } = generateQrSvgPath(matrix, 6);
    expect(size).toBe(matrix.length * 6);
    expect(path).toContain("M0,0");
    expect(path).toContain("h6v6h-6z");
  });
});

describe("ITAM & CMMS ApiClient", () => {
  it("fetches device asset detail", async () => {
    const mockDetail = {
      id: "dev-123",
      name: "EMPLOYEE-DEMO-PC",
      healthScore: { score: 95, grade: "Excellent" },
      repairVsReplace: { recommendation: "Keep & Maintain" }
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockDetail), { status: 200, headers: { "Content-Type": "application/json" } })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const result = await client.getDeviceAssetDetail("dev-123");

    expect(result).toEqual(mockDetail);
    expect(fetchMock.mock.calls[0][0]).toContain("/api/v1/devices/dev-123/asset-detail");
  });

  it("creates work orders with anti-CSRF header", async () => {
    const mockWo = { id: "wo-1", workOrderNumber: "WO-2026-0001", title: "Clean fan" };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockWo), { status: 201, headers: { "Content-Type": "application/json" } })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const result = await client.createWorkOrder({
      deviceId: "dev-123",
      title: "Clean fan",
      type: "Preventive"
    });

    expect(result).toEqual(mockWo);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/v1/work-orders");
    expect(init.method).toBe("POST");
    expect(init.headers).toHaveProperty("X-SentinelLAN-CSRF", "1");
  });

  it("resolves a public QR code through the minimal public endpoint", async () => {
    const mockPublic = {
      deviceName: "EMPLOYEE-DEMO-PC",
      assetTag: "AST-001",
      assetStatus: "InUse",
      contactPolicy: "Contact IT support",
      isOnline: true,
      isAssigned: true
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockPublic), { status: 200, headers: { "Content-Type": "application/json" } })
    );
    vi.stubGlobal("fetch", fetchMock);

    const client = new ApiClient();
    const result = await client.resolvePublicQr("signed-code/with unsafe chars");

    expect(result).toEqual(mockPublic);
    expect(fetchMock.mock.calls[0][0]).toContain("/api/v1/qr/signed-code%2Fwith%20unsafe%20chars/public");
  });
});
