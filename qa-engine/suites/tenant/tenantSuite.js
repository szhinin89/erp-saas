import { request } from '../../core/http.js';
import { SEED_MANIFEST } from '../../seed/manifest.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runTenantSuite(runner, api, ctx) {
  await runner.run('tenant.switch.foreign', 'Foreign company switch blocked', 'tenant', true, async ({ recordBehavior }) => {
    const r = await request('POST', `${api}/api/auth/switch-company`, {
      token: ctx.tenantAToken,
      body: { companyId: SEED_MANIFEST.foreignCompanyId },
    });
    if (r.ok) throw new Error('foreign switch allowed');
    recordBehavior({
      'tenant.isolation.foreignSwitch.status': r.status,
      'tenant.isolation.foreignSwitch.allowed': r.ok,
    });
  });

  await runner.run('tenant.company.foreign.read', 'Foreign company read blocked', 'tenant', true, async ({ recordBehavior }) => {
    const r = await request('GET', `${api}/api/companies/${SEED_MANIFEST.foreignCompanyId}`, {
      token: ctx.tenantAToken,
    });
    if (r.ok) throw new Error('foreign read allowed');
    recordBehavior({
      'tenant.isolation.foreignRead.status': r.status,
      'tenant.isolation.foreignRead.allowed': r.ok,
    });
  });

  await runner.run('tenant.isolation.crossToken', 'Tenant B token cannot read Tenant A company', 'tenant', true, async ({ recordBehavior }) => {
    if (!ctx.tenantACompanyId) {
      throw new Error('tenant A company id unavailable — complete company selection in seed');
    }
    const r = await request('GET', `${api}/api/companies/${ctx.tenantACompanyId}`, {
      token: ctx.tenantBToken,
    });
    if (r.ok) throw new Error('cross-tenant company read allowed');
    recordBehavior({
      'tenant.isolation.crossRead.status': r.status,
      'tenant.isolation.crossRead.allowed': r.ok,
    });
  });

  await runner.run('tenant.subscriber.id.manipulation', 'Invalid subscriber context rejected', 'tenant', true, async ({ recordBehavior }) => {
    const garbage = `${ctx.tenantAToken.slice(0, -4)}0000`;
    const r = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: garbage });
    if (r.ok) throw new Error('tampered subscriber context accepted');
    recordBehavior({
      'tenant.isolation.tamperedToken.status': r.status,
      'tenant.isolation.tamperedToken.allowed': r.ok,
    });
  });
}
