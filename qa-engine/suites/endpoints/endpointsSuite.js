import { request } from '../../core/http.js';

const CRITICAL_ENDPOINTS = [
  { id: 'ep.health.live', path: '/health/live', auth: false, method: 'GET' },
  { id: 'ep.health.ready', path: '/health/ready', auth: false, method: 'GET' },
  { id: 'ep.auth.login', path: '/api/auth/login', auth: false, method: 'POST', body: { email: 'x@y.z', password: 'bad' }, expectUnauthorized: true },
  { id: 'ep.permissions.me', path: '/api/admin/iam/me/permissions', auth: true, method: 'GET' },
  { id: 'ep.companies.my', path: '/api/auth/my-companies', auth: true, method: 'GET' },
];

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runEndpointsSuite(runner, api, ctx) {
  for (const ep of CRITICAL_ENDPOINTS) {
    await runner.run(ep.id, `Endpoint ${ep.method} ${ep.path}`, 'endpoints', ep.auth, async () => {
      const r = await request(ep.method, `${api}${ep.path}`, {
        token: ep.auth ? ctx.tenantAToken : undefined,
        body: ep.body,
      });
      if (ep.expectUnauthorized) {
        if (r.status !== 401 && r.status !== 400 && r.status !== 422) {
          throw new Error(`expected auth failure, got ${r.status}`);
        }
        return;
      }
      if (ep.auth && (r.status === 401 || r.status === 403)) {
        throw new Error(`protected endpoint rejected session: ${r.status}`);
      }
      if (!ep.auth && !r.ok && r.status >= 500) {
        throw new Error(`server error: ${r.status}`);
      }
    });
  }
}
