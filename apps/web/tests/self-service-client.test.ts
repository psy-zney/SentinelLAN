import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClient, ApiError } from "../src/lib/api-client";

afterEach(() => vi.unstubAllGlobals());

describe("employee self-service transport", () => {
  it("submits the explicit confirmation and stable idempotency key with the cookie/CSRF boundary", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ id: "request" }));
    vi.stubGlobal("fetch", fetchMock);
    const client = new ApiClient();
    await client.createSupportRequest({ kind: "Panic", category: "Suspicious", title: "Máy có thông báo đáng ngờ", canWork: false, confirmed: true, idempotencyKey: "stable-request-key" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/v1/self-service/requests");
    expect(init.credentials).toBe("include");
    expect(init.headers["X-SentinelLAN-CSRF"]).toBe("1");
    expect(JSON.parse(init.body)).toMatchObject({ confirmed: true, idempotencyKey: "stable-request-key", kind: "Panic" });
    expect(JSON.parse(init.body)).not.toHaveProperty("deviceId");
  });

  it("returns approval codes only from the authenticated decision response", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ request: { status: "Approved" }, otp: "12345678" }));
    vi.stubGlobal("fetch", fetchMock);
    const response = await new ApiClient().decideSupportRequest("request-id", { approved: true, reason: "Thay máy theo kế hoạch", confirmed: true });
    expect(response.otp).toBe("12345678");
    expect(fetchMock.mock.calls[0][0]).toContain("/requests/request-id/decision");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toMatchObject({ confirmed: true, approved: true });
  });

  it("preserves a server denial instead of pretending maintenance executed", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ detail: "Mã đã hết hạn" }, { status: 409 })));
    await expect(new ApiClient().redeemMaintenanceCode("request-id", "12345678", true)).rejects.toBeInstanceOf(ApiError);
  });

  it("downloads evidence with authentication instead of a public link", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(new Uint8Array([137, 80, 78, 71]), { headers: { "Content-Type": "image/png" } }));
    vi.stubGlobal("fetch", fetchMock);
    const blob = await new ApiClient().downloadSupportAttachment("request-id", "image-id");
    expect(blob.type).toBe("image/png");
    expect(fetchMock.mock.calls[0][0]).toContain("/requests/request-id/attachments/image-id");
    expect(fetchMock.mock.calls[0][1].credentials).toBe("include");
  });
});
