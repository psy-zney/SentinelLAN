import { expect, test, type Page } from "@playwright/test";

const employeeUrl = process.env.SENTINELLAN_E2E_EMPLOYEE_URL ?? "http://127.0.0.1:3101";

async function mockWorkspace(page: Page, role: "Admin" | "Employee") {
  await page.route("**/api/v1/**", route => route.fulfill({
    json: new URL(route.request().url()).pathname === "/api/v1/auth/session"
      ? { role, displayName: "Nguyễn Minh Anh" }
      : []
  }));
}

test("desktop navigation keeps the current section clear and keyboard skip link usable", async ({ page }) => {
  await mockWorkspace(page, "Admin");
  await page.goto("/company/support");
  const nav = page.getByRole("navigation", { name: "Menu chính", exact: true });
  await expect(nav.getByRole("link", { name: "Hỗ trợ IT", exact: true })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("button", { name: "Mở menu", exact: true })).toBeHidden();
  const skip = page.getByRole("link", { name: "Đến nội dung chính" });
  // Next.js can focus the page heading after navigation; start at the skip link explicitly.
  await skip.focus();
  await expect(skip).toBeFocused();
  await skip.press("Enter");
  await expect(page.locator("#main-content")).toBeFocused();
});

test("mobile admin menu traps focus, closes with Escape and navigates", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockWorkspace(page, "Admin");
  await page.goto("/company/support");
  const trigger = page.getByRole("button", { name: "Mở menu", exact: true });
  await trigger.click();
  const menu = page.getByRole("dialog", { name: "SentinelLAN" });
  await expect(menu).toBeVisible();
  for (let i = 0; i < 13; i++) {
    await page.keyboard.press("Tab");
    expect(await menu.evaluate(element => element.contains(document.activeElement))).toBe(true);
  }
  await page.keyboard.press("Escape");
  await expect(menu).toBeHidden();
  await expect(trigger).toBeFocused();
  await trigger.click();
  await menu.getByRole("link", { name: "Cảnh báo", exact: true }).click();
  await expect(page).toHaveURL(/\/company\/alerts$/);
  await expect(page.getByRole("heading", { name: "Cảnh báo", exact: true, level: 1 })).toBeVisible();
  await expect(menu).toBeHidden();
});

test("employee mobile shortcuts and bottom navigation remain usable without page overflow", async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockWorkspace(page, "Employee");
  await page.goto(`${employeeUrl}/employee/support`);
  const bottom = page.getByRole("navigation", { name: "Menu trên điện thoại" });
  await expect(bottom.getByRole("link")).toHaveCount(3);
  await expect(bottom.getByRole("link", { name: "Hỗ trợ IT", exact: true })).toHaveAttribute("aria-current", "page");
  await expect(page.locator(".content > .page-content")).toHaveCSS("opacity", "1");
  await expect(page.locator(".support-content > .page-content")).toHaveCSS("opacity", "1");
  await page.screenshot({ path: testInfo.outputPath("employee-mobile.png") });
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: testInfo.outputPath("employee-desktop.png"), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole("button", { name: "Tôi cần phần mềm", exact: false }).click();
  await expect(page.getByRole("heading", { name: "Phần mềm công ty cho phép" })).toBeVisible();
  await page.getByRole("button", { name: "Tôi gặp sự cố", exact: false }).click();
  await expect(page.getByRole("form", { name: "Báo sự cố" })).toBeVisible();
  await expect(page.getByRole("form", { name: "Báo sự cố" })).toBeInViewport();
  await expect(page.locator(".support-content")).toBeFocused();
  await expect(page.getByLabel("Bạn có tiếp tục làm việc được không?")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("login password visibility preserves the value and accessible field name", async ({ page }) => {
  await page.goto("/company/login");
  const password = page.getByLabel("Mật khẩu", { exact: true });
  await password.fill("interface-test-value");
  await expect(password).toHaveAttribute("type", "password");
  await page.getByRole("button", { name: "Hiện mật khẩu", exact: true }).click();
  await expect(password).toHaveAttribute("type", "text");
  await expect(password).toHaveValue("interface-test-value");
  await page.getByRole("button", { name: "Ẩn mật khẩu", exact: true }).click();
  await expect(password).toHaveAttribute("type", "password");
});

test("reduced motion removes page travel and drawer animations", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 320, height: 740 });
  await mockWorkspace(page, "Admin");
  await page.goto("/company/support");
  await page.getByRole("button", { name: "Mở menu", exact: true }).click();
  const drawer = page.getByRole("dialog", { name: "SentinelLAN" });
  expect(await drawer.evaluate(element => parseFloat(getComputedStyle(element).animationDuration))).toBeLessThan(0.001);
  await page.keyboard.press("Escape");
  await expect(page.locator(".content > .page-content")).toHaveCSS("transform", "none");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
