import { request, unwrapEnvelope } from '../core/http.js';

/**
 * @param {unknown} subscribers
 * @param {string} slug
 */
export function findSubscriberBySlug(subscribers, slug) {
  const list = Array.isArray(subscribers) ? subscribers : [];
  return list.find((s) => (s.slug ?? s.Slug ?? '').toLowerCase() === slug.toLowerCase());
}

/**
 * @param {string} apiBase
 * @param {string} email
 * @param {string} password
 */
export async function loginTenantSession(apiBase, email, password) {
  const login = await request('POST', `${apiBase}/api/auth/login`, {
    body: { email, password },
  });
  if (!login.ok) {
    throw new Error(`login failed for ${email}: ${login.status} ${login.text}`);
  }

  const data = unwrapEnvelope(login.text);
  let token = data?.token ?? data?.Token;
  let companyId = data?.companyId ?? data?.CompanyId ?? null;
  let refreshToken = data?.refreshToken ?? data?.RefreshToken ?? null;
  const subscriberId = data?.subscriberId ?? data?.SubscriberId ?? null;

  if (!token) throw new Error(`login missing token for ${email}`);

  if (!companyId) {
    const companiesRes = await request('GET', `${apiBase}/api/auth/my-companies`, { token });
    if (!companiesRes.ok) {
      throw new Error(`my-companies failed: ${companiesRes.status}`);
    }
    const companies = unwrapEnvelope(companiesRes.text);
    const first = Array.isArray(companies) ? companies[0] : null;
    companyId = first?.companyId ?? first?.CompanyId ?? first?.id ?? first?.Id ?? null;
    if (!companyId) throw new Error(`no accessible company for ${email}`);

    const switched = await request('POST', `${apiBase}/api/auth/switch-company`, {
      token,
      body: { companyId },
    });
    if (!switched.ok) {
      throw new Error(`switch-company failed: ${switched.status} ${switched.text}`);
    }
    const sw = unwrapEnvelope(switched.text);
    token = sw?.token ?? sw?.Token ?? token;
    refreshToken = sw?.refreshToken ?? sw?.RefreshToken ?? refreshToken;
  }

  return { token, companyId, refreshToken, subscriberId };
}
