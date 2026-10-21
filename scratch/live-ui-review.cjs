const { chromium, expect } = require('@playwright/test');
const { execFileSync } = require('node:child_process');
const path = require('node:path');

(async () => {
  // SSH output is captured in memory. Credentials are never printed or saved.
  const accounts = JSON.parse(execFileSync('ssh', ['-o', 'BatchMode=yes', 'vps-cong', 'sudo', '-n', 'python3', '/home/ubuntu/read-ui-accounts.py'], { encoding: 'utf8' }));
  const browser = await chromium.launch({ headless: true });
  const origin = 'https://sentinellan.149.118.50.176.sslip.io';
  try {
    for (const app of process.argv.includes('--platform-only') ? ['platform'] : ['platform', 'company', 'employee']) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', () => errors.push('runtime error'));
      const response = await page.goto(`${origin}/${app}/login`);
      expect(response.status()).toBe(200);
      await expect(page.getByRole('heading', { name: 'Đăng nhập', exact: true })).toBeVisible();
      await expect(page.locator('.dev-helper')).toHaveCount(0);
      await page.screenshot({ path: path.join(__dirname, 'ui-review', `live-${app}-login.png`) });
      if (accounts[app]) {
        const account = accounts[app];
        if (app === 'company') await page.locator('input[name="organizationCode"]').fill(account.code);
        await page.locator('input[name="email"]').fill(account.email);
        await page.locator('input[name="password"]').fill(account.password);
        const loginResponse = page.waitForResponse(r => r.url().includes('/auth/login') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
        expect((await loginResponse).status()).toBe(200);
        await page.waitForURL(url => !url.pathname.endsWith('/login'));
        await expect(page.getByRole('heading', { name: app === 'platform' ? 'Công ty' : 'Tổng quan', exact: true })).toBeVisible();
        if (app === 'platform') {
          await expect(page.getByText('Dữ liệu: Đã kết nối', { exact: true })).toBeVisible();
          expect(await page.locator('.company-table button').evaluateAll(buttons => buttons.every(button => button.getBoundingClientRect().height <= 39))).toBe(true);
        }
        else await expect(page.locator('.nav [aria-current="page"]')).toHaveText('Tổng quan');
        await page.screenshot({ path: path.join(__dirname, 'ui-review', `live-${app}.png`) });
        await page.setViewportSize({ width: 390, height: 844 });
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
        await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
        await page.waitForURL(url => url.pathname.endsWith('/login'));
      }
      expect(errors).toEqual([]);
      console.log(`PASS HTTPS ${app}: production page${accounts[app] ? ', real sign-in, data, mobile width and sign-out' : ''}`);
      await context.close();
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
