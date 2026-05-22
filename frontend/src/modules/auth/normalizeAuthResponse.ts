import type { AuthResponse } from '../../types/auth';

type AuthLike = Record<string, unknown> | null | undefined;

function pickString(o: AuthLike, ...keys: string[]): string {
  if (!o) return '';
  for (const key of keys) {
    const v = o[key];
    if (typeof v === 'string' && v.trim()) return v.trim();
  }
  return '';
}

function pickStringList(o: AuthLike, ...keys: string[]): string[] {
  if (!o) return [];
  for (const key of keys) {
    const v = o[key];
    if (Array.isArray(v)) {
      return v.filter((item): item is string => typeof item === 'string');
    }
  }
  return [];
}

/** Normaliza envelope Auth (camelCase / PascalCase) a contrato frontend. */
export function normalizeAuthResponse(raw: AuthLike): AuthResponse {
  const token = pickString(raw, 'token', 'Token');
  const subscriberId = pickString(raw, 'subscriberId', 'SubscriberId');
  const userId = pickString(raw, 'userId', 'UserId');

  return {
    userId,
    fullName: pickString(raw, 'fullName', 'FullName'),
    email: pickString(raw, 'email', 'Email'),
    role: pickString(raw, 'role', 'Role'),
    subscriberId,
    userType: pickString(raw, 'userType', 'UserType') || null,
    platformRole: pickString(raw, 'platformRole', 'PlatformRole') || null,
    companyId: pickString(raw, 'companyId', 'CompanyId') || null,
    token,
    planCode: pickString(raw, 'planCode', 'PlanCode') || null,
    enabledModules: pickStringList(raw, 'enabledModules', 'EnabledModules'),
    refreshToken: pickString(raw, 'refreshToken', 'RefreshToken') || null,
    refreshTokenExpiry: pickString(raw, 'refreshTokenExpiry', 'RefreshTokenExpiry') || null,
  };
}
