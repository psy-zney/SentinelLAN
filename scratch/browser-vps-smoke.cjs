const { execFileSync } = require('node:child_process');
const { chromium, expect } = require('@playwright/test');

(async () => {
  // Capture into memory only; do not print login credentials or browser cookies.
  const credentials = JSON.parse(execFileSync('ssh', ['-o', 'BatchMode=yes', 'vps-cong', 'sudo', '-n', 'python3', '/opt/sentinellan/shared/browser-credentials.py'], { encoding: 'utf8' }));
  const browser = await chromium.launch({ headless: true });
  try {
    for (const portal of ['platform', 'company', 'employee']) {
      const context = await browser.newContext();
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', () => errors.push('browser runtime error'));
      const account = credentials[portal];
      await page.goto(`${credentials.origin}/${portal}/login`, { waitUntil: 'networkidle' });
      await expect(page.getByText(/Thử nghiệm nhanh \(Demo\)|Điền tài khoản Demo Dev/)).toHaveCount(0);
      if (portal !== 'platform') await page.locator('input[name="organizationCode"]').fill(account.code);
      await page.locator('input[name="email"]').fill(account.email);
      await page.locator('input[name="password"]').fill(account.password);
      const loginPath = portal === 'platform' ? '/api/v1/platform/auth/login' : '/api/v1/auth/login';
      const loggedIn = page.waitForResponse(response => response.url().endsWith(loginPath) && response.request().method() === 'POST');
      await page.locator('button[type="submit"]').click();
      if ((await loggedIn).status() !== 200) throw new Error(`${portal} UI login failed`);
      await page.waitForURL(url => !url.pathname.endsWith('/login'), { timeout: 30000 });
      await page.waitForLoadState('networkidle');
      if (portal === 'platform') {
        await expect(page.getByRole('heading', { name: 'SentinelLAN Platform Control Center' })).toBeVisible();
        await expect(page.getByText('VPS Backend: Online')).toBeVisible();
        const system = page.waitForResponse(response => response.url().endsWith('/api/v1/platform/system/status'));
        await page.reload({ waitUntil: 'networkidle' });
        if (!(await (await system).json()).databaseConnected) throw new Error('Platform database status failed');
      } else if (portal === 'company') {
        const response = await page.request.get(`${credentials.origin}/api/v1/dashboard`);
        if (response.status() !== 200) throw new Error('Admin browser session cannot access dashboard');
      } else {
        const response = await page.request.get(`${credentials.origin}/api/v1/my-device`);
        if (response.status() !== 200 || (await response.json()).device.id !== credentials.deviceId) throw new Error('Employee browser session cannot access assigned device');
        await expect(page.getByText('DEPLOY-CHECK-API-HANDSHAKE').first()).toBeVisible();
      }
      if (errors.length) throw new Error(`${portal} has browser runtime errors`);
      console.log(`PASS ${portal}: HTTPS UI login, authenticated page and backend data; production demo helpers hidden`);
      await context.close();
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
