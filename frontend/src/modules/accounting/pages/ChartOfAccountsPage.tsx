import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { PageShell, Badge } from "../../../components/PageShell";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import { ACCOUNT_NATURE_OPTIONS, ACCOUNT_TYPE_OPTIONS } from "../schemas/accountSchema";
import { AccountFormModal, type AccountFormIntent } from "../components/AccountFormModal";
import {
  ancestorIds,
  buildAccountTree,
  cleanAccountName,
  containerIds,
  filteredTreeRows,
  flatRows,
  initialCollapsedIds,
  visibleTreeRows,
  type AccountTreeRow,
} from "../utils/accountTreeModel";

import "../../../styles/shared/items-catalog.css";
import "./ChartOfAccountsPage.css";

const ACCOUNT_TYPE_LABEL: Record<string, string> = Object.fromEntries(
  ACCOUNT_TYPE_OPTIONS.map((o) => [o.value, o.label]),
);
const ACCOUNT_NATURE_LABEL: Record<string, string> = Object.fromEntries(
  ACCOUNT_NATURE_OPTIONS.map((o) => [o.value, o.label]),
);

/** Autorizaciones reales de los endpoints `accounts*` (AccountingController). */
const PERMISSIONS = {
  create: "accounting.create",
  update: "accounting.update",
  disable: "accounting.delete",
} as const;

type ClassFilter = "" | "group" | "posting";
type StatusFilter = "" | "active" | "inactive";

interface AccountNameCellProps {
  row: AccountTreeRow;
  treeMode: boolean;
  expanded: boolean;
  orphan: boolean;
  onToggle: (account: AccountDto) => void;
}

function AccountNameCell({ row, treeMode, expanded, orphan, onToggle }: Readonly<AccountNameCellProps>) {
  const { account, depth, childCount, isContext } = row;
  const name = cleanAccountName(account.name);
  const canToggle = treeMode && childCount > 0;

  return (
    <span
      className={`acc-coa-tree-name${account.allowsPosting ? "" : " acc-coa-tree-name--group"}`}
      data-depth={depth}
    >
      {depth > 0 && (
        <span className="acc-coa-tree-name__guides" aria-hidden="true">
          {Array.from({ length: depth }, (_, index) => (
            <span
              key={index}
              className={`acc-coa-tree-name__guide${index === depth - 1 ? " acc-coa-tree-name__guide--branch" : ""}`}
            />
          ))}
        </span>
      )}
      {treeMode && (
      <span className="acc-coa-tree-toggle">
        {canToggle && (
          <ZHBtn
            type="button"
            variant="secondary"
            size="xs"
            expanded={expanded}
            aria-label={`${expanded ? "Contraer" : "Expandir"} ${account.code} ${name} (${childCount} subcuentas)`}
            title={expanded ? "Contraer" : "Expandir"}
            onClick={() => onToggle(account)}
          >
            {childCount}
          </ZHBtn>
        )}
      </span>
      )}
      <span className="zh-account-ref acc-coa-tree-name__label">
        <span className="zh-account-ref__code">{account.code}</span>
        <span className="zh-account-ref__name">{name}</span>
        {isContext && (
          <ZHDataValue variant="muted" italic>
            (contexto)
          </ZHDataValue>
        )}
        {orphan && (
          <ZHDataValue variant="muted" italic>
            (padre no disponible)
          </ZHDataValue>
        )}
      </span>
    </span>
  );
}

/**
 * Plan de Cuentas — ACCOUNTING-CHART-UX-01. Auditoría de reutilización: `ZHDataTable` (jerarquía
 * expandible sin componente de tabla nuevo), `ZHFilterBar plain`, `ReportKpiCard`, `ZHModal`
 * (vía AccountFormModal, patrón de Categorías de gasto), `Badge`, `ZHDataValue`, `.zh-account-ref`
 * y `usePermissionsUi`. Sin cambios de backend, DTO ni reglas: Code/AccountType/Nature siguen
 * inmutables y las confirmaciones de activar/desactivar se conservan.
 * ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: sin botones cruzados hacia Asientos/Reportes.
 */
export function ChartOfAccountsPage() {
  const { has } = usePermissionsUi();
  const canCreate = has(PERMISSIONS.create);
  const canUpdate = has(PERMISSIONS.update);
  const canDisable = has(PERMISSIONS.disable);

  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string> | null>(null);
  const [intent, setIntent] = useState<AccountFormIntent | null>(null);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [classFilter, setClassFilter] = useState<ClassFilter>("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [levelFilter, setLevelFilter] = useState("");
  const [togglingAccountId, setTogglingAccountId] = useState<string | null>(null);
  /** Control que abrió el modal; recupera el foco al cerrarlo. */
  const openerRef = useRef<HTMLElement | null>(null);

  const fetchAccounts = useCallback(async (): Promise<AccountDto[] | null> => {
    setLoading(true);
    try {
      const list = await accountingApi.listAccounts();
      setAccounts(list);
      return list;
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cargar el Plan de Cuentas." }));
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchAccounts();
  }, [fetchAccounts]);

  const tree = useMemo(() => buildAccountTree(accounts), [accounts]);
  const collapsedIds = useMemo(() => collapsed ?? initialCollapsedIds(tree), [collapsed, tree]);

  const hasFilters = Boolean(search.trim() || typeFilter || classFilter || statusFilter || levelFilter);
  const treeMode = levelFilter === "";

  const matches = useMemo(() => {
    const term = search.trim().toLowerCase();
    const ids = new Set<string>();
    for (const a of accounts) {
      if (typeFilter && a.accountType !== typeFilter) continue;
      if (classFilter === "group" && a.allowsPosting) continue;
      if (classFilter === "posting" && !a.allowsPosting) continue;
      if (statusFilter === "active" && !a.isActive) continue;
      if (statusFilter === "inactive" && a.isActive) continue;
      if (levelFilter !== "" && String(a.level) !== levelFilter) continue;
      if (term && !a.code.toLowerCase().includes(term) && !a.name.toLowerCase().includes(term)) continue;
      ids.add(a.id);
    }
    return ids;
  }, [accounts, search, typeFilter, classFilter, statusFilter, levelFilter]);

  const rows = useMemo(() => {
    if (!treeMode) return flatRows(tree, matches);
    if (hasFilters) return filteredTreeRows(tree, matches);
    return visibleTreeRows(tree, collapsedIds);
  }, [tree, matches, treeMode, hasFilters, collapsedIds]);

  const summary = useMemo(() => {
    const group = accounts.filter((a) => !a.allowsPosting).length;
    return {
      total: accounts.length,
      group,
      posting: accounts.length - group,
      inactive: accounts.filter((a) => !a.isActive).length,
    };
  }, [accounts]);

  const availableLevels = useMemo(
    () => Array.from(new Set(accounts.map((a) => a.level))).sort((a, b) => a - b),
    [accounts],
  );

  const clearFilters = () => {
    setSearch("");
    setTypeFilter("");
    setClassFilter("");
    setStatusFilter("");
    setLevelFilter("");
  };

  const handleToggleExpanded = (account: AccountDto) => {
    const next = new Set(collapsedIds);
    if (next.has(account.id)) next.delete(account.id);
    else next.add(account.id);
    setCollapsed(next);
  };

  const openModal = (opener: HTMLElement, next: AccountFormIntent) => {
    openerRef.current = opener;
    setIntent(next);
  };

  const closeModal = () => {
    setIntent(null);
    const opener = openerRef.current;
    openerRef.current = null;
    window.setTimeout(() => {
      if (opener?.isConnected) opener.focus();
    }, 0);
  };

  /** Tras guardar: recarga y deja visible la cuenta guardada expandiendo sus ancestros. */
  const handleSaved = async (saved: AccountDto) => {
    closeModal();
    const list = await fetchAccounts();
    if (!list) return;
    const ancestors = ancestorIds(buildAccountTree(list), saved.id);
    if (ancestors.length === 0) return;
    setCollapsed((current) => {
      const next = new Set(current ?? collapsedIds);
      ancestors.forEach((id) => next.delete(id));
      return next;
    });
  };

  // CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05: afecta si la cuenta puede usarse en nuevos
  // asientos/reglas de posteo — se confirma antes de ejecutar. No cambia validaciones ni lógica
  // contable del backend, solo agrega confirmación previa y bloqueo de doble submit.
  const handleToggleActive = async (a: AccountDto) => {
    if (togglingAccountId) return;

    const label = `${a.code} — ${a.name}`;
    const confirmed = await message.confirm({
      title: a.isActive ? `Desactivar cuenta "${label}"` : `Activar cuenta "${label}"`,
      message: a.isActive
        ? `La cuenta "${label}" no podrá usarse para nuevos asientos ni reglas de posteo. Los asientos ya registrados no se eliminan. Si hay reglas contables asociadas a esta cuenta, revísalas antes de continuar.`
        : `La cuenta "${label}" volverá a estar disponible para uso contable (nuevos asientos y reglas de posteo).`,
      variant: a.isActive ? "danger" : "warning",
      confirmLabel: a.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setTogglingAccountId(a.id);
    try {
      if (a.isActive) await accountingApi.disableAccount(a.id);
      else await accountingApi.enableAccount(a.id);
      message.success(a.isActive ? "Cuenta desactivada correctamente." : "Cuenta activada correctamente.");
      void fetchAccounts();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cambiar el estado de la cuenta." }));
    } finally {
      setTogglingAccountId(null);
    }
  };

  const columns: ZHDataTableColumn<AccountTreeRow>[] = [
    {
      key: "account",
      header: "Cuenta",
      render: (row) => (
        <AccountNameCell
          row={row}
          treeMode={treeMode && !hasFilters}
          expanded={!collapsedIds.has(row.account.id)}
          orphan={tree.orphanIds.has(row.account.id)}
          onToggle={handleToggleExpanded}
        />
      ),
    },
    {
      key: "classification",
      header: "Tipo / Naturaleza",
      render: ({ account }) => (
        <>
          <div>
            <ZHDataValue>{ACCOUNT_TYPE_LABEL[account.accountType] ?? account.accountType}</ZHDataValue>
          </div>
          <div>
            <ZHDataValue variant="muted">{ACCOUNT_NATURE_LABEL[account.nature] ?? account.nature}</ZHDataValue>
          </div>
        </>
      ),
    },
    {
      key: "class",
      header: "Clase",
      render: ({ account }) => (
        <Badge
          label={account.allowsPosting ? "Movimiento" : "Agrupadora"}
          variant={account.allowsPosting ? "blue" : "neutral"}
        />
      ),
    },
    {
      key: "status",
      header: "Estado",
      render: ({ account }) => (
        <Badge label={account.isActive ? "Activa" : "Inactiva"} variant={account.isActive ? "green" : "gray"} />
      ),
    },
    {
      key: "actions",
      header: "Acciones",
      align: "right",
      cellClassName: "zh-table-cell--actions",
      render: ({ account }) => {
        const label = `${account.code} ${cleanAccountName(account.name)}`;
        const canToggle = account.isActive ? canDisable : canUpdate;
        return (
          <div className="prd-row-actions">
            {canCreate && !account.allowsPosting && account.isActive && (
              <ZHIconButton
                icon="add"
                title="Crear subcuenta"
                ariaLabel={`Crear subcuenta de ${label}`}
                variant="ghost"
                onClick={(e) => openModal(e.currentTarget, { kind: "create", parent: account })}
              />
            )}
            {canUpdate && (
              <ZHIconButton
                icon="edit"
                title="Editar"
                ariaLabel={`Editar ${label}`}
                variant="ghost"
                onClick={(e) => openModal(e.currentTarget, { kind: "edit", account })}
              />
            )}
            {canToggle && (
              <ZHIconButton
                icon={account.isActive ? "toggle_on" : "toggle_off"}
                title={account.isActive ? "Desactivar" : "Activar"}
                ariaLabel={`${account.isActive ? "Desactivar" : "Activar"} ${label}`}
                variant="ghost"
                disabled={togglingAccountId === account.id}
                onClick={() => void handleToggleActive(account)}
              />
            )}
          </div>
        );
      },
    },
  ];

  const treeActionsDisabled = loading || hasFilters || accounts.length === 0;

  // Contador: sin filtros = filas visibles según expansión; con filtros = coincidencias y
  // ancestros de contexto por separado (el contexto nunca cuenta como resultado). Siempre el total.
  const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
  const contextCount = rows.length - matches.size;
  const listSummary = [
    hasFilters ? plural(matches.size, "coincidencia", "coincidencias") : plural(rows.length, "visible", "visibles"),
    hasFilters && contextCount > 0 ? plural(contextCount, "cuenta de contexto", "cuentas de contexto") : null,
    `${accounts.length} ${accounts.length === 1 ? "cuenta" : "cuentas"} en total`,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <PageShell
      kicker="Contabilidad"
      title="Plan de cuentas"
      subtitle="Cuentas contables de la empresa — clasificación, jerarquía y estado"
      action={
        canCreate ? (
          <ZHBtn
            type="button"
            variant="primary"
            icon="add"
            onClick={(e) => openModal(e.currentTarget, { kind: "create", parent: null })}
          >
            Nueva cuenta
          </ZHBtn>
        ) : undefined
      }
    >
      {!loading && (
        <div className="pg-kpis">
          <ReportKpiCard layout="horizontal" icon="account_tree" tone="primary" label="Total cuentas" value={String(summary.total)} />
          <ReportKpiCard layout="horizontal" icon="folder" tone="secondary" label="Agrupadoras" value={String(summary.group)} />
          <ReportKpiCard layout="horizontal" icon="payments" tone="info" label="Movimiento" value={String(summary.posting)} />
          <ReportKpiCard layout="horizontal" icon="block" tone="error" label="Inactivas" value={String(summary.inactive)} />
        </div>
      )}

      <ZHCard
        className="acc-coa-list-card"
        title="Listado"
        actions={
          <div className="acc-coa-tree-actions">
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              icon="unfold_more"
              disabled={treeActionsDisabled}
              onClick={() => setCollapsed(new Set())}
            >
              Expandir todo
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              icon="unfold_less"
              disabled={treeActionsDisabled}
              onClick={() => setCollapsed(new Set(containerIds(tree)))}
            >
              Contraer todo
            </ZHBtn>
            <ZHBtn type="button" variant="ghost" size="sm" icon="refresh" onClick={() => void fetchAccounts()} disabled={loading}>
              Actualizar
            </ZHBtn>
          </div>
        }
      >
        <ZHFilterBar plain onClear={hasFilters ? clearFilters : undefined}>
          <div className="zh-filterbar__field zh-filterbar__field--grow">
            <ZHField label="Buscar" density="compact">
              <ZhTextInput
                placeholder="Código o nombre..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Tipo" density="compact">
              <ZhSelect value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
                <option value="">Todos los tipos</option>
                {ACCOUNT_TYPE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Clase" density="compact">
              <ZhSelect value={classFilter} onChange={(e) => setClassFilter(e.target.value as ClassFilter)}>
                <option value="">Todas las clases</option>
                <option value="group">Agrupadoras</option>
                <option value="posting">Movimiento</option>
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Estado" density="compact">
              <ZhSelect value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}>
                <option value="">Todos los estados</option>
                <option value="active">Activas</option>
                <option value="inactive">Inactivas</option>
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Nivel" density="compact">
              <ZhSelect value={levelFilter} onChange={(e) => setLevelFilter(e.target.value)}>
                <option value="">Todos (jerarquía)</option>
                {availableLevels.map((lvl) => (
                  <option key={lvl} value={lvl}>
                    Nivel {lvl} (lista plana)
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
        </ZHFilterBar>
        <p className="acc-coa-list-summary-text subtle" aria-live="polite">
          {listSummary}
        </p>
        <ZHDataTable
          columns={columns}
          rows={rows}
          rowKey={(row) => row.account.id}
          rowClassName={(row) => (row.isContext ? "acc-coa-row--context" : undefined)}
          loading={loading}
          tableClassName="table--compact"
          emptyMessage={hasFilters ? "Ninguna cuenta coincide con los filtros." : "No hay cuentas registradas."}
        />
      </ZHCard>

      {intent && <AccountFormModal intent={intent} tree={tree} onClose={closeModal} onSaved={(a) => void handleSaved(a)} />}
    </PageShell>
  );
}
