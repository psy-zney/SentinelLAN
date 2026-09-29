const employeeUrl = process.env.SENTINELLAN_E2E_EMPLOYEE_URL ?? "http://127.0.0.1:3101";
import { expect, test } from "@playwright/test";

const demoQrCode = "demo-qr-asset-employee-pc-2026";

test("public QR card exposes only the minimal asset identity", async ({ page }) => {
  await page.goto(`${employeeUrl}/employee/qr/${demoQrCode}`);

  await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible();
  await expect(page.getByText(/SentinelLAN/).first()).toBeVisible();
  await expect(page.getByText(/local-demo-only/)).toHaveCount(0);
});

test("assigned employee enters a QR code and reaches My Device", async ({ page }) => {
  await page.goto(`${employeeUrl}/employee/login`);
  await page.locator('input[name="organizationCode"]').fill("demo");
  await page.locator('input[name="email"]').fill("employee@sentinellan.local");
  await page.locator('input[name="password"]').fill("local-demo-only");
  await page.locator("form.login button[type=submit]").click();
  await expect(page).toHaveURL(/\/my-device$/);

  await page.goto(`${employeeUrl}/employee/scan`);
  await page.locator("#manual-code").fill(demoQrCode);
  await page.locator("#manual-code").locator("xpath=ancestor::form").locator('button[type="submit"]').click();

  await expect(page).toHaveURL(/\/my-device$/);
  await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible();
});
