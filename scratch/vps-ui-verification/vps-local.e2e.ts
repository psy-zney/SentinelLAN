import { test } from "../../node_modules/@playwright/test/index.mjs";
import "../../apps/company/tests/e2e/vps-status.e2e";
test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/auth/**", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ role: "Admin", displayName: "Local UI fixture" })
  }));
  await page.route("**/api/v1/dashboard", route => route.fulfill({
    status: 503, contentType: "application/json", body: JSON.stringify({ message: "Dashboard outside isolated UI verification" })
  }));
});
