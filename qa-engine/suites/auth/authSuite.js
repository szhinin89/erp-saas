import { request, unwrapEnvelope } from '../../core/http.js';
import { SEED_MANIFEST } from '../../seed/manifest.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {import('../../seed/seedEngine.js').SeedContext} ctx */
export async function runAuthSuite(runner, api, ctx) {
  await runner.run('auth.login.tenantA', 'Tenant A login', 'auth', true, async ({ trace, recordBehavior }) => {
    trace.push({ step: 'POST /api/auth/login', at: new Date().toISOString() });
    const r = await request('POST', `${api}/api/auth/login`, {
      body: { email: SEED_MANIFEST.subscriberA.adminEmail, password: SEED_MANIFEST.subscriberA.adminPassword },
    });
    if (!r.ok) throw new Error(`status=${r.status}`);
    const data = unwrapEnvelope(r.text);
    const token = data?.token ?? data?.Token;
    if (!token) throw new Error('missing token');
    recordBehavior({
      'auth.login.hasToken': true,
      'auth.login.hasCompanyId': !!(data?.companyId ?? data?.CompanyId),
      'auth.login.responseFields': Object.keys(data ?? {}).sort(),
    });
  });

  await runner.run('auth.refresh.rotate', 'Refresh token rotation', 'auth', true, async ({ trace }) => {
    let refreshToken = ctx.tenantARefreshToken;
    if (!refreshToken) {
      trace.push({ step: 're-login for refresh token', at: new Date().toISOString() });
      const login = await request('POST', `${api}/api/auth/login`, {
        body: {
          email: SEED_MANIFEST.subscriberA.adminEmail,
          password: SEED_MANIFEST.subscriberA.adminPassword,
        },
      });
      const data = unwrapEnvelope(login.text);
      refreshToken = data?.refreshToken ?? data?.RefreshToken;
      if (!refreshToken) throw new Error('no refresh token available');
    }

    const r = await request('POST', `${api}/api/auth/refresh`, {
      body: { refreshToken },
    });
    if (!r.ok) throw new Error(`refresh failed: ${r.status}`);
    const data = unwrapEnvelope(r.text);
    const newToken = data?.token ?? data?.Token;
    const newRefresh = data?.refreshToken ?? data?.RefreshToken;
    if (!newToken || !newRefresh) throw new Error('refresh response incomplete');

    const logout = await request('POST', `${api}/api/auth/logout`, {
      body: { refreshToken: newRefresh },
    });
    if (!logout.ok) throw new Error(`logout failed: ${logout.status}`);

    const reuse = await request('POST', `${api}/api/auth/refresh`, {
      body: { refreshToken: newRefresh },
    });
    if (reuse.ok) throw new Error('revoked refresh token still accepted');
  });

  await runner.run('auth.jwt.tampering', 'JWT tampering rejected', 'auth', true, async ({ recordBehavior }) => {
    const tampered = `${ctx.tenantAToken}x`;
    const r = await request('GET', `${api}/api/admin/iam/me/permissions`, { token: tampered });
    if (r.status !== 401 && r.status !== 403) {
      throw new Error(`tampered token accepted: ${r.status}`);
    }
    recordBehavior({ 'auth.jwt.tampering.status': r.status });
  });

  await runner.run('auth.unauthorized.blocked', 'Unauthenticated permissions blocked', 'auth', true, async ({ recordBehavior }) => {
    const r = await request('GET', `${api}/api/admin/iam/me/permissions`);
    if (r.status !== 401 && r.status !== 403) throw new Error(`status=${r.status}`);
    recordBehavior({ 'auth.unauthorized.status': r.status });
  });
}
