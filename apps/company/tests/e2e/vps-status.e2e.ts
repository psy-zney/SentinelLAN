import { expect, test } from "@playwright/test";

test("Admin can expand individual VPS resources and port bindings, recognize old data, and loses details when access is denied", async ({ page }) => {
  const snapshot = {
    name: "vps-test", host: "192.0.2.1", capturedAtUtc: new Date().toISOString(), osInfo: "Linux", uptimeSeconds: 86400,
    cpuPercent: 2, ramPercent: 25, diskPercent: 40, memoryUsedBytes: 1024 ** 3, memoryTotalBytes: 4 * 1024 ** 3,
    diskUsedBytes: 16 * 1024 ** 3, diskTotalBytes: 40 * 1024 ** 3, warnings: [],
    listeningPorts: [
      { address: "127.0.0.1", port: 9003, protocol: "tcp", process: "docker-proxy" },
      { address: "0.0.0.0", port: 443, protocol: "tcp", process: "nginx, nginx-worker" },
      { address: "::1", port: 9004, protocol: "tcp", process: "sshd" },
      { address: "::", port: 5353, protocol: "udp", process: null }
    ],
    runtime: { systemState: "running", dockerAvailable: true, dockerError: null,
      services: [{ name: "nginx", activeState: "active", startupState: "enabled" }, { name: "docker", activeState: "active", startupState: "enabled" }, { name: "ufw", activeState: "inactive", startupState: "disabled" }],
      containers: [{ id: "api", name: "sentinellan-prod-api-1", image: "api:test", state: "running", status: "Up", health: null,
        cpuPercent: 1, memoryUsage: "100MiB / 4GiB", memoryPercent: 2.5,
        ports: [{ containerPort: 8080, protocol: "tcp", hostIp: null, hostPort: null }],
        listeningPorts: [{ address: "0.0.0.0", port: 8443, protocol: "tcp", process: null }] },
        { id: "nginx", name: "sentinellan-prod-nginx-1", image: "nginx:test", state: "running", status: "Up", health: null,
          cpuPercent: 0.5, memoryUsage: "20MiB / 4GiB", memoryPercent: 0.5,
          ports: [{ containerPort: 8443, protocol: "tcp", hostIp: "127.0.0.1", hostPort: 9003 }],
          listeningPorts: [{ address: "0.0.0.0", port: 8443, protocol: "tcp", process: null }] }],
      storageBreakdown: [{ name: "SentinelLAN source", path: "/opt/sentinellan/current", sizeBytes: 1024 ** 2, category: "repo", reclaimable: null }] }
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
  const cpu = page.getByRole("button", { name: "CPU", exact: true });
  const ram = page.getByRole("button", { name: "RAM", exact: true });
  const disk = page.getByRole("button", { name: "Ổ đĩa", exact: true });
  const docker = page.getByRole("button", { name: "Docker", exact: true });
  for (const resource of [cpu, ram, disk, docker]) await expect(resource).toHaveAttribute("aria-expanded", "false");
  await cpu.focus();
  await cpu.press("Enter");
  await expect(page.locator("#vps-cpu-details")).toBeVisible();
  await ram.click();
  await expect(page.locator("#vps-cpu-details")).toBeHidden();
  await expect(page.locator("#vps-ram-details")).toBeVisible();
  await ram.click();
  await expect(page.locator("#vps-ram-details")).toBeHidden();
  await disk.click();
  await expect(page.getByText("/opt/sentinellan/current", { exact: true })).toBeHidden();
  await page.locator("summary").filter({ hasText: "SentinelLAN source" }).click();
  await expect(page.getByText("/opt/sentinellan/current", { exact: true })).toBeVisible();
  await page.locator("#vps-disk-details").getByRole("button", { name: "Thu gọn", exact: true }).click();
  await expect(disk).toHaveAttribute("aria-expanded", "false");
  await docker.click();
  await page.locator("#vps-docker-details").getByRole("button", { name: /sentinellan-prod-api-1/ }).click();
  await expect(page.locator("#vps-docker-details .vps-container-card").filter({ hasText: "sentinellan-prod-api-1" }).getByText("0.0.0.0:8443/tcp", { exact: true })).toBeVisible();
  await expect(page.getByText("Cổng EXPOSE — không có ánh xạ ra VPS", { exact: true })).toBeVisible();
  await expect(page.locator("#vps-docker-details").getByText("Cổng công bố", { exact: true })).toBeHidden();
  await page.locator("#vps-docker-details").getByRole("button", { name: /sentinellan-prod-nginx-1/ }).click();
  await expect(page.locator("#vps-docker-details").getByText("127.0.0.1:9003 → 8443/tcp", { exact: true })).toBeVisible();
  const services = page.locator(".vps-disclosure").filter({ has: page.getByRole("button", { name: /Dịch vụ trên VPS/ }) });
  await services.getByRole("button", { name: /Dịch vụ trên VPS/ }).click();
  const diagram = services.locator(".vps-service-diagram");
  const nginxNode = diagram.getByRole("button", { name: "Dịch vụ nginx", exact: true });
  const nodeDetails = diagram.locator(".vps-diagram-detail");
  await nginxNode.focus();
  await nginxNode.press("Enter");
  await expect(nginxNode).toHaveAttribute("aria-expanded", "true");
  await expect(nodeDetails.getByText("0.0.0.0:443/tcp", { exact: true })).toBeVisible();
  await expect(nodeDetails.getByText("Mọi địa chỉ mạng", { exact: true })).toBeVisible();
  await nginxNode.click();
  await expect(nginxNode).toHaveAttribute("aria-expanded", "false");
  await expect(nodeDetails.getByText("0.0.0.0:443/tcp", { exact: true })).toHaveCount(0);
  await diagram.getByRole("button", { name: "Dịch vụ docker", exact: true }).click();
  await expect(nodeDetails.getByText("127.0.0.1:9003/tcp", { exact: true })).toBeVisible();
  await expect(nodeDetails.getByText("Chỉ localhost", { exact: true })).toBeVisible();
  await nodeDetails.getByRole("button", { name: "sentinellan-prod-nginx-1", exact: true }).click();
  const containerNode = diagram.getByRole("button", { name: "Container sentinellan-prod-nginx-1", exact: true });
  await expect(containerNode).toBeVisible();
  await expect(containerNode).toHaveAttribute("aria-expanded", "true");
  await expect(nodeDetails.getByText("nginx:test", { exact: true })).toBeVisible();
  await expect(nodeDetails.locator(".vps-diagram-binding")).toHaveText("127.0.0.1:9003→8443/tcp");
  await diagram.screenshot({ path: test.info().outputPath("vps-service-diagram.png") });
  await nodeDetails.getByRole("button", { name: "Thu gọn", exact: true }).click();
  await expect(containerNode).toBeFocused();
  await expect(containerNode).toHaveAttribute("aria-expanded", "false");
  await diagram.getByRole("button", { name: "Dịch vụ ufw", exact: true }).click();
  await expect(nodeDetails.getByText("inactive", { exact: true })).toBeVisible();
  await expect(nodeDetails.getByText("Chưa ghi nhận cổng tương ứng với tiến trình của dịch vụ này.", { exact: true })).toBeVisible();
  await nodeDetails.getByRole("button", { name: "Thu gọn", exact: true }).click();
  await services.getByRole("button", { name: /Dịch vụ trên VPS/ }).click();
  await expect(diagram).toBeHidden();
  await page.getByRole("button", { name: /Cổng đang nghe trên VPS/ }).click();
  const localhost = page.getByRole("button", { name: "Chi tiết cổng 127.0.0.1:9003/tcp", exact: true });
  await expect(localhost.locator("xpath=ancestor::tr")).toContainText("Chỉ localhost");
  await expect(localhost.locator("xpath=ancestor::tr")).toContainText("sentinellan-prod-nginx-1");
  await localhost.click();
  await expect(page.locator(".vps-port-group-body").getByText("127.0.0.1:9003 → 8443/tcp", { exact: true })).toBeVisible();
  await localhost.click();
  await expect(localhost).toHaveAttribute("aria-expanded", "false");
  const publicBinding = page.getByRole("button", { name: "Chi tiết cổng 0.0.0.0:443/tcp", exact: true });
  await expect(publicBinding.locator("xpath=ancestor::tr")).toContainText("Mọi địa chỉ mạng");
  await expect(publicBinding.locator("xpath=ancestor::tr")).toContainText("nginx");
  await expect(page.getByRole("button", { name: "Chi tiết cổng [::1]:9004/tcp", exact: true }).locator("xpath=ancestor::tr")).toContainText("Chỉ localhost");
  await page.getByRole("textbox", { name: "Tìm cổng", exact: true }).fill("sentinellan-prod-nginx-1");
  await expect(localhost).toBeVisible();
  await expect(publicBinding).toHaveCount(0);
  await page.getByRole("textbox", { name: "Tìm cổng", exact: true }).fill("");
  await page.getByRole("button", { name: "UDP (1)", exact: true }).click();
  await expect(page.getByRole("button", { name: "Chi tiết cổng [::]:5353/udp", exact: true }).locator("xpath=ancestor::tr")).toContainText("Chưa xác định tiến trình");
  await page.getByRole("button", { name: /Cổng đang nghe trên VPS/ }).click();
  await expect(page.getByRole("textbox", { name: "Tìm cổng", exact: true })).toBeHidden();
  stale = true;
  snapshot.capturedAtUtc = new Date(Date.now() - 600_000).toISOString();
  await page.getByRole("button", { name: "Làm mới", exact: true }).click();
  await expect(page.getByText("Dữ liệu cũ — chưa xác nhận trạng thái hiện tại", { exact: true })).toBeVisible();
  status = 403;
  await page.getByRole("button", { name: "Làm mới", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Chỉ Admin của đơn vị vận hành" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "vps-test · 192.0.2.1" })).toHaveCount(0);
  await expect(docker).toHaveCount(0);
  await expect(page.locator("#vps-docker-details")).toHaveCount(0);
  await expect(diagram).toHaveCount(0);
});
