import type { AuthResponse } from "../../types/auth";

type AuthLike = Record<string, unknown> | null | undefined;

function pickString(o: AuthLike, ...keys: string[]): string {
  if (!o) return "";
  for (const key of keys) {
    const v = o[key];
    if (typeof v === "string" && v.trim()) return v.trim();
  }
  return "";
}

function pickBoolean(o: AuthLike, ...keys: string[]): boolean {
  if (!o) return false;
  for (const key of keys) {
    const v = o[key];
    if (typeof v === "boolean") return v;
  }
  return false;
}

function pickNumber(o: AuthLike, ...keys: string[]): number | null {
  if (!o) return null;
  for (const key of keys) {
    const v = o[key];
    if (typeof v === "number" && Number.isFinite(v)) return v;
  }
  return null;
}

/** Normaliza envelope Auth (camelCase / PascalCase) a contrato frontend. */
export function normalizeAuthResponse(raw: AuthLike): AuthResponse {
  const token = pickString(raw, "token", "Token");
  const tenantId = pickString(raw, "tenantId", "TenantId");
  const userId = pickString(raw, "userId", "UserId");

  return {
    userId,
    fullName: pickString(raw, "fullName", "FullName"),
    username: pickString(raw, "username", "Username"),
    email: pickString(raw, "email", "Email") || null,
    role: pickString(raw, "role", "Role"),
    tenantId,
    companyId: pickString(raw, "companyId", "CompanyId") || null,
    requiresCompanySelection: pickBoolean(
      raw,
      "requiresCompanySelection",
      "RequiresCompanySelection",
    ),
    token,
    refreshToken: pickString(raw, "refreshToken", "RefreshToken") || null,
    refreshTokenExpiry:
      pickString(raw, "refreshTokenExpiry", "RefreshTokenExpiry") || null,
    requiresPasswordReset: pickBoolean(
      raw,
      "requiresPasswordReset",
      "RequiresPasswordReset",
    ),
    passwordResetToken:
      pickString(raw, "passwordResetToken", "PasswordResetToken") || null,
    passwordResetTokenExpiresIn: pickNumber(
      raw,
      "passwordResetTokenExpiresIn",
      "PasswordResetTokenExpiresIn",
    ),
  };
}
