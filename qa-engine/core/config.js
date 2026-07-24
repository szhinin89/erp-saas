import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');

/** @typedef {{ api: string; frontend: string; reportsDir: string; previousRunPath: string; previousBehaviorPath: string; logVerbose: boolean; skipDbReset: boolean; skipRedisFlush: boolean; readinessTimeoutMs: number; suiteTimeoutMs: number; infraRetryCount: number; redisHost: string; redisPort: number }} QaConfig */

/** @returns {QaConfig} */
export function loadConfig() {
  const reportsDir = process.env.QA_REPORTS_DIR ?? path.join(repoRoot, 'reports');
  return {
    api: (process.env.QA_API_URL ?? 'http://localhost:5003').replace(/\/$/, ''),
    frontend: (process.env.QA_FRONTEND_URL ?? 'http://localhost:4173').replace(/\/$/, ''),
    reportsDir,
    previousRunPath: process.env.QA_PREVIOUS_RUN_PATH ?? path.join(reportsDir, 'previous_run.json'),
    previousBehaviorPath:
      process.env.QA_PREVIOUS_BEHAVIOR_PATH ?? path.join(reportsDir, 'previous_behavior.json'),
    logVerbose: process.env.QA_VERBOSE === '1' || process.env.QA_VERBOSE === 'true',
    skipDbReset: process.env.QA_SKIP_DB_RESET === '1' || process.env.QA_SKIP_DB_RESET === 'true',
    skipRedisFlush: process.env.QA_SKIP_REDIS_FLUSH === '1' || process.env.QA_SKIP_REDIS_FLUSH === 'true',
    readinessTimeoutMs: Number(process.env.QA_READINESS_TIMEOUT_MS ?? 90_000),
    suiteTimeoutMs: Number(process.env.QA_SUITE_TIMEOUT_MS ?? 120_000),
    infraRetryCount: Number(process.env.QA_INFRA_RETRY_COUNT ?? 3),
    redisHost: process.env.QA_REDIS_HOST ?? 'localhost',
    redisPort: Number(process.env.QA_REDIS_PORT ?? 6379),
  };
}
