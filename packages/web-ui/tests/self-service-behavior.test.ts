import { describe, expect, it } from "vitest";
import { executionLabel, itStatusOptions, validateSupportImage } from "../src/lib/self-service";

describe("employee outcomes", () => {
  it("keeps pending installation approval separate from completed work", () => {
    expect(itStatusOptions({ kind: "InstallApp", status: "Approved", commandId: "id", commandStatus: "Pending" })).not.toContain("Resolved");
    expect(itStatusOptions({ kind: "InstallApp", status: "Approved", commandId: "id", commandStatus: "Succeeded" })).toContain("Resolved");
    expect(itStatusOptions({ kind: "PauseAgent", status: "Approved", commandId: null, commandStatus: null })).not.toContain("Open");
  });
  it("requires accepting a new incident before resolving it", () => {
    expect(itStatusOptions({ kind: "Incident", status: "Open", commandId: null, commandStatus: null })).toEqual(["Open", "InProgress"]);
  });
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
