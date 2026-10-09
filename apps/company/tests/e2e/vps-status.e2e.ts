import { expect, test } from "@playwright/test";

test("Admin can inspect VPS ports, recognize old data, and cannot retain it after access is denied", async ({ page }) => {
  const snapshot = {
    name: "vps-test", host: "192.0.2.1", capturedAtUtc: new Date().toISOString(), osInfo: "Linux", uptimeSeconds: 86400,
    cpuPercent: 2, ramPercent: 25, diskPercent: 40, memoryUsedBytes: 1024 ** 3, memoryTotalBytes: 4 * 1024 ** 3,
    diskUsedBytes: 16 * 1024 ** 3, diskTotalBytes: 40 * 1024 ** 3, warnings: [],
    listeningPorts: [{ address: "127.0.0.1", port: 9003, protocol: "tcp", process: "docker-proxy" }],
    runtime: { systemState: "running", dockerAvailable: true, dockerError: null,
      services: [{ name: "nginx", activeState: "active", startupState: "enabled" }],
      containers: [{ id: "api", name: "sentinellan-prod-api-1", image: "api:test", state: "running", status: "Up", health: null,
        cpuPercent: 1, memoryUsage: "100MiB / 4GiB", memoryPercent: 2.5,
        ports: [{ containerPort: 8080, protocol: "tcp", hostIp: null, hostPort: null }],
        listeningPorts: [{ address: "0.0.0.0", port: 8443, protocol: "tcp", process: null }] }] }
  };
  let status = 200;
  let stale = false;
  await page.route("**/api/v1/host/status", route => route.fulfill({ status, contentType: "application/json",
    body: JSON.stringify({ configured: true, available: true, stale, message: "fixture", snapshot }) }));
  await page.goto("/company/login");
  await page.locator('input[name="organizationCode"]').fill("demo");
  await page.locator('input[name="email"]').fill("admin@sentinellan.local");
  await page.locator('input[name="password"]').fill("local-demo-only");
  await page.locator("form.login button[type=submit]").click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await page.getByRole("link", { name: "Tình trạng VPS", exact: true }).click();
  await expect(page).toHaveURL(/\/vps$/);
  await expect(page.getByRole("heading", { name: "vps-test · 192.0.2.1" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "9003", exact: true })).toBeVisible();
  await expect(page.getByText("0.0.0.0:8443/tcp", { exact: true })).toBeVisible();
  await expect(page.getByText("Không công bố", { exact: true })).toBeVisible();
  stale = true;
  snapshot.capturedAtUtc = new Date(Date.now() - 600_000).toISOString();
  await page.getByRole("button", { name: "Làm mới", exact: true }).click();
  await expect(page.getByText("Dữ liệu cũ — chưa xác nhận trạng thái hiện tại", { exact: true })).toBeVisible();
  status = 403;
  await page.getByRole("button", { name: "Làm mới", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Chỉ Admin của đơn vị vận hành" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "vps-test · 192.0.2.1" })).toHaveCount(0);
});
