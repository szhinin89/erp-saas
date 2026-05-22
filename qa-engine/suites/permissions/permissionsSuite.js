import { request, unwrapEnvelope } from '../../core/http.js';
import { SEED_MANIFEST } from '../../seed/manifest.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runPermissionsSuite(runner, api, ctx) {
  await runner.run('permissions.me.snapshot', 'Session permissions snapshot', 'permissions', true, async ({ trace, recordBehavior }) => {
    trace.push({ step: 'GET /api/admin/iam/me/permissions', at: new Date().toISOString() });
    const r = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: ctx.tenantAToken });
    if (!r.ok) throw new Error(`status=${r.status}`);
    const data = unwrapEnvelope(r.text);
    const keys = data?.permissions ?? data?.Permissions ?? [];
    if (!Array.isArray(keys) || keys.length === 0) throw new Error('empty permissions');
    recordBehavior({
      endpoint: '/api/admin/iam/me/permissions',
      'permissions.count': keys.length,
      'permissions.hasWildcard': keys.includes('*'),
    });
  });

  await runner.run('permissions.admin.wildcard', 'Admin receives unrestricted snapshot', 'permissions', true, async ({ recordBehavior }) => {
    const r = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: ctx.tenantAToken });
    const data = unwrapEnvelope(r.text);
    const keys = data?.permissions ?? data?.Permissions ?? [];
    if (!keys.includes('*')) throw new Error('admin missing wildcard in UI snapshot');
    recordBehavior({ 'permissions.admin.wildcard': true });
  });

  await runner.run('permissions.activity.enforced', 'Activity endpoint requires session perm', 'permissions', true, async ({ recordBehavior }) => {
    const unauth = await request('GET', `${api}/api/admin/activity/my`);
    if (unauth.status !== 401 && unauth.status !== 403) {
      throw new Error(`unauth activity allowed: ${unauth.status}`);
    }
    const auth = await request('GET', `${api}/api/admin/activity/my`, { token: ctx.tenantAToken });
    if (auth.status === 401) throw new Error('valid session rejected by activity endpoint');
    recordBehavior({
      'activity.unauth.status': unauth.status,
      'activity.auth.status': auth.status,
    });
  });

  await runner.run('permissions.bootstrap.bypass', 'Bootstrap token cannot access business API', 'permissions', true, async ({ recordBehavior }) => {
    const boot = await request('POST', `${api}/api/admin/iam/bootstrap-login`, {
      body: {
        email: SEED_MANIFEST.tenantA.adminEmail,
        password: SEED_MANIFEST.tenantA.adminPassword,
      },
    });
    if (!boot.ok) throw new Error(`bootstrap login failed: ${boot.status}`);
    const data = unwrapEnvelope(boot.text);
    const bootstrapToken = data?.bootstrapToken ?? data?.BootstrapToken;
    if (!bootstrapToken) throw new Error('no bootstrap token');

    const activity = await request('GET', `${api}/api/admin/activity/my`, { token: bootstrapToken });
    if (activity.ok) throw new Error('bootstrap token reached business endpoint');
    recordBehavior({
      'bootstrap.activity.status': activity.status,
      'bootstrap.activity.allowed': activity.ok,
    });
  });
}
