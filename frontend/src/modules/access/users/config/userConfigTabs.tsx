import type { ReactNode } from "react";
import type {
  Control,
  FieldErrors,
  UseFormGetValues,
  UseFormRegister,
  UseFormSetValue,
} from "react-hook-form";
import { UserGeneralSection } from "../sections/UserGeneralSection";
import { UserBranchesSection } from "../sections/UserBranchesSection";
import { UserPreferencesSection } from "../sections/UserPreferencesSection";
import { UserSecuritySection } from "../sections/UserSecuritySection";
import { type CompanyUserMembershipAdminDto } from "../api/membershipService";
import { type Profile } from "../../api/profileService";
import { type BranchListItemDto } from "../../../branches/api/branchService";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";

export type UserConfigTabId =
  "general" | "branches" | "preferences" | "security";

export type UserConfigContext = {
  mode: "new" | "edit";
  membership: CompanyUserMembershipAdminDto | null;
  profiles: Profile[];
  branchesCatalog: BranchListItemDto[];
  canManage: boolean;
  canCreateUser: boolean;
  saving: boolean;
  register: UseFormRegister<UserConfigFormValues>;
  control: Control<UserConfigFormValues>;
  setValue: UseFormSetValue<UserConfigFormValues>;
  getValues: UseFormGetValues<UserConfigFormValues>;
  errors: FieldErrors<UserConfigFormValues>;
  /** Error de guardado específico de este bloque (General/Sucursales/Preferencias) — nunca un error genérico tapando toda la pantalla. */
  blockErrors: { general: string; branches: string; preferences: string };
};

export type UserConfigTab = {
  id: UserConfigTabId;
  labelKey: string;
  /** Seguridad requiere una membership ya existente — no hay nada que resumir antes de la primera alta. */
  requiresExistingUser: boolean;
  render: (ctx: UserConfigContext) => ReactNode;
};

/**
 * Secciones de la pantalla única de configuración de usuario (/access/users/new,
 * /access/users/:userId). Todas comparten un único useForm (UserConfigPage) — agregar una
 * sección futura (Permisos por usuario, Notificaciones, Auditoría) es un elemento nuevo en este
 * array + su sections/UserXxxSection.tsx, nunca requiere modificar UserConfigPage.tsx. Hoy solo
 * se incluyen secciones con datos reales.
 */
export const userConfigTabs: UserConfigTab[] = [
  {
    id: "general",
    labelKey: "users.config.tabs.general",
    requiresExistingUser: false,
    render: (ctx) => (
      <UserGeneralSection
        mode={ctx.mode}
        register={ctx.register}
        setValue={ctx.setValue}
        errors={ctx.errors}
        profiles={ctx.profiles}
        canManage={ctx.canManage}
        canCreateUser={ctx.canCreateUser}
        disabled={ctx.saving}
        blockError={ctx.blockErrors.general}
      />
    ),
  },
  {
    id: "branches",
    labelKey: "users.config.tabs.branches",
    requiresExistingUser: false,
    render: (ctx) => (
      <UserBranchesSection
        branchesCatalog={ctx.branchesCatalog}
        control={ctx.control}
        canManage={ctx.canManage}
        disabled={ctx.saving}
        blockError={ctx.blockErrors.branches}
      />
    ),
  },
  {
    id: "preferences",
    labelKey: "users.config.tabs.preferences",
    requiresExistingUser: false,
    render: (ctx) => (
      <UserPreferencesSection
        control={ctx.control}
        register={ctx.register}
        setValue={ctx.setValue}
        getValues={ctx.getValues}
        errors={ctx.errors}
        branchesCatalog={ctx.branchesCatalog}
        canManage={ctx.canManage}
        disabled={ctx.saving}
        blockError={ctx.blockErrors.preferences}
      />
    ),
  },
  {
    id: "security",
    labelKey: "users.config.tabs.security",
    requiresExistingUser: true,
    render: (ctx) =>
      ctx.membership ? (
        <UserSecuritySection membership={ctx.membership} />
      ) : null,
  },
];
