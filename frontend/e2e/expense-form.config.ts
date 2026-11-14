import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".", testMatch: "expense-form-responsive.spec.ts", workers: 1,
  use: { browserName: "chromium" },
});
