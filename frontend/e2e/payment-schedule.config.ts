import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".", testMatch: "payment-schedule-responsive.spec.ts", workers: 1,
  use: { browserName: "chromium" },
});
