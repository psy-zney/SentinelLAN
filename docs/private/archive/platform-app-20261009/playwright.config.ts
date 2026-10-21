import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: "**/*.e2e.ts",
  workers: 1,
  timeout: 60_000,
  reporter: "line",
  use: { baseURL: "http://127.0.0.1:3212", ...devices["Desktop Chrome"] },
  webServer: {
    command: "npm.cmd run start -- --hostname 127.0.0.1 --port 3212",
    url: "http://127.0.0.1:3212/platform",
    reuseExistingServer: false,
    timeout: 60_000
  }
});
