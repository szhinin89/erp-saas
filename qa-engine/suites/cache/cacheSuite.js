import { request, unwrapEnvelope } from '../../core/http.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runCacheSuite(runner, api, ctx) {
  await runner.run('cache.redis.health', 'Redis write/read probe', 'cache', true, async () => {
    const r = await request('GET', `${api}/api/dev/redis-health`);
    if (!r.ok) throw new Error(`status=${r.status}`);
    const data = unwrapEnvelope(r.text);
    if (!data?.writeReadOk) throw new Error('redis write/read failed');
  });

  await runner.run('cache.metrics.available', 'Cache metrics endpoint', 'cache', false, async () => {
    const r = await request('GET', `${api}/api/dev/cache-metrics`);
    if (!r.ok) throw new Error(`status=${r.status}`);
    const data = unwrapEnvelope(r.text);
    if (data?.cache_hit_total === undefined && data?.cacheHitTotal === undefined) {
      throw new Error('missing hit metrics');
    }
  });

  await runner.run('cache.permissions.consistency', 'Permissions stable across repeated reads', 'cache', true, async () => {
    const r1 = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: ctx.tenantAToken });
    const r2 = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: ctx.tenantAToken });
    if (!r1.ok || !r2.ok) throw new Error('permissions read failed');
    const d1 = unwrapEnvelope(r1.text);
    const d2 = unwrapEnvelope(r2.text);
    const k1 = JSON.stringify(d1?.permissions ?? d1?.Permissions ?? []);
    const k2 = JSON.stringify(d2?.permissions ?? d2?.Permissions ?? []);
    if (k1 !== k2) throw new Error('permission snapshot drift between reads');
  });
}
