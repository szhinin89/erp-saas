import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".", testMatch: "purchases-form-visual.spec.ts", workers: 1, timeout: 90000,
  use: { browserName: "chromium" },
});
