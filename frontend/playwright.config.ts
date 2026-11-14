import { defineConfig } from '@playwright/test'
export default defineConfig({
  testDir: './tests', workers: 1,
  use: { baseURL: 'http://127.0.0.1:5187', headless: true,
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {} },
  webServer: { command: 'dotnet run --project ../src/Api --no-launch-profile', url: 'http://127.0.0.1:5187', reuseExistingServer: true, timeout: 120000 },
})
