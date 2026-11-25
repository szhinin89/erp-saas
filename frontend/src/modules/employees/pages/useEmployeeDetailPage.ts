import { useCallback, useEffect, useState } from "react";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAsync } from "../../../hooks/useAsync";
import { useAuthStore } from "../../../store/authStore";
import { message } from "../../../lib/messages";
import { readApiErrorCode } from "../../lib/apiError";
import { employeeErrorMessage, employeeService, isConcurrencyConflict } from "../api/employeeService";
import { employeeCatalogService } from "../api/employeeCatalogService";
import { ACTIVE_MEMBERSHIP_WARNING, type EmployeeDto } from "../types/employee.types";

/** Escritura de la ficha. `true` = aplicada; el llamador decide si cierra su diálogo. */
export type RunEmployeeWrite = (
  action: () => Promise<{ warnings?: string[] } | void>,
  messages: { success: string; error: string },
) => Promise<boolean>;

export function useEmployeeDetailPage(id: string | undefined) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("employees.view");
  const permissions = {
    update: canShow("employees.update"),
    disable: canShow("employees.disable"),
    manageAssignments: canShow("employees.assignments.manage"),
    linkUser: canShow("employees.link-user"),
    viewCatalogs: canShow("employees.catalogs.view"),
    viewBranches: canShow("settings.branches.view"),
    viewMemberships: canShow("access.company_user_memberships.view"),
  };
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  const [employee, setEmployee] = useState<EmployeeDto | null>(null);
  const [loading, setLoading] = useState(canView);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  /** Aviso de la última baja: vive solo en esta pantalla (no se persiste). */
  const [membershipWarning, setMembershipWarning] = useState(false);
  /** Cambia tras un conflicto: los formularios se remontan con lo que hay en el servidor. */
  const [resetToken, setResetToken] = useState(0);

  const catalogs = useAsync(employeeCatalogService.loadOptions, canView && permissions.viewCatalogs);

  const load = useCallback(async () => {
    if (!id) return;
    try {
      const next = await employeeService.getById(id);
      setEmployee(next);
      setError("");
      setNotFound(false);
    } catch (err) {
      setEmployee(null);
      if (readApiErrorCode(err) === "EMPLOYEE_NOT_FOUND") setNotFound(true);
      else setError(employeeErrorMessage(err, t("employees.detail.loadError", "No se pudo cargar la ficha.")));
    }
  }, [id, t]);

  useEffect(() => {
    if (!canView) return;
    setEmployee(null);
    setMembershipWarning(false);
    setLoading(true);
    void load().finally(() => setLoading(false));
  }, [canView, load, companySessionVersion]);

  const runWrite = useCallback<RunEmployeeWrite>(
    async (action, messages) => {
      setSaving(true);
      try {
        const result = await action();
        if (result?.warnings?.includes(ACTIVE_MEMBERSHIP_WARNING)) setMembershipWarning(true);
        message.success(messages.success);
        await load();
        return true;
      } catch (err) {
        message.error(employeeErrorMessage(err, messages.error));
        // Conflicto: se muestra lo actual y no se reaplica ni reintenta el cambio rechazado.
        if (isConcurrencyConflict(err)) {
          await load();
          setResetToken((v) => v + 1);
        }
        return false;
      } finally {
        setSaving(false);
      }
    },
    [load],
  );

  return {
    canView,
    permissions,
    employee,
    loading,
    notFound,
    error,
    saving,
    membershipWarning,
    resetToken,
    catalogs,
    runWrite,
    reload: load,
  };
}
