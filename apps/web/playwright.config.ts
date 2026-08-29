import { defineConfig, devices } from "@playwright/test";

const apiUrl = "http://127.0.0.1:8080";
const webUrl = "http://127.0.0.1:3000";

export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: "**/*.e2e.ts",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["line"], ["html", { open: "never" }]] : "line",
  use: {
    baseURL: webUrl,
    trace: "retain-on-failure",
    ...devices["Desktop Chrome"]
  },
  webServer: [
    {
      command: "dotnet run --project ../backend/src/SentinelLAN.Api --no-launch-profile",
      url: `${apiUrl}/health/ready`,
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY: "e2e-access-token-signing-key-at-least-32-characters",
        SENTINELLAN_DEMO_ADMIN_PASSWORD: "local-demo-only",
        SENTINELLAN_ENROLLMENT_TOKEN: "local-e2e-enrollment-only",
        SENTINELLAN_WEB_ORIGINS: webUrl
      }
    },
    {
      command: "npm run dev -- --hostname 127.0.0.1 --port 3000",
      url: `${webUrl}/login`,
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      env: { NEXT_PUBLIC_API_URL: apiUrl }
    }
  ]
});
