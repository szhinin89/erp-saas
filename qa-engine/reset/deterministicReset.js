import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { request, unwrapEnvelope } from '../core/http.js';
import { SEED_MANIFEST } from '../seed/manifest.js';

const execFileAsync = promisify(execFile);

/**
 * Flush Redis via redis-cli (no npm deps). Safe for CI/dev QA only.
 * @param {{ host?: string; port?: number; logger?: import('../core/logger.js').QaLogger }} opts
 */
export async function flushRedis(opts = {}) {
  const host = opts.host ?? process.env.QA_REDIS_HOST ?? 'localhost';
  const port = Number(opts.port ?? process.env.QA_REDIS_PORT ?? 6379);
  const logger = opts.logger;

  try {
    await execFileAsync('redis-cli', ['-h', host, '-p', String(port), 'FLUSHDB'], {
      timeout: 10_000,
    });
    logger?.info('Redis FLUSHDB completed', { host, port });
    return { flushed: true, method: 'redis-cli' };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    logger?.warn('Redis flush skipped (redis-cli unavailable)', { error: message, host, port });
    return { flushed: false, method: 'skipped', reason: message };
  }
}

/**
 * Dev API cache probe cleanup + metrics baseline (in-process resets on next read after redis flush).
 * @param {string} apiBase
 * @param {import('../core/logger.js').QaLogger} [logger]
 */
export async function resetCacheProbeState(apiBase, logger) {
  const health = await request('GET', `${apiBase}/api/dev/redis-health`);
  if (!health.ok) {
    logger?.debug('redis-health unavailable (non-Development?)', { status: health.status });
    return { probed: false };
  }
  const data = unwrapEnvelope(health.text);
  logger?.info('Cache probe state verified post-reset', {
    writeReadOk: data?.writeReadOk ?? data?.write_read_ok,
    redisConnected: data?.redisConnected ?? data?.redis_connected,
  });
  return { probed: true, data };
}

/**
 * Full deterministic reset for QA runs.
 * Order: Redis flush → API reset-first-run → superadmin claim.
 * @param {string} apiBase
 * @param {{ skipDbReset?: boolean; skipRedisFlush?: boolean; logger?: import('../core/logger.js').QaLogger }} opts
 */
export async function runDeterministicReset(apiBase, opts = {}) {
  const logger = opts.logger;
  const steps = [];

  if (!opts.skipRedisFlush) {
    const redisResult = await flushRedis({ logger });
    steps.push({ step: 'redis.flush', ...redisResult });

    if (redisResult.flushed) {
      await resetCacheProbeState(apiBase, logger);
      steps.push({ step: 'cache.probe.verify', status: 'ok' });
    }
  } else {
    steps.push({ step: 'redis.flush', status: 'skipped' });
    logger?.info('Redis flush skipped (QA_SKIP_REDIS_FLUSH)');
  }

  if (opts.skipDbReset) {
    logger?.info('DB reset skipped (QA_SKIP_DB_RESET)');
    steps.push({ step: 'db.reset-first-run', status: 'skipped' });
    return { steps, setupToken: null };
  }

  const reset = await request('POST', `${apiBase}/api/dev/reset-first-run`);
  if (!reset.ok) {
    throw new Error(`reset-first-run failed: ${reset.status} ${reset.text}`);
  }
  const resetData = unwrapEnvelope(reset.text);
  const setupToken = resetData?.setupToken ?? resetData?.SetupToken;
  if (!setupToken) throw new Error('reset-first-run missing setupToken');

  steps.push({
    step: 'db.reset-first-run',
    status: 'ok',
    removedSuperAdmins: resetData?.removedSuperAdmins ?? resetData?.RemovedSuperAdmins,
  });

  const claim = await request('POST', `${apiBase}/api/setup/superadmin`, {
    body: {
      setupToken,
      firstName: SEED_MANIFEST.superAdmin.firstName,
      lastName: SEED_MANIFEST.superAdmin.lastName,
      email: SEED_MANIFEST.superAdmin.email,
      password: SEED_MANIFEST.superAdmin.password,
    },
  });
  if (!claim.ok) {
    throw new Error(`claim superadmin failed: ${claim.status} ${claim.text}`);
  }

  steps.push({ step: 'identity.claim-superadmin', status: 'ok' });

  if (!opts.skipRedisFlush) {
    await resetCacheProbeState(apiBase, logger);
  }

  return { steps, setupToken };
}
