import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useAuthStore } from "../../../../store/authStore";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  membershipService,
  type CompanyUserMembershipAdminDto,
} from "../api/membershipService";
import { branchAssignmentService } from "../api/branchAssignmentService";
import { companyUserPreferencesService } from "../../api/companyUserPreferencesService";
import "./UsersPage.css";

const PERMISSION = "access.company_user_memberships.view";

/**
 * Fase I-C. Carga en paralelo, por fila, el resumen de sucursales autorizadas y el LoginMode —
 * no existe (ni se crea en esta fase) un endpoint de lista que ya traiga ese resumen agregado;
 * GET .../branches y GET .../preferences (Fase I-B/F) son las únicas fuentes. Con el volumen
 * típico de usuarios por empresa esto es aceptable; una empresa con cientos de usuarios se
 * beneficiaría de un endpoint de resumen agregado (fuera de alcance de esta fase).
 */
function useMembershipExtras(membershipId: string) {
  const [branches, setBranches] = useState<{
    authorized: number;
    total: number;
  } | null>(null);
  const [loginMode, setLoginMode] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.allSettled([
      branchAssignmentService.getMembershipBranches(membershipId),
      companyUserPreferencesService.get(membershipId),
    ]).then(([branchesResult, prefsResult]) => {
      if (cancelled) return;
      if (branchesResult.status === "fulfilled") {
        const list = branchesResult.value.branches;
        setBranches({
          authorized: list.filter((b) => b.authorized).length,
          total: list.length,
        });
      }
      if (prefsResult.status === "fulfilled") {
        setLoginMode(prefsResult.value?.loginMode ?? null);
      }
      setLoading(false);
    });
    return () => {
      cancelled = true;
    };
  }, [membershipId]);

  return { branches, loginMode, loading };
}

function MembershipRow(props: {
  row: CompanyUserMembershipAdminDto;
  canManage: boolean;
  onOpen: (row: CompanyUserMembershipAdminDto) => void;
  onRevoke: (row: CompanyUserMembershipAdminDto) => void;
  onReactivate: (row: CompanyUserMembershipAdminDto) => void;
  actionPending: boolean;
}) {
  const { row, canManage, onOpen, onRevoke, onReactivate, actionPending } =
    props;
  const { t } = useI18n();
  const { branches, loginMode, loading } = useMembershipExtras(
    row.companyUserId,
  );

  return (
    <tr className={!row.isActive ? "acc-row--inactive" : ""}>
      <td>
        <button
          type="button"
          className="acc-user-cell acc-user-cell--link"
          onClick={() => onOpen(row)}
        >
          <span className="acc-user-name">{row.fullName}</span>
          <span className="acc-user-username">{row.username}</span>
        </button>
      </td>
      <td className="acc-user-email">{row.email ?? "—"}</td>
      <td>{row.profileName ?? t("users.table.noProfile", "Sin perfil")}</td>
      <td>{row.role}</td>
      <td>
        <span
          className={`zh-status zh-status--${row.isActive ? "active" : "inactive"}`}
        >
          {row.isActive ? t("common.active") : t("common.inactive")}
        </span>
      </td>
      <td>
        {loading
          ? t("common.loading")
          : branches
            ? `${branches.authorized}/${branches.total}`
            : "—"}
      </td>
      <td>
        {loading
          ? t("common.loading")
          : loginMode === "DirectToDefault"
            ? t("security.preferences.loginMode.directToDefault")
            : loginMode === "AskBranch"
              ? t("security.preferences.loginMode.askBranch")
              : "—"}
      </td>
      {canManage ? (
        <td>
          <div className="acc-row-actions">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => onOpen(row)}
            >
              {t("users.actions.configure", "Configurar")}
            </ZHBtn>
            {row.isActive ? (
              <ZHBtn
                variant="destructive"
                size="sm"
                type="button"
                disabled={actionPending}
                onClick={() => onRevoke(row)}
              >
                {t("users.actions.revoke", "Revocar")}
              </ZHBtn>
            ) : (
              <ZHBtn
                variant="secondary"
                size="sm"
                type="button"
                disabled={actionPending}
                onClick={() => onReactivate(row)}
              >
                {t("users.actions.reactivate", "Reactivar")}
              </ZHBtn>
            )}
          </div>
        </td>
      ) : null}
    </tr>
  );
}

export function UsersPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const { canShow, isAdminRole } = usePermissionsUi();
  const canManage = canShow(PERMISSION);

  const [rows, setRows] = useState<CompanyUserMembershipAdminDto[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState("");
  const [actionPendingId, setActionPendingId] = useState<string | null>(null);

  // El buscador vive en la URL (?q=) — no en estado local — para que UserConfigPage pueda
  // devolver al admin a esta misma vista con el filtro intacto al guardar/cancelar.
  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get("q") ?? "";
  const setSearch = (value: string) => {
    setSearchParams(value ? { q: value } : {}, { replace: true });
  };
  const listPath = `/access/users${searchParams.toString() ? `?${searchParams.toString()}` : ""}`;

  const loadUsers = useCallback(async () => {
    setListLoading(true);
    setListError("");
    try {
      setRows(await membershipService.list(false));
    } catch (err) {
      setListError(
        formatApiRequestError(err, {
          generic: t(
            "users.error.load",
            "No se pudo cargar la lista de usuarios.",
          ),
        }),
      );
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!isAdminRole && !canManage) return;
    void loadUsers();
  }, [isAdminRole, canManage, loadUsers]);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) =>
      `${r.fullName} ${r.username} ${r.email ?? ""}`.toLowerCase().includes(q),
    );
  }, [rows, search]);

  const openUser = (row: CompanyUserMembershipAdminDto) => {
    navigate(`/access/users/${row.companyUserId}`, {
      state: { from: listPath },
    });
  };

  const handleRevoke = async (row: CompanyUserMembershipAdminDto) => {
    const confirmed = await message.confirm({
      title: t("users.revoke.confirmTitle", "Revocar acceso"),
      message: t("users.revoke.confirmMessage", { name: row.fullName }),
      variant: "danger",
      confirmLabel: t("users.actions.revoke", "Revocar"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setActionPendingId(row.companyUserId);
    try {
      await membershipService.revokeMembership(row.username);
      message.success(
        t("users.revoke.success", "Acceso revocado correctamente."),
      );
      await loadUsers();
    } catch (err) {
      message.error(
        formatApiRequestError(err, {
          generic: t("users.revoke.error", "No se pudo revocar el acceso."),
        }),
      );
    } finally {
      setActionPendingId(null);
    }
  };

  const handleReactivate = async (row: CompanyUserMembershipAdminDto) => {
    const confirmed = await message.confirm({
      title: t("users.reactivate.confirmTitle", "Reactivar usuario"),
      message: t(
        "users.reactivate.confirmMessage",
        `Vas a reactivar el acceso de "${row.fullName}" a esta empresa.`,
      ),
      variant: "warning",
      confirmLabel: t("users.actions.reactivate", "Reactivar"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setActionPendingId(row.companyUserId);
    try {
      await membershipService.upsertMembership({
        username: row.username,
        role: row.role,
        profileId: row.profileId,
      });
      message.success(
        t("users.reactivate.success", "Usuario reactivado correctamente."),
      );
      await loadUsers();
    } catch (err) {
      message.error(
        formatApiRequestError(err, {
          generic: t(
            "users.reactivate.error",
            "No se pudo reactivar el usuario.",
          ),
        }),
      );
    } finally {
      setActionPendingId(null);
    }
  };

  if (!user || (!isAdminRole && !canManage)) {
    return <NoAccessPage title={t("users.title", "Usuarios empresariales")} />;
  }

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("users.title", "Usuarios empresariales")}
      subtitle={t(
        "users.subtitle",
        "Administra el acceso, rol, perfil y sucursales de los usuarios de esta empresa.",
      )}
      action={
        canManage ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() =>
              navigate("/access/users/new", { state: { from: listPath } })
            }
          >
            + {t("users.addUser.action", "Agregar usuario")}
          </ZHBtn>
        ) : undefined
      }
    >
      {listError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={listError}
        />
      ) : null}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-search">
            <span className="material-symbols-outlined">search</span>
            <ZhTextInput
              className="zh-input"
              placeholder={t("common.zhList.searchPlaceholder")}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <span className="pg-result-count">
            {filteredRows.length} {t("common.zhList.entityLabel")}
          </span>
        </div>

        {listLoading ? (
          <p className="subtle pg-state-pad">{t("common.loading")}</p>
        ) : filteredRows.length === 0 ? (
          <p className="subtle pg-state-pad">{t("common.noData")}</p>
        ) : (
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("users.table.user", "Usuario")}</th>
                  <th>{t("users.table.email", "Email")}</th>
                  <th>{t("users.table.profile", "Perfil")}</th>
                  <th>{t("users.table.role", "Role")}</th>
                  <th>{t("users.table.status", "Estado")}</th>
                  <th>{t("users.table.branches", "Sucursales autorizadas")}</th>
                  <th>{t("users.table.loginMode", "Modo de ingreso")}</th>
                  {canManage ? (
                    <th className="pg-th-actions">{t("common.actions")}</th>
                  ) : null}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map((row) => (
                  <MembershipRow
                    key={row.companyUserId}
                    row={row}
                    canManage={canManage}
                    onOpen={openUser}
                    onRevoke={(r) => void handleRevoke(r)}
                    onReactivate={(r) => void handleReactivate(r)}
                    actionPending={actionPendingId === row.companyUserId}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}
