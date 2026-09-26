import { describe, expect, it } from "vitest";
import { executionLabel, validateSupportImage } from "../src/lib/self-service";

describe("employee outcomes", () => {
  it("never describes approval, delivery or a missing command as successful isolation", () => {
    for (const commandStatus of [null, "Pending", "Delivered", "Expired", "Failed"]) {
      const label = executionLabel({ kind: "Panic", status: "Approved", commandId: commandStatus ? "id" : null, commandStatus });
      expect(label).not.toContain("Agent đã xác nhận lệnh cô lập");
    }
    expect(executionLabel({ kind: "Panic", status: "Approved", commandId: "id", commandStatus: "Succeeded" })).toContain("môi trường lab");
  });
  it("requires a separate OTP step for approved maintenance", () => {
    expect(executionLabel({ kind: "UninstallAgent", status: "Approved", commandId: null, commandStatus: null })).toContain("nhập mã");
  });
  it("rejects HTML/SVG and oversize evidence before employee upload", () => {
    expect(validateSupportImage({ type: "image/svg+xml", size: 50 })).toBeTruthy();
    expect(validateSupportImage({ type: "image/jpeg", size: 2097153 })).toBeTruthy();
    expect(validateSupportImage({ type: "image/png", size: 200 })).toBeNull();
  });
});
