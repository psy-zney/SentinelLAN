import { expect, test } from "@playwright/test";

test("Admin can create an Employee, issue a one-time token, and open assignment controls", async ({ page }) => {
  const suffix = `${Date.now()}`;
  const email = `employee-${suffix}@sentinellan.local`;
  await page.goto("/login");
  await page.locator('input[name="organizationCode"]').fill("demo");
  await page.locator('input[name="email"]').fill("admin@sentinellan.local");
  await page.locator('input[name="password"]').fill("local-demo-only");
  await page.locator("form.login button[type=submit]").click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto("/users");
  await page.getByRole("button", { name: "+ Tạo tài khoản" }).click();
  await page.locator("#user-display-name").fill(`E2E Employee ${suffix}`);
  await page.locator("#user-email").fill(email);
  await page.locator("#user-reason").fill("E2E onboarding lifecycle");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Lưu" }).click();
  await expect(page.locator('input[readonly][value*="/activate?token="]')).toBeVisible();
  await expect(page.getByRole("cell", { name: email, exact: true })).toBeVisible();

  await page.goto("/devices");
  await page.getByRole("button", { name: "+ Cấp token enrollment" }).click();
  await page.locator("#token-reason").fill("E2E device enrollment");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Lưu" }).click();
  await expect(page.getByLabel("Enrollment token")).toBeVisible();
  await expect(page.getByText("Token chỉ hiển thị trong phiên này.")).toBeVisible();
});
