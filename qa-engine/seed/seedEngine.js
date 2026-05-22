import { request, unwrapEnvelope } from '../core/http.js';
import { runDeterministicReset } from '../reset/deterministicReset.js';
import { SEED_MANIFEST } from './manifest.js';
import { findSubscriberBySlug, loginTenantSession } from './session.js';

/**
 * @param {string} apiBase
 * @param {string} platformToken
 * @param {typeof SEED_MANIFEST.tenantA} spec
 */
async function ensureSubscriber(apiBase, platformToken, spec) {
  const list = await request('GET', `${apiBase}/api/platform/subscribers`, {
    token: platformToken,
  });
  if (!list.ok) throw new Error(`list subscribers failed: ${list.status}`);

  const listData = unwrapEnvelope(list.text);
  const subscribers = Array.isArray(listData)
    ? listData
    : listData?.subscribers ?? listData?.items ?? listData ?? [];

  const existing = findSubscriberBySlug(subscribers, spec.subscriberSlug);
  if (existing) {
    const subscriberId = existing.id ?? existing.Id ?? existing.subscriberId ?? existing.SubscriberId;
    const session = await loginTenantSession(apiBase, spec.adminEmail, spec.adminPassword);
    return { subscriberId, ...session };
  }

  const create = await request('POST', `${apiBase}/api/platform/subscribers`, {
    token: platformToken,
    body: {
      subscriberName: spec.subscriberName,
      subscriberSlug: spec.subscriberSlug,
      adminFirstName: spec.adminFirstName,
      adminLastName: spec.adminLastName,
      adminEmail: spec.adminEmail,
      adminPassword: spec.adminPassword,
      planCode: spec.planCode,
    },
  });
  if (create.status !== 201 && !create.ok) {
    throw new Error(`create subscriber ${spec.subscriberSlug} failed: ${create.status} ${create.text}`);
  }

  const created = unwrapEnvelope(create.text);
  const token = created?.token ?? created?.Token;
  const companyId = created?.companyId ?? created?.CompanyId;
  const subscriberId = created?.subscriberId ?? created?.SubscriberId;

  if (token && companyId) {
    return { subscriberId, token, companyId, refreshToken: null };
  }

  const session = await loginTenantSession(apiBase, spec.adminEmail, spec.adminPassword);
  return { subscriberId: subscriberId ?? session.subscriberId, ...session };
}

/**
 * Deterministic seed via Development APIs (no production code coupling).
 * @param {string} apiBase
 * @param {{ skipDbReset?: boolean; skipRedisFlush?: boolean; logger?: import('../core/logger.js').QaLogger }} [opts]
 * @returns {Promise<SeedContext>}
 */
export async function runSeedEngine(apiBase, opts = {}) {
  const logger = opts.logger;
  logger?.info('QA Seed Engine start');

  if (!opts.skipDbReset || !opts.skipRedisFlush) {
    await runDeterministicReset(apiBase, {
      skipDbReset: opts.skipDbReset,
      skipRedisFlush: opts.skipRedisFlush,
      logger,
    });
  } else {
    logger?.info('Reset skipped (QA_SKIP_DB_RESET + QA_SKIP_REDIS_FLUSH)');
  }

  const platformLogin = await request('POST', `${apiBase}/api/platform/auth/login`, {
    body: {
      email: SEED_MANIFEST.superAdmin.email,
      password: SEED_MANIFEST.superAdmin.password,
    },
  });
  if (!platformLogin.ok) {
    throw new Error(`platform login failed: ${platformLogin.status} ${platformLogin.text}`);
  }
  const platformData = unwrapEnvelope(platformLogin.text);
  const platformToken = platformData?.token ?? platformData?.Token;
  if (!platformToken) throw new Error('platform login missing token');

  const tenantA = await ensureSubscriber(apiBase, platformToken, SEED_MANIFEST.tenantA);
  const tenantB = await ensureSubscriber(apiBase, platformToken, SEED_MANIFEST.tenantB);

  if (!tenantA.token || !tenantB.token) throw new Error('seed missing session tokens');
  if (!tenantA.companyId || !tenantB.companyId) throw new Error('seed missing company ids');

  logger?.info('Seed complete', {
    tenantA: SEED_MANIFEST.tenantA.subscriberSlug,
    tenantB: SEED_MANIFEST.tenantB.subscriberSlug,
  });

  return Object.freeze({
    platformToken,
    tenantAToken: tenantA.token,
    tenantBToken: tenantB.token,
    tenantACompanyId: tenantA.companyId,
    tenantBCompanyId: tenantB.companyId,
    tenantASubscriberId: tenantA.subscriberId,
    tenantBSubscriberId: tenantB.subscriberId,
    tenantARefreshToken: tenantA.refreshToken,
  });
}
