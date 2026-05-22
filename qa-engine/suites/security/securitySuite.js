import { request, unwrapEnvelope } from '../../core/http.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runSecuritySuite(runner, api, ctx) {
  await runner.run('security.noauth.companies', 'Companies API requires auth', 'security', true, async () => {
    const r = await request('GET', `${api}/api/companies`);
    if (r.status !== 401 && r.status !== 403) throw new Error(`status=${r.status}`);
  });

  await runner.run('security.role.escalation.platform', 'Tenant token cannot list platform subscribers', 'security', true, async () => {
    const r = await request('GET', `${api}/api/platform/subscribers`, { token: ctx.tenantAToken });
    if (r.ok) throw new Error('tenant accessed platform admin API');
  });

  await runner.run('security.subscribers.mutation', 'Tenant cannot patch foreign operational settings', 'security', true, async () => {
    if (!ctx.tenantBSubscriberId) throw new Error('tenant B id missing');
    const r = await request('PATCH', `${api}/api/Subscribers/${ctx.tenantBSubscriberId}/operational-settings`, {
      token: ctx.tenantAToken,
      body: {
        currency: 'USD',
        language: 'es',
        timezone: 'America/Guayaquil',
        invoicePrefix: 'X',
        defaultCreditDays: 30,
      },
    });
    if (r.ok) throw new Error('cross-tenant operational settings mutation allowed');
  });

  await runner.run('security.password.reset.mode', 'Tenant A cannot change Tenant B password reset mode', 'security', true, async () => {
    if (!ctx.tenantBSubscriberId) throw new Error('tenant B id missing');
    const r = await request('PATCH', `${api}/api/Subscribers/${ctx.tenantBSubscriberId}/password-reset-mode`, {
      token: ctx.tenantAToken,
      body: { subscriberId: ctx.tenantBSubscriberId, passwordResetMode: 1 },
    });
    if (r.ok) throw new Error('cross-tenant password reset mode allowed');
  });
}
