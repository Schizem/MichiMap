import { defineConfig, devices } from '@playwright/test';

// BASE_URL is injected by CI; locally the dev server is started automatically below.
const baseURL = process.env['BASE_URL'] ?? 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: [['html', { outputFolder: 'playwright-report', open: 'never' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ],

  // Automatically start the Angular dev server when running tests locally.
  // In CI, BASE_URL is set externally and the server is expected to be running.
  webServer: process.env['CI'] ? undefined : {
    command: 'npx ng serve',
    url: baseURL,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
