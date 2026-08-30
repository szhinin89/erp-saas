import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { NoAccessPage } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
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

interface MembershipExtras {
  branchesText: string;
  loginModeText: string;
  loading: boolean;
}

/**
 * ZH-LISTING-USERS-REFACTOR-03 — reemplaza el hook `useMembershipExtras(membershipId)` que antes
 * corría DENTRO de cada fila (`MembershipRow`, un componente por fila con su propio hook). Ese
 * patrón es incompatible con `ZHDataTableColumn.render(row)`, que es una función normal — no un
 * componente — y no puede llamar hooks. Se sube la carga a nivel de página: un solo efecto por
 * lista de membresías, con caché por `companyUserId` en un mapa de estado (evita refetch al
 * buscar/filtrar, ya que el filtro es client-side sobre la misma lista). Fase I-C sigue vigente:
 * no existe un endpoint de lista que traiga sucursales/loginMode agregados — se resuelven aparte
 * por GET .../branches y GET .../preferences (Fase I-B/F).
 */
function useMembershipExtrasCache(
  memberships: CompanyUserMembershipAdminDto[],
): Record<string, MembershipExtras> {
  const { t } = useI18n();
  const [cache, setCache] = useState<Record<string, MembershipExtras>>({});
  // Ids ya solicitados (en curso o resueltos) — evita refetch duplicado sin depender del estado
  // `cache` (que el propio efecto actualiza) como dependencia del efecto.
  const requestedIdsRef = useRef<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    const idsToFetch = Array.from(new Set(memberships.map((m) => m.companyUserId))).filter(
      (id) => !requestedIdsRef.current.has(id),
    );
    if (idsToFetch.length === 0) return;
    idsToFetch.forEach((id) => requestedIdsRef.current.add(id));

    setCache((prev) => {
      const next = { ...prev };
      idsToFetch.forEach((id) => {
        next[id] = {
          branchesText: t("common.loading"),
          loginModeText: t("common.loading"),
          loading: true,
        };
      });
      return next;
    });

    idsToFetch.forEach((membershipId) => {
      Promise.allSettled([
        branchAssignmentService.getMembershipBranches(membershipId),
        companyUserPreferencesService.get(membershipId),
      ]).then(([branchesResult, prefsResult]) => {
        if (cancelled) return;
        const branchesText =
          branchesResult.status === "fulfilled"
            ? `${branchesResult.value.branches.filter((b) => b.authorized).length}/${branchesResult.value.branches.length}`
            : "—";
        const loginMode =
          prefsResult.status === "fulfilled" ? (prefsResult.value?.loginMode ?? null) : null;
        const loginModeText =
          loginMode === "DirectToDefault"
            ? t("security.preferences.loginMode.directToDefault")
            : loginMode === "AskBranch"
              ? t("security.preferences.loginMode.askBranch")
              : "—";
        setCache((prev) => ({
          ...prev,
          [membershipId]: { branchesText, loginModeText, loading: false },
        }));
      });
    });

    return () => {
      cancelled = true;
    };
  }, [memberships, t]);

  return cache;
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

  // Caché a nivel de página — se alimenta de la lista completa (no de `filteredRows`) para que
  // buscar/filtrar no dispare refetch de sucursales/loginMode ya resueltas.
  const extrasByMembershipId = useMembershipExtrasCache(rows);

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

  // ZH-LISTING-USERS-REFACTOR-03: columnas puras — solo leen `row` y el mapa `extrasByMembershipId`
  // ya resuelto arriba (a nivel de página). Ningún hook se llama dentro de estos `render(row)`.
  const columns: ZHDataTableColumn<CompanyUserMembershipAdminDto>[] = [
    {
      key: "user",
      header: t("users.table.user", "Usuario"),
      render: (row) => (
        <button
          type="button"
          className="acc-user-cell acc-user-cell--link"
          onClick={() => openUser(row)}
        >
          <span className="acc-user-name">{row.fullName}</span>
          <span className="acc-user-username">{row.username}</span>
        </button>
      ),
    },
    {
      key: "email",
      header: t("users.table.email", "Email"),
      render: (row) => <span className="acc-user-email">{row.email ?? "—"}</span>,
    },
    {
      key: "profile",
      header: t("users.table.profile", "Perfil"),
      render: (row) => row.profileName ?? t("users.table.noProfile", "Sin perfil"),
    },
    {
      key: "role",
      header: t("users.table.role", "Role"),
      render: (row) => row.role,
    },
    {
      key: "status",
      header: t("users.table.status", "Estado"),
      render: (row) => (
        <span className={`zh-status zh-status--${row.isActive ? "active" : "inactive"}`}>
          {row.isActive ? t("common.active") : t("common.inactive")}
        </span>
      ),
    },
    {
      key: "branches",
      header: t("users.table.branches", "Sucursales autorizadas"),
      render: (row) => extrasByMembershipId[row.companyUserId]?.branchesText ?? "—",
    },
    {
      key: "loginMode",
      header: t("users.table.loginMode", "Modo de ingreso"),
      render: (row) => extrasByMembershipId[row.companyUserId]?.loginModeText ?? "—",
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: t("common.actions"),
            align: "right" as const,
            render: (row: CompanyUserMembershipAdminDto) => (
              <div className="acc-row-actions">
                <ZHBtn
                  variant="ghost"
                  size="sm"
                  type="button"
                  onClick={() => openUser(row)}
                  aria-label={`${t("users.actions.configure", "Configurar")} ${row.fullName}`}
                >
                  {t("users.actions.configure", "Configurar")}
                </ZHBtn>
                {row.isActive ? (
                  <ZHBtn
                    variant="destructive"
                    size="sm"
                    type="button"
                    disabled={actionPendingId === row.companyUserId}
                    onClick={() => void handleRevoke(row)}
                    aria-label={`${t("users.actions.revoke", "Revocar")} ${row.fullName}`}
                  >
                    {t("users.actions.revoke", "Revocar")}
                  </ZHBtn>
                ) : (
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    type="button"
                    disabled={actionPendingId === row.companyUserId}
                    onClick={() => void handleReactivate(row)}
                    aria-label={`${t("users.actions.reactivate", "Reactivar")} ${row.fullName}`}
                  >
                    {t("users.actions.reactivate", "Reactivar")}
                  </ZHBtn>
                )}
              </div>
            ),
          },
        ]
      : []),
  ];

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

        <ZHDataTable
          columns={columns}
          rows={filteredRows}
          rowKey={(row) => row.companyUserId}
          loading={listLoading}
          showRowNumber
          rowClassName={(row) => (row.isActive ? undefined : "acc-row--inactive")}
          emptyMessage={t("common.noData")}
        />
      </div>
    </ErpPageTemplate>
  );
}
