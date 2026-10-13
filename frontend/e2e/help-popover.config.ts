import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".", testMatch: "help-popover.spec.ts", workers: 1,
  use: { baseURL: "http://127.0.0.1:4187", browserName: "chromium" },
  webServer: { command: "npm run dev -- --host 127.0.0.1 --port 4187 --strictPort", url: "http://127.0.0.1:4187", reuseExistingServer: !process.env.CI },
});
