import { defineConfig } from "@playwright/test";
import config from "./playwright.config";

const { webServer, ...sharedConfig } = config;

// UI behavior is verified with intercepted API responses; no backend or device is needed.
export default defineConfig({
  ...sharedConfig,
  testMatch: "**/interface.e2e.ts",
  webServer: Array.isArray(webServer) ? webServer.slice(1).map(server => ({ ...server, reuseExistingServer: !process.env.CI })) : undefined,
});
