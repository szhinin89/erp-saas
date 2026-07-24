import { apiGet, apiPost } from '../../../lib/apiEnvelope';

/** Proyección administrativa de CompanyUserMembership — GET /api/v1/admin/iam/memberships (Fase I-C). */
export type CompanyUserMembershipAdminDto = {
  companyUserId: string;
  identityUserId: string;
  username: string;
  fullName: string;
  /** Dato de contacto opcional — ya no es la identidad del usuario (Fase G). */
  email: string | null;
  role: string;
  isActive: boolean;
  profileId: string | null;
  profileName: string | null;
};

/**
 * Roles del backend (ERP.Domain.Kernel.Security.SecurityRoles) — solo dos valores existen, sin
 * catálogo dinámico detrás (mismo criterio que COMPANY_USER_LOGIN_MODES).
 */
export const MEMBERSHIP_ROLES = ['Admin', 'User'] as const;
export type MembershipRole = (typeof MEMBERSHIP_ROLES)[number];

export type UpsertMembershipPayload = {
  username: string;
  role: string;
  profileId?: string | null;
};

export type CreateSystemUserPayload = {
  username: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  password: string;
  role: string;
  profileId?: string | null;
};

export type CreateSystemUserResultDto = {
  identityUserId: string;
  username: string;
};

export type UsernameMembershipLookupDto = {
  companyUserMembershipId: string;
  isActive: boolean;
  role: string;
  profileId: string | null;
};

export type UsernameLookupDto = {
  identityUserExists: boolean;
  fullName: string | null;
  membership: UsernameMembershipLookupDto | null;
};

export const membershipService = {
  list: (onlyActive = false) =>
    apiGet<CompanyUserMembershipAdminDto[]>('/api/v1/admin/iam/memberships', {
      params: { onlyActive },
    }),

  /**
   * Alta o edición de membership (rol/perfil). No crea IdentityUser — el username debe pertenecer
   * a un usuario ya existente en la plataforma; si no existe, el backend responde con un error
   * controlado ("Usuario no existe."). Reactiva automáticamente una membership inactiva (mismo
   * comportamiento que UpsertCompanyUserMembershipHandler.Activate en el backend).
   */
  upsertMembership: (payload: UpsertMembershipPayload) =>
    apiPost<object>('/api/v1/admin/iam/memberships', payload),

  /** Revoca (desactiva) la membership del usuario en la empresa activa. Idempotente. */
  revokeMembership: (username: string) =>
    apiPost<object>('/api/v1/admin/iam/memberships/revoke', { username }),

  /**
   * Único punto de creación de un IdentityUser nuevo fuera de First Run. Crea el usuario y, en el
   * mismo flujo del backend, su membership en la empresa activa (rol + perfil) — nunca reimplementa
   * esa lógica aquí. Requiere permiso propio access.identity_users.create.
   */
  createSystemUser: (payload: CreateSystemUserPayload) =>
    apiPost<CreateSystemUserResultDto>('/api/v1/admin/iam/users', payload),

  /**
   * Fase F (username en Fase G) — único punto de entrada de "Agregar usuario": resuelve si el
   * username ya es un IdentityUser (y si ya tiene membership en la empresa activa) para que el
   * wizard decida automáticamente entre upsertMembership y createSystemUser, sin que el admin
   * tenga que saberlo.
   */
  lookupUserByUsername: (username: string) =>
    apiGet<UsernameLookupDto>('/api/v1/admin/iam/memberships/lookup', { params: { username } }),

  /**
   * Asigna una contraseña temporal a un usuario existente de la empresa activa. El backend hace
   * hash, fuerza RequirePasswordReset, revoca refresh tokens y cierra sesiones activas — el
   * frontend solo consume el endpoint. Requiere permiso access.identity_users.assign_temporary_password.
   */
  assignTemporaryPassword: (username: string, temporaryPassword: string) =>
    apiPost<string>(
      `/api/v1/admin/iam/users/${encodeURIComponent(username)}/assign-temporary-password`,
      { temporaryPassword },
    ),
};
