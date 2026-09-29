import { describe, expect, it, vi, afterEach } from "vitest";
import { extractSentinelLanQrCode } from "../src/lib/qr-code-parser";
afterEach(()=>{vi.unstubAllEnvs();vi.resetModules();});
describe("separate web applications",()=>{
  it("company permits only tenant operators",async()=>{
    vi.stubEnv("NEXT_PUBLIC_PORTAL","company");
    const {portalAllows}=await import("../src/lib/portal");
    expect(portalAllows("Admin")).toBe(true);expect(portalAllows("Technician")).toBe(true);expect(portalAllows("Employee")).toBe(false);
  });
  it("employee permits only employees",async()=>{
    vi.stubEnv("NEXT_PUBLIC_PORTAL","employee");
    const {portalAllows}=await import("../src/lib/portal");
    expect(portalAllows("Employee")).toBe(true);expect(portalAllows("Admin")).toBe(false);expect(portalAllows("Technician")).toBe(false);
  });
  it("accepts prefixed genuine QR links while rejecting foreign origins",()=>{
    const code="opaque-valid-code-0123456789";
    expect(extractSentinelLanQrCode(`https://sentinel.test/employee/qr/${code}`,"https://sentinel.test")).toBe(code);
    expect(extractSentinelLanQrCode(`https://evil.test/employee/qr/${code}`,"https://sentinel.test")).toBeNull();
  });
});
