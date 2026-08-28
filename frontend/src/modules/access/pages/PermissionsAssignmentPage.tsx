import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { profileService, type Profile } from "../api/profileService";
import { ZHField, ZHFormSection, ZHToggle, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { NoAccessPage } from "../../../components/PageShell";
import { ZhSelect } from "../../../components/zh/inputs";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { formatApiRequestError } from "../../lib/apiError";
import "./ProfilesPage.css";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";

/**
 * ADMINISTRATION-CLEAN-ACCESS-01: pantalla propia de responsabilidad única — asigna permisos a un
 * perfil. Extraída de la sección "Permissions" que vivía embebida en el mismo formulario/modal de
 * ProfilesPage.tsx (mezcla crítica). No crea usuarios ni perfiles: reutiliza los mismos endpoints
 * ya existentes de AccessProfilesController (GET/PUT .../profiles/{id}/permissions) que ya usaba
 * ProfilesPage — sin cambios de backend/permisos.
 *
 * No existe el concepto de override de permisos por usuario en el dominio (solo perfil→permiso),
 * así que esta pantalla asigna permisos únicamente por perfil.
 */

/* ── Permission groups mapped to CRUD columns ───────────────── */
type PermGroup = {
  module: string;
  planModule: string;
  view: string[];
  create: string[];
  edit: string[];
  delete: string[];
};

const MODULE_PERM_GROUPS: PermGroup[] = [
  {
    module: "Clientes / Proveedores",
    planModule: "sales",
    view: ["masterdata.businesspartners.view"],
    create: ["masterdata.businesspartners.create"],
    edit: ["masterdata.businesspartners.update"],
    delete: ["masterdata.businesspartners.disable"],
  },
  {
    module: "Inventario",
    planModule: "inventory",
    view: ["inventory.Items.view", "inventory.warehouses.view"],
    create: ["inventory.Items.create", "inventory.warehouses.create"],
    edit: ["inventory.Items.update", "inventory.warehouses.update"],
    delete: ["inventory.Items.delete"],
  },
  {
    module: "Configuración",
    planModule: "access",
    view: [
      "settings.branches.view",
      "settings.company.view",
      "settings.geography.view",
    ],
    create: ["settings.branches.create"],
    edit: ["settings.branches.update"],
    delete: ["settings.branches.delete"],
  },
  {
    module: "Facturación Electrónica",
    planModule: "access",
    view: ["electronic-invoicing.view"],
    create: [],
    edit: ["electronic-invoicing.configure"],
    delete: [],
  },
  {
    module: "Administración",
    planModule: "access",
    view: [
      "access.profiles.view",
      "access.company_user_memberships.view",
      "admin.activity.view",
    ],
    create: [],
    edit: [],
    delete: [],
  },
  {
    module: "RIDE (Ventas)",
    planModule: "sales",
    view: ["ride.view"],
    create: [],
    edit: ["ride.regenerate"],
    delete: [],
  },
];

/* ── Helpers ────────────────────────────────────────────────── */
function allOn(keys: string[], state: Record<string, boolean>): boolean {
  return keys.length > 0 && keys.every((k) => !!state[k]);
}

function setKeys(
  keys: string[],
  value: boolean,
  state: Record<string, boolean>,
): Record<string, boolean> {
  const next = { ...state };
  for (const k of keys) next[k] = value;
  return next;
}

function countModulesWithAnyPerm(state: Record<string, boolean>): number {
  return MODULE_PERM_GROUPS.filter((g) =>
    [...g.view, ...g.create, ...g.edit, ...g.delete].some((k) => !!state[k]),
  ).length;
}

function buildEmptyPermState(): Record<string, boolean> {
  const s: Record<string, boolean> = {};
  for (const g of MODULE_PERM_GROUPS) {
    for (const k of [...g.view, ...g.create, ...g.edit, ...g.delete])
      s[k] = false;
  }
  return s;
}

/* ── Column toggle with cascade ─────────────────────────────── */
function handleColumnToggle(
  group: PermGroup,
  col: "view" | "create" | "edit" | "delete",
  checked: boolean,
  state: Record<string, boolean>,
): Record<string, boolean> {
  let next = setKeys(group[col], checked, state);
  if (checked) {
    // cascading: any action requires view
    if (col !== "view") next = setKeys(group.view, true, next);
    // delete also requires edit
    if (col === "delete") next = setKeys(group.edit, true, next);
  }
  return next;
}

/* ── Main component ─────────────────────────────────────────── */
export function PermissionsAssignmentPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const { canShow, isAdminRole } = usePermissionsUi();
  const canManage = canShow("access.profiles.view");

  const [searchParams, setSearchParams] = useSearchParams();
  const selectedProfileId = searchParams.get("profileId") ?? "";

  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState("");

  const [permState, setPermState] =
    useState<Record<string, boolean>>(buildEmptyPermState);
  const [permLoading, setPermLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [rejectedPerms, setRejectedPerms] = useState<
    { permissionKey: string; reason: string }[]
  >([]);

  /* ── Load profile list ─────────────────────────────────────── */
  const loadProfiles = useCallback(async () => {
    setListLoading(true);
    setListError("");
    try {
      setProfiles((await profileService.list(false)) ?? []);
    } catch (err) {
      setListError(
        formatApiRequestError(err, {
          generic: t("permissionsAssignment.error.loadProfiles"),
        }),
      );
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadProfiles();
  }, [loadProfiles]);

  /* ── Load permissions for the selected profile ─────────────── */
  const loadPermissions = useCallback(async (profileId: string) => {
    setPermLoading(true);
    setSaveError("");
    setRejectedPerms([]);
    try {
      const res = await profileService.getPermissions(profileId);
      const next = buildEmptyPermState();
      for (const item of res?.items ?? []) {
        if (item.permissionKey in next)
          next[item.permissionKey] = !!item.isAllowed;
      }
      setPermState(next);
    } catch {
      setPermState(buildEmptyPermState());
    } finally {
      setPermLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedProfileId) void loadPermissions(selectedProfileId);
    else setPermState(buildEmptyPermState());
  }, [selectedProfileId, loadPermissions]);

  const selectedProfile = useMemo(
    () => profiles.find((p) => p.id === selectedProfileId) ?? null,
    [profiles, selectedProfileId],
  );

  const onSelectProfile = (profileId: string) => {
    if (profileId) setSearchParams({ profileId });
    else setSearchParams({});
  };

  /* ── Save ────────────────────────────────────────────────────── */
  const onSave = async () => {
    if (!selectedProfileId) return;
    setSaving(true);
    setSaveError("");
    try {
      const permItems = Object.entries(permState).map(
        ([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }),
      );
      const upsertResult = await profileService.upsertPermissions(
        selectedProfileId,
        permItems,
      );
      const planRejections = (upsertResult?.rejected ?? []).filter(
        (r) => r.rejectionCode === "blocked_by_plan",
      );
      setRejectedPerms(planRejections);
    } catch (err) {
      setSaveError(
        formatApiRequestError(err, { generic: t("profiles.perms.error.save") }),
      );
    } finally {
      setSaving(false);
    }
  };

  const modulesWithPerms = countModulesWithAnyPerm(permState);
  const progressPct = Math.round(
    (modulesWithPerms / MODULE_PERM_GROUPS.length) * 100,
  );
  const modulesWithoutPerms = MODULE_PERM_GROUPS.length - modulesWithPerms;

  if (!user || (!isAdminRole && !canManage)) {
    return <NoAccessPage title={t("permissionsAssignment.title")} />;
  }

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("permissionsAssignment.title")}
      subtitle={t("permissionsAssignment.subtitle")}
    >
      {listError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={listError}
        />
      ) : null}

      <div className="pg-section prf-modal-section-flush">
        <div className="pa-profile-select-wrap">
          <ZHField label={t("permissionsAssignment.selectProfile")}>
            <ZhSelect
              className="zh-input"
              value={selectedProfileId}
              disabled={listLoading}
              onChange={(e) => onSelectProfile(e.target.value)}
            >
              <option value="">
                {t("permissionsAssignment.selectProfilePlaceholder")}
              </option>
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
      </div>

      {!selectedProfile ? (
        <p className="subtle pg-state-pad">
          {t("permissionsAssignment.noProfileSelected")}
        </p>
      ) : (
        <>
          <div className="pg-section prf-modal-section-flush">
            <div className="pg-section-header">
              <span className="material-symbols-outlined prf-modal-section-icon">
                rule
              </span>
              {t("profiles.perms.title")}
            </div>

            {permLoading ? (
              <p className="subtle prf-modal-loading">{t("common.loading")}</p>
            ) : (
              MODULE_PERM_GROUPS.map((group) => (
                <ZHFormSection key={group.module} title={group.module}>
                  <div className="zh-stack zh-gap-8">
                    {(["view", "create", "edit", "delete"] as const).map(
                      (col) =>
                        group[col].length > 0 && (
                          <ZHToggle
                            key={col}
                            label={t(`profiles.perms.${col}`)}
                            description={t(`profiles.perms.${col}.desc`)}
                            value={allOn(group[col], permState)}
                            onChange={(checked) =>
                              setPermState((s) =>
                                handleColumnToggle(group, col, checked, s),
                              )
                            }
                          />
                        ),
                    )}
                  </div>
                </ZHFormSection>
              ))
            )}

            <div className="prf-modal-status-row pa-status-row">
              {modulesWithoutPerms > 0 ? (
                <>
                  <span className="material-symbols-outlined prf-modal-status-row-icon">
                    pending_actions
                  </span>
                  <p className="subtle prf-modal-status-row-text">
                    {t("profiles.form.missingModules", {
                      count: modulesWithoutPerms,
                    })}
                    {" "}
                    ({progressPct}%)
                  </p>
                </>
              ) : (
                <>
                  <span className="material-symbols-outlined prf-modal-status-row-icon prf-modal-status-row-icon--success">
                    check_circle
                  </span>
                  <p className="subtle prf-modal-status-row-text">
                    {t("profiles.form.allModulesSet")}
                  </p>
                </>
              )}
            </div>
          </div>

          {rejectedPerms.length > 0 && (
            <ZHPageNotice
              variant="warning"
              message={`${rejectedPerms.length} permiso(s) no guardados -- fuera del plan`}
              detail={rejectedPerms
                .map((r) => `${r.permissionKey}: ${r.reason}`)
                .join("\n")}
            />
          )}

          {saveError && (
            <ZHPageNotice
              variant="error"
              message={t("common.errorPrefix")}
              detail={saveError}
            />
          )}

          <ZHFormActions
            hideDraft
            hideCancel
            disableSave={saving || permLoading}
            onSave={() => void onSave()}
            labels={{
              cancel: t("common.cancel"),
              save: saving
                ? t("common.saving")
                : t("permissionsAssignment.saveAction"),
            }}
          />
        </>
      )}
    </ErpPageTemplate>
  );
}
