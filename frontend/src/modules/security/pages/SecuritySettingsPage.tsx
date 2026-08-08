import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  EmptyState,
  LoadingState,
  PageShell,
  NoAccessPage,
} from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import {
  ZHToggle,
  ZHField,
  ZHBtn,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { useAuthStore } from "../../../store/authStore";
import {
  securityService,
  type SecurityAdminMatrix,
  type SecurityUser,
} from "../api/securityService";
import {
  companyUserPreferencesFacade,
  COMPANY_USER_LOGIN_MODES,
} from "../../access/facades/companyUserPreferencesFacade";
import {
  branchLookupFacade,
  type BranchListItemDto,
} from "../../branches/facades/branchLookupFacade";
import {
  companyUserPreferencesSchema,
  type CompanyUserPreferencesFormValues,
} from "../../../schemas/access/companyUserPreferencesSchema";
import { isAdminRole } from "../../../access/permissionUi";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatApiError } from "../../lib/formatApiError";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import "./SecuritySettingsPage.css";

type ScopeKey =
  "manageRoles" | "manageModules" | "manageScreens" | "manageProcesses";

const scopeMap: Record<ScopeKey, number> = {
  manageRoles: 1,
  manageModules: 2,
  manageScreens: 3,
  manageProcesses: 4,
};

const scopeColumns: Array<{ key: ScopeKey; labelKey: string }> = [
  { key: "manageRoles", labelKey: "security.scopes.manageRoles" },
  { key: "manageModules", labelKey: "security.scopes.manageModules" },
  { key: "manageScreens", labelKey: "security.scopes.manageScreens" },
  { key: "manageProcesses", labelKey: "security.scopes.manageProcesses" },
];

function userAllowedScopes(
  matrix: SecurityAdminMatrix,
  user: SecurityUser,
): Set<number> {
  const allowed = new Set<number>();
  for (const a of matrix.assignments) {
    if (a.subjectType !== "User") continue;
    if (a.subjectKey !== user.id) continue;
    if (a.isAllowed) allowed.add(a.scope);
  }
  return allowed;
}

export function SecuritySettingsPage() {
  const { user } = useAuthStore();
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const [matrix, setMatrix] = useState<SecurityAdminMatrix | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [error, setError] = useState("");

  const isAdminUser = isAdminRole(user?.role);
  const canManagePreferences = canShow("access.company_user_memberships.view");

  /* ── Fase G: modal de preferencias (sucursal por defecto + modo de login) ────── */
  const [prefsTarget, setPrefsTarget] = useState<SecurityUser | null>(null);
  const [prefsModalOpen, setPrefsModalOpen] = useState(false);
  const [prefsLoading, setPrefsLoading] = useState(false);
  const [prefsSaving, setPrefsSaving] = useState(false);
  const [prefsSaveError, setPrefsSaveError] = useState("");
  const [branches, setBranches] = useState<BranchListItemDto[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(false);

  const {
    register: registerPrefs,
    handleSubmit: handlePrefsSubmit,
    reset: resetPrefs,
    watch: watchPrefs,
    setError: setPrefsFieldError,
    formState: { errors: prefsErrors },
  } = useForm<CompanyUserPreferencesFormValues>({
    resolver: zodResolver(companyUserPreferencesSchema),
    defaultValues: { loginMode: "AskBranch", defaultBranchId: null },
  });
  const prefsLoginMode = watchPrefs("loginMode");

  const openPreferences = async (target: SecurityUser) => {
    setPrefsTarget(target);
    setPrefsModalOpen(true);
    setPrefsSaveError("");
    resetPrefs({ loginMode: "AskBranch", defaultBranchId: null });
    setPrefsLoading(true);
    try {
      if (branches.length === 0) {
        setBranchesLoading(true);
        branchLookupFacade
          .list("all")
          .then(setBranches)
          .finally(() => setBranchesLoading(false));
      }
      const existing = await companyUserPreferencesFacade.get(
        target.companyUserMembershipId,
      );
      resetPrefs({
        loginMode: existing?.loginMode ?? "AskBranch",
        defaultBranchId: existing?.defaultBranchId ?? null,
      });
    } catch (e) {
      setPrefsSaveError(
        formatApiRequestError(e, {
          generic: t(
            "security.preferences.error.load",
            "No se pudieron cargar las preferencias.",
          ),
        }),
      );
    } finally {
      setPrefsLoading(false);
    }
  };

  const closePreferences = () => {
    setPrefsModalOpen(false);
    setPrefsTarget(null);
    setPrefsSaveError("");
  };

  const onSubmitPreferences = handlePrefsSubmit(async (values) => {
    if (!prefsTarget) return;
    setPrefsSaving(true);
    setPrefsSaveError("");
    try {
      await companyUserPreferencesFacade.update(
        prefsTarget.companyUserMembershipId,
        {
          loginMode: values.loginMode,
          defaultBranchId: values.defaultBranchId || null,
        },
      );
      message.success(
        t(
          "security.preferences.success",
          "Preferencias actualizadas correctamente.",
        ),
      );
      closePreferences();
    } catch (e) {
      const applied = applyServerErrors(e, setPrefsFieldError, (msg) =>
        setPrefsSaveError(msg),
      );
      if (!applied) {
        setPrefsSaveError(
          formatApiRequestError(e, {
            generic: t(
              "security.preferences.error.save",
              "No se pudieron guardar las preferencias.",
            ),
          }),
        );
      }
    } finally {
      setPrefsSaving(false);
    }
  });

  useEffect(() => {
    if (!isAdminUser) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError("");
        const data = await securityService.getAdminMatrix();
        if (cancelled) return;
        setMatrix(data);
      } catch (e) {
        if (cancelled) return;
        setError(formatApiError(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isAdminUser]);

  const rows = useMemo(() => {
    if (!matrix) return [];
    return matrix.users
      .slice()
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
  }, [matrix]);

  const scopeStateByUserId = useMemo(() => {
    const map = new Map<string, Set<number>>();
    if (!matrix) return map;
    for (const u of matrix.users) map.set(u.id, userAllowedScopes(matrix, u));
    return map;
  }, [matrix]);

  const toggleScope = async (targetUserId: string, scope: number) => {
    if (!matrix) return;
    const current = new Set(scopeStateByUserId.get(targetUserId) ?? []);
    if (current.has(scope)) current.delete(scope);
    else current.add(scope);

    setSavingKey(targetUserId);
    setError("");
    try {
      await securityService.upsertAdminScopes({
        subjectType: "User",
        subjectKey: targetUserId,
        allowedScopes: [...current.values()],
      });

      // Refresh local matrix assignments for this user (optimistic merge).
      const nextAssignments = matrix.assignments
        .filter(
          (a) => !(a.subjectType === "User" && a.subjectKey === targetUserId),
        )
        .concat(
          [...current.values()].map((s) => ({
            subjectType: "User" as const,
            subjectKey: targetUserId,
            scope: s,
            isAllowed: true,
          })),
        );

      setMatrix({ ...matrix, assignments: nextAssignments });
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSavingKey(null);
    }
  };

  if (!isAdminUser) {
    return <NoAccessPage title={t("security.title")} />;
  }

  return (
    <PageShell
      kicker={t("app.nav.group.security")}
      title={t("security.title")}
      subtitle={t("security.subtitle")}
    >
      {error ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={error}
        />
      ) : null}

      <ZHCard>
        {loading ? (
          <LoadingState />
        ) : rows.length === 0 ? (
          <EmptyState message={t("security.emptyUsers")} />
        ) : (
          <div className="table-scroll">
            <table className="table table--compact table--neutral table--matrix table--sticky-column">
              <thead>
                <tr>
                  <th className="table__sticky-cell">{t("security.users")}</th>
                  {scopeColumns.map((c) => (
                    <th key={c.key}>{t(c.labelKey)}</th>
                  ))}
                  {canManagePreferences ? (
                    <th>{t("security.preferences.column", "Preferencias")}</th>
                  ) : null}
                </tr>
              </thead>
              <tbody>
                {rows.map((u) => {
                  const current =
                    scopeStateByUserId.get(u.id) ?? new Set<number>();
                  const disabled = savingKey === u.id;
                  return (
                    <tr
                      key={u.id}
                      className={!u.isActive ? "row--inactive" : ""}
                    >
                      <td className="table__sticky-cell">
                        <div className="userCell">
                          <div className="userName">{u.fullName}</div>
                          <div className="userMeta">
                            <span className="mono security-mono">
                              {u.username}
                            </span>
                            {u.email ? (
                              <span className="mono security-mono">
                                {u.email}
                              </span>
                            ) : null}
                            <span className="security-badge">{u.role}</span>
                          </div>
                        </div>
                      </td>
                      {scopeColumns.map((c) => {
                        const scope = scopeMap[c.key];
                        const checked = current.has(scope);
                        return (
                          <td key={c.key} className="cell-center">
                            <ZHToggle
                              label={t(c.labelKey)}
                              description={u.fullName}
                              value={checked}
                              onChange={() => toggleScope(u.id, scope)}
                              disabled={disabled}
                            />
                          </td>
                        );
                      })}
                      {canManagePreferences ? (
                        <td className="cell-center">
                          <ZHBtn
                            variant="ghost"
                            size="md"
                            type="button"
                            onClick={() => void openPreferences(u)}
                          >
                            {t("security.preferences.action", "Preferencias")}
                          </ZHBtn>
                        </td>
                      ) : null}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </ZHCard>

      <ZHModal
        open={prefsModalOpen}
        onClose={closePreferences}
        size="md"
        title={t(
          "security.preferences.title",
          "Preferencias de inicio de sesión",
        )}
        subtitle={prefsTarget?.fullName}
      >
        {prefsLoading ? (
          <p className="subtle">{t("common.loading")}</p>
        ) : (
          <form onSubmit={onSubmitPreferences}>
            <ZHField
              label={t("security.preferences.loginMode", "Modo de ingreso")}
              error={prefsErrors.loginMode?.message}
            >
              <ZhSelect disabled={prefsSaving} {...registerPrefs("loginMode")}>
                {COMPANY_USER_LOGIN_MODES.map((mode) => (
                  <option key={mode} value={mode}>
                    {mode === "AskBranch"
                      ? t(
                          "security.preferences.loginMode.askBranch",
                          "Preguntar sucursal al iniciar sesión",
                        )
                      : t(
                          "security.preferences.loginMode.directToDefault",
                          "Ingresar directamente a la sucursal por defecto",
                        )}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField
              label={t(
                "security.preferences.defaultBranch",
                "Sucursal por defecto",
              )}
              error={prefsErrors.defaultBranchId?.message}
              hint={
                prefsLoginMode === "AskBranch"
                  ? t(
                      "security.preferences.defaultBranch.optional",
                      "Opcional en este modo.",
                    )
                  : null
              }
            >
              <ZhSelect
                disabled={prefsSaving || branchesLoading}
                {...registerPrefs("defaultBranchId")}
              >
                <option value="">
                  {t(
                    "security.preferences.defaultBranch.none",
                    "Sin sucursal por defecto",
                  )}
                </option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            {prefsSaveError ? (
              <ZHPageNotice
                variant="error"
                message={t("common.errorPrefix")}
                detail={prefsSaveError}
              />
            ) : null}

            <ZHFormActions
              onCancel={closePreferences}
              hideDraft
              saveButtonType="submit"
              disableSave={prefsSaving}
              labels={{
                cancel: t("common.cancel"),
                save: prefsSaving ? t("common.saving") : t("common.save"),
              }}
            />
          </form>
        )}
      </ZHModal>
    </PageShell>
  );
}
