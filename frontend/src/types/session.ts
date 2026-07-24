import type { CompanyLogo } from './companyProfile';

export interface SessionContextDto {
  identity: {
    userId: string;
    fullName: string;
    email: string;
  };
  tenant: {
    id: string;
    displayName: string;
    logo: CompanyLogo | null;
  };
  authorization: {
    roles: string[];
    permissions: string[];
  };
  preferences: {
    language: string;
  };
  /** Sucursal activa inicial (Fase I-1/I-2). Null si no se pudo resolver una única sucursal. */
  branch: SessionBranchDto | null;
}

export interface SessionBranchDto {
  id: string;
  name: string;
  isMainBranch: boolean;
}

/**
 * Sucursales autorizadas del usuario actual en la empresa activa + preferencia de arranque
 * (CompanyUserPreferences) — consumido por el selector post-login cuando el bootstrap no
 * pudo resolver una sucursal única (GET /api/v1/session/available-branches).
 */
export interface MyAvailableBranchesDto {
  branches: SessionBranchDto[];
  loginMode: 'AskBranch' | 'DirectToDefault';
  defaultBranchId: string | null;
}
