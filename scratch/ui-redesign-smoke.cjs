const { chromium, expect } = require('@playwright/test');
const fs = require('node:fs');
const path = require('node:path');

// Visual checks use local pages and synthetic API responses only.
const out = path.join(__dirname, 'ui-review');
fs.mkdirSync(out, { recursive: true });
const stamp = new Date().toISOString();
const id = '11111111-1111-4111-8111-111111111111';
const devices = ['Kế toán · Máy 01', 'Nhân sự · Máy 02', 'Phòng họp · Máy 03'].map((name, i) => ({ id: id.slice(0, -1) + i, name, osVersion: 'Windows 11', agentVersion: '0.1.0', lastSeenAt: stamp, isOnline: i !== 2, isRevoked: false, assignedUserId: id }));
const companies = ['Công ty An Bình', 'Công ty Minh Phát', 'Văn phòng Hà Nội'].map((name, i) => ({ id: id.slice(0, -1) + i, name, code: ['an-binh', 'minh-phat', 'ha-noi'][i], isSuspended: i === 2, users: [24, 12, 8][i], devices: [18, 10, 6][i], onlineDevices: [16, 9, 0][i], lastSeenAt: stamp, createdAt: stamp }));
const sys = { databaseConnected: true, databaseLatencyMs: 1.8, databaseProvider: 'PostgreSQL', hostName: 'sentinellan-api', osDescription: 'Linux', runtimeVersion: '.NET 10', uptimeSeconds: 32400, memoryUsageMb: 146, totalCompanies: 3, activeCompanies: 2, suspendedCompanies: 1, totalDevices: 34, onlineDevices: 25, totalUsers: 44, vpsNodes: [] };
const node = { id, name: 'Máy chủ văn phòng', host: '192.0.2.10', port: 22, username: 'sentinellan', hostKeyFingerprint: 'SHA256:' + 'a'.repeat(43), status: 'Online', cpuPercent: 12, ramPercent: 38, diskPercent: 24, lastCheckedAt: stamp, createdAt: stamp, updatedAt: stamp, osInfo: 'Ubuntu Linux', uptime: '2 ngày', dockerContainersCount: 6, runtime: { systemState: 'running', dockerAvailable: true, dockerError: null, services: [{ name: 'nginx', activeState: 'active', startupState: 'enabled' }], containers: [] } };

async function mock(context, employee, writes) {
  await context.route('**/api/v1/**', async route => {
    const req = route.request();
    const url = new URL(req.url());
    const p = url.pathname;
    const headers = { 'Access-Control-Allow-Origin': req.headers().origin || '*', 'Access-Control-Allow-Credentials': 'true', 'Access-Control-Allow-Headers': 'Content-Type,X-SentinelLAN-CSRF', 'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS' };
    if (req.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    let data = [];
    if (req.method() !== 'GET') writes.push({ path: p, data: req.postDataJSON() });
    if (p.endsWith('/platform/companies') && req.method() === 'POST') data = { organizationId: id, organizationCode: 'mau', adminEmail: 'quantri@example.test', activationUrl: '/company/activate#token=visual-sample', expiresAt: stamp };
    else if (p.endsWith('/platform/companies')) data = companies;
    else if (p.endsWith('/platform/system/status')) data = sys;
    else if (p.includes('/platform/companies/')) data = { ...companies[0], updatedAt: stamp, users: [{ id, email: 'quantri@example.test', displayName: 'Nguyễn An', role: 'Admin', status: 'Active', createdAt: stamp }], devices: devices.map(d => ({ ...d, assignedUserDisplayName: 'Nguyễn An' })), alerts: [], recentAudits: [] };
    else if (p.endsWith('/platform/vps-nodes')) data = [node];
    else if (p.endsWith('/refresh-metrics')) data = node;
    else if (p.endsWith('/auth/session')) data = { role: employee ? 'Employee' : 'Admin', displayName: employee ? 'Nguyễn An' : 'Lê Minh' };
    else if (p.endsWith('/auth/login')) data = { role: employee ? 'Employee' : 'Admin', displayName: 'Nguyễn An' };
    else if (p.endsWith('/dashboard')) data = { totalDevices: 3, onlineDevices: 2, offlineDevices: 1, openAlerts: 1, devices };
    else if (p.endsWith('/devices')) data = devices;
    else if (p.endsWith('/my-device')) data = { device: devices[0], appliedPolicy: null, latestTelemetry: null, incidents: [], recentActions: [], privacyManifest: { collectedTechnicalData: ['Tên máy, hệ điều hành', 'CPU, bộ nhớ và ổ đĩa'], strictlyProhibitedData: ['Nội dung bàn phím', 'Ảnh chụp màn hình không được bạn chọn'], agentPermissions: [], dataRetentionDays: 30 }, serialNumber: 'ABC-2026', manufacturer: 'Dell', model: 'Latitude 5440', assetType: 'Laptop', location: 'Văn phòng · Tầng 2', assignedAt: stamp };
    else if (p.includes('/my-device/telemetry')) data = [{ id, deviceId: devices[0].id, cpuPercent: 12, ramPercent: 38, diskPercent: 24, createdAt: stamp }];
    await route.fulfill({ status: 200, contentType: 'application/json', headers, body: JSON.stringify(data) });
  });
}

async function checkWidth(page, label) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), label + ': viewport overflow').toBe(true);
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    for (const app of ['platform', 'company', 'employee']) {
      const writes = [];
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
      await mock(context, app === 'employee', writes);
      const page = await context.newPage();
      const runtimeErrors = [];
      page.on('pageerror', error => runtimeErrors.push(error.message));
      const origin = `http://localhost:${{ company: 3000, employee: 3001, platform: 3002 }[app]}`;
      await page.goto(`${origin}/${app}/login`);
      await expect(page.getByRole('heading', { name: 'Đăng nhập', exact: true })).toBeVisible();
      await page.screenshot({ path: path.join(out, `${app}-login.png`) });
      await page.setViewportSize({ width: 390, height: 844 });
      await checkWidth(page, app + ' mobile login');
      await page.setViewportSize({ width: 1440, height: 1000 });
      if (app === 'platform') {
        await page.goto(`${origin}/platform`);
        await expect(page.getByRole('heading', { name: 'Công ty', exact: true })).toBeVisible();
        await expect(page.getByText('Công ty An Bình', { exact: true })).toBeVisible();
        await page.screenshot({ path: path.join(out, 'platform-companies.png') });
        await page.getByRole('button', { name: 'Xem chi tiết', exact: true }).first().click();
        await expect(page.getByRole('dialog', { name: /Chi tiết công ty/ })).toBeVisible();
        await expect(page.getByText('Quản trị viên', { exact: true })).toBeVisible();
        await page.keyboard.press('Escape');
        await expect(page.getByRole('dialog')).toHaveCount(0);
        await page.getByRole('button', { name: 'Máy chủ', exact: true }).click();
        await expect(page.getByText('Máy chủ văn phòng', { exact: true })).toBeVisible();
        await page.screenshot({ path: path.join(out, 'platform-server.png') });
        await page.getByRole('button', { name: 'Thêm máy chủ', exact: true }).click();
        await expect(page.getByLabel('Khóa riêng SSH (private key)')).toBeVisible();
        await page.getByText('Cách lấy mã xác minh máy chủ').click();
        await expect(page.getByText('Sao chép phần bắt đầu bằng SHA256: vào ô bên trên.')).toBeVisible();
        await page.setViewportSize({ width: 390, height: 844 });
        await checkWidth(page, 'SSH form');
        await page.screenshot({ path: path.join(out, 'platform-ssh-mobile.png') });
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: 'Thêm công ty', exact: true }).click();
        await page.locator('input[name="code"]').fill('mau');
        await page.locator('input[name="name"]').fill('Công ty mẫu');
        await page.locator('input[name="adminEmail"]').fill('quantri@example.test');
        await page.locator('input[name="adminName"]').fill('Nguyễn An');
        await page.locator('input[name="reason"]').fill('Kiểm tra giao diện');
        await page.getByRole('button', { name: 'Tạo công ty', exact: true }).click();
        expect(writes.filter(w => w.path.endsWith('/companies'))).toHaveLength(0);
        await page.getByRole('checkbox').check();
        await page.getByRole('button', { name: 'Tạo công ty', exact: true }).click();
        await expect(page.getByRole('dialog', { name: 'Liên kết kích hoạt' })).toBeVisible();
        const sent = writes.find(w => w.path.endsWith('/companies'));
        expect(sent.data.confirmation.confirmed).toBe(true);
        expect(sent.data.confirmation.nonce).toBeTruthy();
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: 'Công ty', exact: true }).click();
      } else if (app === 'company') {
        await page.goto(`${origin}/company/dashboard`);
        await expect(page.getByRole('heading', { name: 'Tổng quan', exact: true })).toBeVisible();
        await expect(page.locator('.nav [aria-current="page"]')).toHaveText('Tổng quan');
        await expect(page.getByText(devices[0].name, { exact: true })).toBeVisible();
        await page.screenshot({ path: path.join(out, 'company-dashboard.png') });
        await page.getByRole('link', { name: 'Thiết bị', exact: true }).click();
        await expect(page.locator('.nav [aria-current="page"]')).toHaveText('Thiết bị');
        await page.locator('#device-search').fill('Phòng họp');
        await expect(page.getByText(devices[0].name, { exact: true })).toHaveCount(0);
        await expect(page.getByText(devices[2].name, { exact: true })).toBeVisible();
      } else {
        await page.goto(`${origin}/employee/my-device`);
        await expect(page.getByRole('heading', { name: 'Máy tính của tôi', exact: true })).toBeVisible();
        await expect(page.locator('.nav [aria-current="page"]')).toHaveText('Máy tính của tôi');
        await expect(page.getByText('Bộ xử lý (CPU)', { exact: true })).toBeHidden();
        await page.getByText('Thông số máy tính', { exact: true }).click();
        await expect(page.getByText('Bộ xử lý (CPU)', { exact: true })).toBeVisible();
        await page.getByText('Thông số máy tính', { exact: true }).click();
        await page.screenshot({ path: path.join(out, 'employee-device.png') });
        await page.getByRole('button', { name: 'Nhờ IT hỗ trợ', exact: true }).first().click();
        await expect(page.getByLabel('Bạn còn làm việc được không?')).toBeVisible();
        await expect(page.getByLabel('Bạn còn làm việc được không?').locator('option')).toHaveText(['Có, tôi vẫn làm việc được', 'Có, nhưng công việc bị gián đoạn', 'Không, tôi cần IT hỗ trợ']);
        await page.getByRole('button', { name: 'Hủy', exact: true }).click();
        await page.getByRole('link', { name: 'Hỗ trợ IT', exact: true }).click();
        await expect(page.getByRole('button', { name: /Tôi gặp sự cố/ })).toBeVisible();
        await page.screenshot({ path: path.join(out, 'employee-support.png') });
      }
      await page.setViewportSize({ width: 390, height: 844 });
      await checkWidth(page, app + ' mobile workspace');
      await page.screenshot({ path: path.join(out, `${app}-mobile.png`), fullPage: true });
      expect(runtimeErrors, app + ' runtime errors').toEqual([]);
      console.log(`PASS ${app}: labels, navigation, workflows and mobile width; screenshots saved`);
      await context.close();
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
