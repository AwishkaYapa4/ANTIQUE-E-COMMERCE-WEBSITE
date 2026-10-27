import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests run the real stack locally:
 *   - Firebase Auth Emulator (no real Firebase project or credentials)
 *   - ASP.NET Core API with a local SQLite database and the development example catalog
 *   - Vite dev server in "e2e" mode (see .env.e2e)
 * Existing servers on the same ports are reused.
 */
const apiDir = '../backend/AntiqueStore.Api';
export const e2eEnv = {
  ASPNETCORE_ENVIRONMENT: 'Development',
  Database__Provider: 'Sqlite',
  ConnectionStrings__Default: 'Data Source=App_Data/dev.db',
  Database__ApplyMigrationsOnStartup: 'true',
  Database__SeedExampleData: 'true',
  Firebase__ProjectId: 'demo-antique-store',
  Firebase__AuthEmulatorHost: '127.0.0.1:9099',
};

export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://localhost:5173', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'npx -y firebase-tools@14 emulators:start --only auth --project demo-antique-store',
      cwd: '..',
      url: 'http://127.0.0.1:9099',
      reuseExistingServer: true,
      timeout: 180_000,
    },
    {
      command: 'dotnet run --no-launch-profile --urls http://localhost:5080',
      cwd: apiDir,
      url: 'http://localhost:5080/health/ready',
      env: e2eEnv,
      reuseExistingServer: true,
      timeout: 240_000,
    },
    {
      command: 'npx vite --mode e2e --port 5173 --strictPort',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});
