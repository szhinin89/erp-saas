import { useEffect, useMemo, useState } from "react";
import {
  EmptyState,
  LoadingState,
  PageShell,
  NoAccessPage,
} from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHToggle } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  securityService,
  type SecurityAdminMatrix,
  type SecurityUser,
} from "../api/securityService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatApiRequestError } from "../../lib/apiError";
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
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const [matrix, setMatrix] = useState<SecurityAdminMatrix | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [error, setError] = useState("");

  const canView = canShow("admin.delegation.view");
  const canConfigure = canShow("admin.delegation.configure");

  useEffect(() => {
    if (!canView) {
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
        setError(
          formatApiRequestError(e, {
            generic: t("security.loadError", "No se pudo cargar la matriz de delegación."),
          }),
        );
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
    // t() se usa dentro del efecto pero no debe disparar un refetch — su identidad cambia en
    // cada render y duplicaría las llamadas HTTP (mismo criterio ya aplicado en UserConfigPage).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView]);

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

  const toggleScope = async (
    targetUserId: string,
    targetUserName: string,
    scope: number,
    scopeLabel: string,
  ) => {
    if (!matrix || savingKey) return;
    const current = new Set(scopeStateByUserId.get(targetUserId) ?? []);
    const granting = !current.has(scope);
    if (granting) current.add(scope);
    else current.delete(scope);

    const confirmed = await message.confirm({
      title: granting
        ? `Otorgar "${scopeLabel}"`
        : `Revocar "${scopeLabel}"`,
      message: (
        <p className="zh-confirm-message">
          Vas a {granting ? "otorgar" : "revocar"} la capacidad{" "}
          <strong>{scopeLabel}</strong> a <strong>{targetUserName}</strong>. Esto modifica una
          capacidad administrativa sensible: quien la tenga podrá administrar roles, módulos,
          pantallas o procesos según el permiso otorgado.
        </p>
      ),
      variant: granting ? "warning" : "danger",
      confirmLabel: granting ? "Otorgar" : "Revocar",
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSavingKey(targetUserId);
    setError("");
    try {
      await securityService.upsertAdminScopes({
        subjectType: "User",
        subjectKey: targetUserId,
        allowedScopes: [...current.values()],
      });

      // El estado local solo se actualiza tras confirmar éxito real del backend — nunca antes.
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
      message.success(
        t("security.scopeUpdateSuccess", "Capacidad actualizada correctamente."),
      );
    } catch (e) {
      setError(
        formatApiRequestError(e, {
          generic: t("security.scopeUpdateError", "No se pudo actualizar la capacidad."),
        }),
      );
    } finally {
      setSavingKey(null);
    }
  };

  if (!canView) {
    return <NoAccessPage title={t("security.title")} />;
  }

  return (
    <PageShell
      kicker={t("app.nav.group.admin")}
      title={t("security.title")}
      subtitle={t("security.subtitle")}
    >
      <ZHPageNotice variant="info" message={t("security.profilesNote")} />

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
                </tr>
              </thead>
              <tbody>
                {rows.map((u) => {
                  const current =
                    scopeStateByUserId.get(u.id) ?? new Set<number>();
                  const disabled = savingKey === u.id || !canConfigure;
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
                              onChange={() =>
                                void toggleScope(u.id, u.fullName, scope, t(c.labelKey))
                              }
                              disabled={disabled}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </ZHCard>
    </PageShell>
  );
}
