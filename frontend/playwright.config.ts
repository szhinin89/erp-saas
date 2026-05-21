import { defineConfig, devices } from '@playwright/test';

/**
 * Smoke E2E sobre build de producción (`vite preview`).
 * En CI: `npm run build` antes de `npm run test:e2e` (ver workflow).
 */
export default defineConfig({
  testDir: 'e2e',
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:4173',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  // API E2E: E2E_API_URL (default http://localhost:5003). Helpers poll GET /health/live. See scripts/run-e2e.ps1.
  webServer: {
    command: 'npm run preview -- --host 127.0.0.1 --port 4173 --strictPort',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
