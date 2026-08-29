import { expect, test } from "@playwright/test";

const apiUrl = "http://127.0.0.1:8080";

test("Employee completes login, refresh, assigned-device view, and logout", async ({ page, context }) => {
  await page.goto("/login");
  await page.getByLabel("Organization code").fill("demo");
  await page.getByLabel("Email").fill("employee@sentinellan.local");
  await page.getByLabel("Password").fill("local-demo-only");
  await page.getByRole("button", { name: "Sign in securely" }).click();

  await expect(page).toHaveURL(/\/my-device$/);
  await expect(page.getByRole("heading", { name: "My device" })).toBeVisible();
  await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible();
  await expect(page.getByText("Standard", { exact: true })).toBeVisible();

  const refreshStatus = await page.evaluate(async url => {
    const response = await fetch(`${url}/api/v1/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "X-SentinelLAN-CSRF": "1" }
    });
    return response.status;
  }, apiUrl);
  expect(refreshStatus).toBe(200);

  await page.reload();
  await expect(page.getByText("EMPLOYEE-DEMO-PC")).toBeVisible();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  const cookies = await context.cookies(apiUrl);
  expect(cookies.find(cookie => cookie.name === "sentinellan.access")).toBeUndefined();
  expect(cookies.find(cookie => cookie.name === "sentinellan.refresh")).toBeUndefined();
});
