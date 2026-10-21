import { expect, test } from "@playwright/test";

test("owner monitors containers, refreshes on demand and confirms standard VPS operations", async ({ page }) => {
  const id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  let refreshes = 0;
  let rejected = true;
  const operations: Record<string, unknown>[] = [];
  const node = {
    id: "lab-vps", name: "VPS thử nghiệm", host: "192.0.2.1", port: 22, username: "operator", status: "Online",
    cpuPercent: 20, ramPercent: 50, diskPercent: 40, dockerContainersCount: 1,
    osInfo: "Linux 6.8 x86_64", uptime: "up 2 days", lastCheckedAt: new Date().toISOString(),
    createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
    runtime: {
      systemState: "degraded", dockerAvailable: true, dockerError: null,
      services: [{ name: "docker", activeState: "active", startupState: "enabled" }],
      containers: [{ id, name: "api", image: "sentinellan:1", state: "running", status: "Up 2 days", health: "unhealthy", restartPolicy: "unless-stopped", cpuPercent: 150, memoryUsage: "100MiB / 400MiB", memoryPercent: 25, storageUsage: "10MB (virtual 200MB)", networkIo: "1MB / 2MB", blockIo: "30MB / 40MB" }]
    }
  };
  await page.route("**/api/v1/platform/**", async route => {
    const path = new URL(route.request().url()).pathname;
    const origin = route.request().headers().origin ?? "http://127.0.0.1:3212";
    const headers = { "Access-Control-Allow-Origin": origin, "Access-Control-Allow-Credentials": "true", "Access-Control-Allow-Headers": "Content-Type,X-SentinelLAN-CSRF", "Access-Control-Allow-Methods": "GET,POST,OPTIONS" };
    if (route.request().method() === "OPTIONS") { await route.fulfill({ status: 204, headers }); return; }
    let body: unknown = [];
    let status = 200;
    if (path.endsWith("/system/status")) body = { databaseConnected: true, databaseLatencyMs: 1, databaseProvider: "PostgreSQL", hostName: "API", osDescription: "Linux", runtimeVersion: ".NET 10", uptimeSeconds: 7200, memoryUsageMb: 50, totalCompanies: 0, activeCompanies: 0, suspendedCompanies: 0, totalUsers: 0, totalDevices: 0, onlineDevices: 0, vpsNodes: [] };
    if (path.endsWith("/vps-nodes")) body = [{ ...node, runtime: null }];
    if (path.endsWith("/refresh-metrics")) { refreshes++; body = node; }
    if (path.endsWith("/operations")) {
      operations.push(route.request().postDataJSON());
      if (rejected) { status = 400; body = { success: false, message: "VPS từ chối lệnh: kiểm tra quyền sudo." }; }
      else body = { success: true, message: "VPS đã nhận lịch khởi động lại sau khoảng 1 phút." };
    }
    await route.fulfill({ status, headers, json: body });
  });
  await page.clock.install();
  await page.goto("/platform");
  await page.getByRole("button", { name: /Giám sát VPS/ }).click();
  const monitor = page.getByRole("region", { name: "Quản lý VPS qua SSH" });
  await expect(monitor.getByText("100MiB / 400MiB", { exact: false })).toBeVisible();
  await expect(monitor.getByText("10MB (virtual 200MB)", { exact: true })).toBeVisible();
  await expect(monitor.getByRole("cell", { name: /unhealthy/ })).toBeVisible();
  await expect(monitor.getByText("Có dịch vụ lỗi", { exact: true })).toBeVisible();
  await expect(monitor.getByRole("button", { name: "Kiểm tra lại ngay" })).toBeEnabled();
  expect(refreshes).toBe(1);
  await monitor.getByRole("button", { name: "Kiểm tra lại ngay" }).click();
  await expect.poll(() => refreshes).toBe(2);
  await expect(monitor.getByRole("button", { name: "Kiểm tra lại ngay" })).toBeEnabled();
  await page.clock.fastForward(180_001);
  await expect.poll(() => refreshes).toBe(3);
  await expect(monitor.getByRole("button", { name: "Kiểm tra lại ngay" })).toBeEnabled();
  await monitor.getByRole("button", { name: "Reboot VPS" }).click();
  const dialog = page.getByRole("dialog", { name: "Khởi động lại VPS" });
  await expect(dialog.getByRole("button", { name: "Xác nhận thực hiện" })).toBeDisabled();
  await expect(dialog.getByText("sudo -n shutdown -r +1", { exact: true })).toBeVisible();
  await dialog.getByLabel("Lý do", { exact: true }).fill("Bảo trì VPS được phép");
  await dialog.getByRole("checkbox").check();
  await dialog.getByRole("button", { name: "Xác nhận thực hiện" }).click();
  await expect(dialog.getByRole("alert")).toContainText("VPS từ chối lệnh");
  rejected = false;
  await dialog.getByRole("button", { name: "Xác nhận thực hiện" }).click();
  await expect(dialog).not.toBeVisible();
  await expect(monitor.getByRole("status")).toContainText("VPS đã nhận lịch");
  expect(operations).toHaveLength(2);
  expect(operations[0]).toMatchObject({ action: "Reboot", target: null, value: null, confirmed: true });
  expect(operations[0].nonce).not.toBe(operations[1].nonce);
  await page.getByRole("button", { name: /Danh sách & Kiểm tra/ }).click();
  const stoppedAt = refreshes;
  await page.clock.fastForward(360_000);
  expect(refreshes).toBe(stoppedAt);
});
