import { defineConfig } from "../../node_modules/@playwright/test/index.mjs";
export default defineConfig({
  testDir: ".",
  testMatch: "vps-local.e2e.ts",
  workers: 1,
  timeout: 90000,
  expect: { timeout: 15000 },
  reporter: "line",
  projects: [{ name: "desktop", use: { viewport: { width: 1440, height: 1000 } } }, { name: "mobile", use: { viewport: { width: 390, height: 844 } } }],
  use: { baseURL: "http://127.0.0.1:3110", viewport: { width: 1440, height: 1000 } },
  webServer: {
    command: "npm.cmd run start --workspace=@sentinellan/company -- --hostname 127.0.0.1 --port 3110",
    cwd: "../..",
    url: "http://127.0.0.1:3110/company/login",
    timeout: 60000,
    reuseExistingServer: false
  }
});

