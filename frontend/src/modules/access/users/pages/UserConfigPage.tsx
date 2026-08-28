import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  useNavigate,
  useParams,
  useSearchParams,
  useLocation,
} from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage, LoadingState } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHFormActions, ZHLinkButton } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import { applyServerErrors } from "../../../lib/validationErrors";
import {
  membershipService,
  MEMBERSHIP_ROLES,
  type CompanyUserMembershipAdminDto,
  type MembershipRole,
} from "../api/membershipService";
import { branchAssignmentService } from "../api/branchAssignmentService";
import { companyUserPreferencesService } from "../../api/companyUserPreferencesService";
import { profileService, type Profile } from "../../api/profileService";
import {
  branchLookupFacade,
  type BranchListItemDto,
} from "../../../branches/facades/branchLookupFacade";
import { userConfigTabs, type UserConfigTabId } from "../config/userConfigTabs";
import {
  userConfigSchema,
  type UserConfigFormValues,
} from "../../../../schemas/access/userConfigSchema";
import "../../../../styles/shared/items-catalog.css";
import "../pages/UsersPage.css";

const PERMISSION = "access.company_user_memberships.view";
const CREATE_USER_PERMISSION = "access.identity_users.create";

const VALID_TAB_IDS = userConfigTabs.map((tab) => tab.id);
const DEFAULT_TAB: UserConfigTabId = "general";

const EMPTY_FORM_VALUES: UserConfigFormValues = {
  stage: "pending",
  username: "",
  firstName: "",
  lastName: "",
  email: "",
  password: "",
  role: "User",
  profileId: null,
  authorizedBranchIds: [],
  loginMode: "AskBranch",
  defaultBranchId: null,
};

function parseTab(raw: string | null): UserConfigTabId {
  if (raw && (VALID_TAB_IDS as string[]).includes(raw))
    return raw as UserConfigTabId;
  return DEFAULT_TAB;
}

/** Backend persiste Role como string libre (SecurityRoles) — defensivo ante valores inesperados. */
function asMembershipRole(role: string): MembershipRole {
  return (MEMBERSHIP_ROLES as readonly string[]).includes(role)
    ? (role as MembershipRole)
    : "User";
}

type BlockErrors = { general: string; branches: string; preferences: string };
const NO_BLOCK_ERRORS: BlockErrors = {
  general: "",
  branches: "",
  preferences: "",
};

/**
 * Pantalla única de configuración de usuario (Fase 1 + pulido UX Fase 2). Un único formulario
 * (`userConfigSchema`) cubre identidad/rol/perfil + sucursales autorizadas + preferencias de
 * ingreso, con un único botón "Guardar configuración" — nunca un submit por sección. Sirve tanto
 * /access/users/new como /access/users/:userId (userId = companyUserId).
 */
export function UserConfigPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const { userId } = useParams<{ userId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((s) => s.user);
  const { canShow, isAdminRole } = usePermissionsUi();
  const canManage = canShow(PERMISSION);
  const canCreateUser = canShow(CREATE_USER_PERMISSION);

  const mode: "new" | "edit" = userId ? "edit" : "new";
  const returnTo =
    (location.state as { from?: string } | undefined)?.from ?? "/access/users";

  const [membership, setMembership] =
    useState<CompanyUserMembershipAdminDto | null>(null);
  const [loading, setLoading] = useState(mode === "edit");
  const [loadError, setLoadError] = useState("");
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [branchesCatalog, setBranchesCatalog] = useState<BranchListItemDto[]>(
    [],
  );
  const [saving, setSaving] = useState(false);
  const [blockErrors, setBlockErrors] = useState<BlockErrors>(NO_BLOCK_ERRORS);

  const {
    register,
    control,
    handleSubmit,
    reset,
    setValue,
    getValues,
    setError,
    watch,
    formState: { errors, isDirty },
  } = useForm<UserConfigFormValues>({
    resolver: zodResolver(userConfigSchema),
    defaultValues: EMPTY_FORM_VALUES,
  });
  const stage = watch("stage");

  const activeTabId = parseTab(searchParams.get("tab"));
  const setTab = (id: UserConfigTabId) => setSearchParams({ tab: id });

  useEffect(() => {
    let cancelled = false;

    if (mode === "new") {
      Promise.all([profileService.list(true), branchLookupFacade.list("active")])
        .then(([profilesList, branchesList]) => {
          if (cancelled) return;
          setProfiles(profilesList);
          setBranchesCatalog(branchesList);
        })
        .catch(() => {
          if (!cancelled) {
            setProfiles([]);
            setBranchesCatalog([]);
          }
        });
      return () => {
        cancelled = true;
      };
    }

    if (!userId) return;
    setLoading(true);
    setLoadError("");
    Promise.all([
      membershipService.list(false),
      profileService.list(true),
      branchLookupFacade.list("active"),
      branchAssignmentService.getMembershipBranches(userId),
      companyUserPreferencesService.get(userId),
    ])
      .then(([rows, profilesList, branchesList, branchAuth, prefs]) => {
        if (cancelled) return;
        const found = rows.find((r) => r.companyUserId === userId);
        if (!found) {
          setLoadError(
            t("users.config.notFound", "No se encontró el usuario solicitado."),
          );
          return;
        }
        setMembership(found);
        setProfiles(profilesList);
        setBranchesCatalog(branchesList);
        reset({
          ...EMPTY_FORM_VALUES,
          stage: "edit",
          username: found.username,
          role: asMembershipRole(found.role),
          profileId: found.profileId,
          authorizedBranchIds: branchAuth.branches
            .filter((b) => b.authorized)
            .map((b) => b.branchId),
          loginMode: prefs?.loginMode ?? "AskBranch",
          defaultBranchId: prefs?.defaultBranchId ?? null,
        });
      })
      .catch((err) => {
        if (!cancelled) {
          setLoadError(
            formatApiRequestError(err, {
              generic: t(
                "users.error.load",
                "No se pudo cargar la lista de usuarios.",
              ),
            }),
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // t() se usa dentro del efecto pero no debe disparar un refetch — su identidad cambia en
    // cada render y duplicaría las llamadas HTTP (auditado en Fase 2 UAT).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, userId, reset]);

  const handleCancel = () => {
    if (!isDirty) {
      navigate(returnTo);
      return;
    }
    void (async () => {
      const confirmed = await message.confirm({
        title: t("users.config.confirmDiscardTitle", "¿Descartar cambios?"),
        message: t("common.unsavedChanges", "Tienes cambios sin guardar."),
        variant: "warning",
        confirmLabel: t("common.discard", "Descartar"),
        cancelLabel: t("common.cancel"),
      });
      if (confirmed) navigate(returnTo);
    })();
  };

  const onSubmit = handleSubmit(async (values) => {
    if (saving) return;

    if (mode === "new") {
      const profileName =
        profiles.find((p) => p.id === values.profileId)?.name ?? null;
      const confirmed = await message.confirm({
        title: t("users.config.confirmCreateTitle", "Crear usuario"),
        message: (
          <>
            <p className="zh-confirm-message">
              Vas a crear el acceso de <strong>{values.username}</strong> a esta empresa con rol{" "}
              <strong>{values.role}</strong>
              {profileName ? (
                <>
                  {" "}
                  y perfil <strong>{profileName}</strong>
                </>
              ) : null}
              .
            </p>
            <p className="zh-confirm-message">
              Sucursales autorizadas: <strong>{values.authorizedBranchIds.length}</strong>.
            </p>
          </>
        ),
        variant: "warning",
        confirmLabel: t("users.config.confirmCreateAction", "Crear usuario"),
        cancelLabel: t("common.cancel"),
      });
      if (!confirmed) return;
    }

    setSaving(true);
    setBlockErrors(NO_BLOCK_ERRORS);

    try {
      if (values.stage === "create") {
        await membershipService.createSystemUser({
          username: values.username,
          firstName: values.firstName,
          lastName: values.lastName,
          email: values.email || null,
          password: values.password,
          role: values.role,
          profileId: values.profileId || null,
        });
      } else {
        await membershipService.upsertMembership({
          username: values.username,
          role: values.role,
          profileId: values.profileId || null,
        });
      }
    } catch (err) {
      const applied = applyServerErrors(err, setError, (msg) =>
        setBlockErrors((prev) => ({ ...prev, general: msg })),
      );
      if (!applied) {
        setBlockErrors((prev) => ({
          ...prev,
          general: formatApiRequestError(err, {
            generic: t(
              "users.membership.error",
              "No se pudo guardar el usuario.",
            ),
          }),
        }));
      }
      setSaving(false);
      return;
    }

    // No existe membershipService.getById — se resuelve el companyUserId buscando por username
    // en la misma lista que ya consume UsersPage, igual que en Fase 1.
    let resolvedCompanyUserId = membership?.companyUserId ?? null;
    try {
      const rows = await membershipService.list(false);
      const found = rows.find((r) => r.username === values.username);
      if (found) resolvedCompanyUserId = found.companyUserId;
    } catch {
      // best-effort: si esto falla no podemos guardar sucursales/preferencias en esta pasada.
    }

    let hadBlockError = false;
    if (resolvedCompanyUserId) {
      try {
        await branchAssignmentService.updateMembershipBranches(
          resolvedCompanyUserId,
          values.authorizedBranchIds,
        );
      } catch (err) {
        hadBlockError = true;
        setBlockErrors((prev) => ({
          ...prev,
          branches: formatApiRequestError(err, {
            generic: t(
              "users.branches.error.save",
              "No se pudieron guardar las sucursales.",
            ),
          }),
        }));
      }
      try {
        await companyUserPreferencesService.update(resolvedCompanyUserId, {
          loginMode: values.loginMode,
          defaultBranchId: values.defaultBranchId || null,
        });
      } catch (err) {
        hadBlockError = true;
        setBlockErrors((prev) => ({
          ...prev,
          preferences: formatApiRequestError(err, {
            generic: t(
              "security.preferences.error.save",
              "No se pudieron guardar las preferencias.",
            ),
          }),
        }));
      }
    }

    setSaving(false);

    if (hadBlockError) {
      message.error(
        t(
          "users.config.partialSaveError",
          "El usuario se guardó, pero algunas secciones no se pudieron actualizar. Revísalas antes de continuar.",
        ),
      );
      if (mode === "new" && resolvedCompanyUserId) {
        navigate(`/access/users/${resolvedCompanyUserId}`, {
          replace: true,
          state: { from: returnTo },
        });
      }
      return;
    }

    message.success(
      t("users.config.saveSuccess", "Usuario configurado correctamente."),
    );
    navigate(returnTo);
  });

  if (!user || (!isAdminRole && !canManage)) {
    return (
      <NoAccessPage
        title={t("users.config.title", "Configuración de usuario")}
      />
    );
  }

  if (loading) {
    return (
      <ErpPageTemplate
        kicker={t("app.nav.group.admin")}
        title={t("common.loading")}
      >
        <LoadingState />
      </ErpPageTemplate>
    );
  }

  if (loadError) {
    return (
      <ErpPageTemplate
        kicker={t("app.nav.group.admin")}
        title={t("users.config.title", "Configuración de usuario")}
      >
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={loadError}
        />
        <ZHLinkButton to="/access/users" variant="ghost">
          ← {t("users.config.backToList", "Volver a Usuarios")}
        </ZHLinkButton>
      </ErpPageTemplate>
    );
  }

  const visibleTabs = userConfigTabs.filter(
    (tab) => !tab.requiresExistingUser || membership,
  );
  const activeTab =
    visibleTabs.find((tab) => tab.id === activeTabId) ?? visibleTabs[0];

  const context = {
    mode,
    membership,
    profiles,
    branchesCatalog,
    canManage,
    canCreateUser,
    saving,
    register,
    control,
    setValue,
    getValues,
    errors,
    blockErrors,
  };

  const saveDisabled =
    saving || !canManage || (mode === "new" && stage === "pending");

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.configuracion")}
      title={
        mode === "new"
          ? t("users.addUser.title", "Agregar usuario")
          : (membership?.fullName ??
            t("users.config.title", "Configuración de usuario"))
      }
      subtitle={mode === "edit" ? membership?.username : undefined}
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit();
        }}
        noValidate
      >
        <div className="prd-tabs" role="tablist">
          {visibleTabs.map((tab) => (
            <button
              key={tab.id}
              id={`user-config-tab-${tab.id}`}
              type="button"
              role="tab"
              aria-selected={activeTab.id === tab.id}
              aria-controls={`user-config-panel-${tab.id}`}
              className={`prd-tab-btn ${activeTab.id === tab.id ? "prd-tab-btn--active" : ""}`}
              onClick={() => setTab(tab.id)}
            >
              {t(tab.labelKey)}
            </button>
          ))}
        </div>

        <div
          className="pg-section"
          role="tabpanel"
          id={`user-config-panel-${activeTab.id}`}
          aria-labelledby={`user-config-tab-${activeTab.id}`}
        >
          <div className="pg-section-body">{activeTab.render(context)}</div>
        </div>

        {canManage ? (
          <ZHFormActions
            onCancel={handleCancel}
            hideDraft
            saveButtonType="submit"
            disableSave={saveDisabled}
            labels={{
              cancel: t("common.cancel"),
              save: saving
                ? t("common.saving")
                : t("users.config.save", "Guardar configuración"),
            }}
          />
        ) : null}
      </form>
    </ErpPageTemplate>
  );
}
