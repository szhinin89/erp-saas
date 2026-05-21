import { describe, expect, it } from 'vitest';
import { isPublicAuthPath, shouldAttemptTokenRefresh } from './authRefreshPolicy';

describe('authRefreshPolicy', () => {
  it('marca /api/auth/refresh como ruta pública (sin reintento recursivo)', () => {
    expect(isPublicAuthPath('/api/auth/refresh')).toBe(true);
    expect(shouldAttemptTokenRefresh(401, '/api/auth/refresh', false)).toBe(false);
  });

  it('permite refresh ante 401 en endpoints protegidos', () => {
    expect(shouldAttemptTokenRefresh(401, '/api/sales/invoices', false)).toBe(true);
  });

  it('no reintenta tras _retry (evita loop infinito)', () => {
    expect(shouldAttemptTokenRefresh(401, '/api/sales/invoices', true)).toBe(false);
  });

  it('ignora status distintos de 401', () => {
    expect(shouldAttemptTokenRefresh(400, '/api/sales/invoices', false)).toBe(false);
    expect(shouldAttemptTokenRefresh(403, '/api/sales/invoices', false)).toBe(false);
  });

  it('no trata login como candidato a refresh', () => {
    expect(shouldAttemptTokenRefresh(401, '/api/auth/login', false)).toBe(false);
    expect(shouldAttemptTokenRefresh(401, '/api/platform/auth/login', false)).toBe(false);
  });
});
