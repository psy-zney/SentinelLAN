import { defineConfig, devices } from "@playwright/test";

const apiUrl = process.env.SENTINELLAN_E2E_API_URL ?? "http://127.0.0.1:8080";
const webUrl = process.env.SENTINELLAN_E2E_WEB_URL ?? "http://127.0.0.1:3100";
const employeeUrl = process.env.SENTINELLAN_E2E_EMPLOYEE_URL ?? "http://127.0.0.1:3101";
const platformUrl = process.env.SENTINELLAN_E2E_PLATFORM_URL ?? "http://127.0.0.1:3102";
const webPort = new URL(webUrl).port || "3100";
const apiCommand = process.env.SENTINELLAN_E2E_API_COMMAND ?? "dotnet ../backend/src/SentinelLAN.Api/bin/Debug/net10.0/SentinelLAN.Api.dll";

export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: "**/*.e2e.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: {
    timeout: 30_000
  },
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["line"], ["html", { open: "never" }]] : "line",
  use: {
    baseURL: webUrl,
    trace: "retain-on-failure",
    ...devices["Desktop Chrome"]
  },
  webServer: [
    {
      command: apiCommand,
      url: `${apiUrl}/health/ready`,
      timeout: 180_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY: "e2e-access-token-signing-key-at-least-32-characters",
        SENTINELLAN_DEMO_ADMIN_PASSWORD: "local-demo-only",
        SENTINELLAN_ENROLLMENT_TOKEN: "local-e2e-enrollment-only",
        SENTINELLAN_PLATFORM_OWNER_EMAIL: "owner@e2e.test",
        SENTINELLAN_PLATFORM_OWNER_PASSWORD: "Owner-e2e-only-2026-secret!",
        SENTINELLAN_WEB_ORIGINS: `${webUrl},${employeeUrl},${platformUrl},http://localhost:${webPort}`
      }
    },
    {
      command: `npm run dev -- --hostname 127.0.0.1 --port ${webPort}`,
      url: `${webUrl}/company/login`,
      timeout: 180_000,
      reuseExistingServer: false,
      env: { NEXT_PUBLIC_API_URL: apiUrl, NEXT_PUBLIC_EMPLOYEE_URL: `${employeeUrl}/employee`, NEXT_PUBLIC_COMPANY_URL: `${webUrl}/company` }
    },
    {
      command: `npm run dev --workspace=@sentinellan/employee -- --hostname 127.0.0.1 --port ${new URL(employeeUrl).port}`,
      url: `${employeeUrl}/employee/login`, timeout: 180_000, reuseExistingServer: false,
      env: { NEXT_PUBLIC_API_URL: apiUrl, NEXT_PUBLIC_EMPLOYEE_URL: `${employeeUrl}/employee`, NEXT_PUBLIC_COMPANY_URL: `${webUrl}/company` }
    },
    {
      command: `npm run dev --workspace=@sentinellan/platform -- --hostname 127.0.0.1 --port ${new URL(platformUrl).port}`,
      url: `${platformUrl}/platform/login`, timeout: 180_000, reuseExistingServer: false,
      env: { NEXT_PUBLIC_API_URL: apiUrl, NEXT_PUBLIC_COMPANY_URL: `${webUrl}/company` }
    }
  ]
});
