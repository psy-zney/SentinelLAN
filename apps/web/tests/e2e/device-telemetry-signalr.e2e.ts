import { expect, test } from "@playwright/test";

const apiUrl = "http://127.0.0.1:8080";

test("Enrollment, live inventory, telemetry, simulated command, audit and heartbeat timeout", async ({ page, playwright }) => {
  test.setTimeout(210_000);
  let hubReady = false;
  let statusEvents = 0;
  page.on("websocket", socket => {
    if (!socket.url().includes("/hubs/updates")) return;
    socket.on("framereceived", event => {
      const payload = event.payload.toString();
      if (payload.includes("{}")) hubReady = true;
      if (payload.includes("device-status")) ++statusEvents;
    });
  });
  await page.goto("/login");
  await page.getByLabel("Organization code").fill("demo");
  await page.getByLabel("Email").fill("admin@sentinellan.local");
  await page.getByLabel("Password").fill("local-demo-only");
  await page.getByRole("button", { name: "Sign in securely" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await page.goto("/devices");
  await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible();
  await expect.poll(() => hubReady).toBe(true);

  const agent = await playwright.request.newContext({ baseURL: apiUrl });
  try {
    const enrollment = await agent.post("/api/v1/agent/enroll", { data: {
      token: "local-e2e-enrollment-only", deviceName: "E2E-REALTIME-PC", osVersion: "Windows 11", agentVersion: "0.1.0"
    } });
    expect(enrollment.status()).toBe(200);
    const identity = await enrollment.json() as { deviceId: string; deviceSecret: string };
    const headers = { "X-SentinelLAN-Device-Id": identity.deviceId, "X-SentinelLAN-Device-Secret": identity.deviceSecret };
    const row = page.locator("tr", { hasText: "E2E-REALTIME-PC" });
    // No reload/navigation: enrollment must appear through a real hub event.
    await expect(row.getByText("Offline", { exact: true })).toBeVisible();
    const heartbeat = async (key: string, cpu: number) => agent.post("/api/v1/agent/heartbeat", { headers, data: {
      idempotencyKey: key, cpuPercent: cpu, ramPercent: 62.4, diskPercent: 49.1,
      osVersion: "Windows 11 24H2 Enterprise", agentVersion: "0.1.0"
    } });
    expect((await heartbeat("e2e-first", 37.5)).status()).toBe(202);
    await expect(row.getByText("Online", { exact: true })).toBeVisible();
    await expect(row.getByText("Windows 11 24H2 Enterprise")).toBeVisible();
    expect(statusEvents).toBeGreaterThan(0);

    await row.getByRole("link", { name: "E2E-REALTIME-PC" }).click();
    await expect(page.getByText("37.5%", { exact: true })).toBeVisible();
    expect((await heartbeat("e2e-second", 40)).status()).toBe(202);
    // The existing detail page must refresh metrics without navigation.
    await expect(page.getByText("40.0%", { exact: true })).toBeVisible();
    await expect(page.getByText("62.4%", { exact: true })).toBeVisible();
    await expect(page.getByText("49.1%", { exact: true })).toBeVisible();
    const timestamp = await page.locator(".device-facts div").filter({ hasText: "Last heartbeat" }).locator("dd").innerText();

    const queue = page.getByRole("button", { name: "Queue simulated lock" });
    await expect(queue).toBeDisabled();
    await page.getByLabel("Command reason").fill("Authorized E2E simulation");
    await page.getByRole("checkbox", { name: /I confirm this simulated action/ }).check();
    await queue.click();
    await expect(page.getByText("Simulated lock command queued and audited.")).toBeVisible();
    const poll = await agent.post("/api/v1/agent/commands/poll", { headers });
    expect(poll.status()).toBe(200);
    const command = await poll.json() as { id: string; type: string; signature: string };
    expect(command.type).toBe("SimulateLock");
    expect(command.signature).toMatch(/^[A-F0-9]{64}$/);
    const resultPath = `/api/v1/agent/commands/${command.id}/result`;
    expect((await agent.post(resultPath, { headers, data: { succeeded: true, message: "E2E simulated receipt; OS unchanged" } })).status()).toBe(202);
    expect((await agent.post(resultPath, { headers, data: { succeeded: true, message: "Retry" } })).status()).toBe(200);
    expect((await agent.post("/api/v1/agent/commands/poll", { headers })).status()).toBe(204);
    const audits = await page.evaluate(async url => {
      const response = await fetch(`${url}/api/v1/audit-logs`, { credentials: "include" });
      if (!response.ok) throw new Error(`Audit API ${response.status}`);
      return response.json() as Promise<{ action: string; outcome: string; deviceId: string }[]>;
    }, apiUrl);
    expect(audits).toEqual(expect.arrayContaining([
      expect.objectContaining({ deviceId: identity.deviceId, action: "CommandCreated:SimulateLock", outcome: "Pending" }),
      expect.objectContaining({ deviceId: identity.deviceId, action: `CommandCompleted:${command.id.replaceAll("-", "")}:SimulateLock`, outcome: "Succeeded" })
    ]));
    // Stop heartbeats and require the server's two-minute timeout to reach the browser.
    await expect(page.getByText("Offline", { exact: true })).toBeVisible({ timeout: 135_000 });
    await expect(page.locator(".device-facts div").filter({ hasText: "Last heartbeat" }).locator("dd")).toHaveText(timestamp);
  } finally { await agent.dispose(); }
});

test("API failures are visible and never replaced by demo inventory", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Organization code").fill("demo");
  await page.getByLabel("Email").fill("admin@sentinellan.local");
  await page.getByLabel("Password").fill("local-demo-only");
  await page.getByRole("button", { name: "Sign in securely" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await page.route("**/api/v1/dashboard", route => route.fulfill({ status: 503, body: "Unavailable" }));
  await page.reload();
  await expect(page.getByRole("alert").filter({ hasText: "Unable to load dashboard" })).toBeVisible();
  await expect(page.getByText("TRAINING-PC-01")).toHaveCount(0);
  await page.route("**/api/v1/devices", route => route.fulfill({ status: 503, body: "Unavailable" }));
  await page.goto("/devices");
  await expect(page.getByRole("alert").filter({ hasText: "Unable to load devices" })).toBeVisible();
  await page.unroute("**/api/v1/devices");
  await expect(async () => {
    const retry = page.getByRole("button", { name: "Retry" });
    if (await retry.isVisible()) await retry.click({ timeout: 1000 });
    await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible({ timeout: 1000 });
  }).toPass({ timeout: 15_000 });
});
