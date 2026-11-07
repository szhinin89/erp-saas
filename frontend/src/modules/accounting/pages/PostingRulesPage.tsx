import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountDto,
  type PostingFactCatalogItemDto,
  type PostingRuleDto,
} from "../api/accountingApi";
import { PostingRuleEditorModal, type PostingRuleEditorState } from "./PostingRuleEditorModal";
import { postingOperationLabel, sourceModuleLabel } from "../labels/accountingLabels";
import { buildPostingRuleRows, type PostingRuleRow } from "./postingRules/postingRuleView";
import { PostingRulesListTab, type PostingRuleFilters } from "./postingRules/PostingRulesListTab";
import { PostingRuleDetailTab } from "./postingRules/PostingRuleDetailTab";

import "../../../styles/shared/items-catalog.css";

const MANAGE_PERMISSION = "accounting.rules.manage";
const NO_FILTERS: PostingRuleFilters = { sourceModule: "", factType: "", status: "" };

/**
 * Contabilidad → Configuración → Reglas contables (ACCOUNTING-POSTING-RULES-UI-12; administración
 * ACCOUNTING-11; presentación ACCOUNTING-POSTING-RULES-UX-01).
 * Auditoría de reutilización: patrón Lista→Detalle de `ConfigTabsLayout` (Sucursales/Cajas), con
 * `ZHFilterBar` + una sola `ZHDataTable` (fila seleccionada `cfg-row--selected`), detalle con
 * `ZHCard`/`ZHInfoRow`/`ZHDataTable`/`Badge`/`ZHPageNotice` y ayuda oficial `ZHFieldHelp`. Sin
 * componentes, CSS ni estilos nuevos. Lectura con `accounting.view`; crear la regla de un hecho sin
 * regla (`GET posting-rules/catalog`), reemplazar líneas (`PostingRuleEditorModal`, sin cambios) y
 * activar/desactivar con `accounting.rules.manage`. Nunca claves libres: solo hechos soportados.
 * Accesible solo desde el menú (`AccountingModule.cs`), sin botones cruzados.
 */
export function PostingRulesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canManage = canShow(MANAGE_PERMISSION);
  const [rules, setRules] = useState<PostingRuleDto[]>([]);
  const [catalog, setCatalog] = useState<PostingFactCatalogItemDto[]>([]);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editor, setEditor] = useState<PostingRuleEditorState | null>(null);
  const [filters, setFilters] = useState<PostingRuleFilters>(NO_FILTERS);
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const fetchRules = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const [list, facts, accountList] = await Promise.all([
        accountingApi.listPostingRules(),
        accountingApi.getPostingFactCatalog(),
        accountingApi.listAccounts(),
      ]);
      setRules(list);
      setCatalog(facts);
      setAccounts(accountList);
    } catch (err: unknown) {
      setLoadError(formatApiRequestError(err, { generic: t("accounting.postingRules.error.load") }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void fetchRules();
  }, [fetchRules]);

  const rows = useMemo(() => buildPostingRuleRows(catalog, rules, t), [catalog, rules, t]);
  const selectedRow = useMemo(() => rows.find((r) => r.key === selectedKey) ?? null, [rows, selectedKey]);

  // Módulos y hechos derivados de las filas reales (catálogo + reglas), no de una lista fija.
  const moduleOptions = useMemo(() => {
    const modules = Array.from(new Set(rows.map((r) => r.sourceModule))).sort();
    return [
      { value: "", label: t("accounting.postingRules.filter.allModules") },
      ...modules.map((m) => ({ value: m, label: sourceModuleLabel(t, m) })),
    ];
  }, [rows, t]);

  const factTypeOptions = useMemo(() => {
    const scoped = filters.sourceModule ? rows.filter((r) => r.sourceModule === filters.sourceModule) : rows;
    return [
      { value: "", label: t("accounting.postingRules.filter.allFactTypes") },
      ...scoped.map((r) => ({ value: r.key, label: postingOperationLabel(t, r.sourceModule, r.factType) })),
    ];
  }, [rows, filters.sourceModule, t]);

  const statusOptions = useMemo(
    () => [
      { value: "", label: t("accounting.postingRules.filter.allStatuses") },
      { value: "active", label: t("accounting.postingRules.status.active") },
      { value: "inactive", label: t("accounting.postingRules.status.inactive") },
      { value: "missing", label: t("accounting.postingRules.status.missing") },
      { value: "unsupported", label: t("accounting.postingRules.status.unsupported") },
    ],
    [t],
  );

  const filteredRows = useMemo(
    () =>
      rows
        .filter((r) => (filters.sourceModule ? r.sourceModule === filters.sourceModule : true))
        .filter((r) => (filters.factType ? r.key === filters.factType : true))
        .filter((r) => (filters.status ? r.status === filters.status : true)),
    [rows, filters],
  );

  const handleSelect = (row: PostingRuleRow) => {
    setSelectedKey(row.key);
    setActiveTab("editor");
  };

  const handleConfigure = (row: PostingRuleRow) => {
    if (!row.fact) return;
    setSelectedKey(row.key);
    setEditor({ mode: "create", fact: row.fact });
  };

  const handleEdit = (row: PostingRuleRow) => {
    if (row.fact && row.rule) setEditor({ mode: "edit", fact: row.fact, rule: row.rule });
  };

  async function handleToggle(row: PostingRuleRow) {
    const rule = row.rule;
    if (!rule) return;
    if (rule.isActive) {
      const confirmed = await message.confirm({
        title: t("accounting.postingRules.disable.confirmTitle"),
        message: t("accounting.postingRules.disable.confirmMessage"),
        confirmLabel: t("accounting.postingRules.action.disable"),
        variant: "danger",
      });
      if (!confirmed) return;
    }
    setBusy(true);
    try {
      if (rule.isActive) await accountingApi.disablePostingRule(rule.id);
      else await accountingApi.enablePostingRule(rule.id);
      message.success(
        rule.isActive ? t("accounting.postingRules.disable.done") : t("accounting.postingRules.enable.done"),
      );
      await fetchRules();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.postingRules.editor.error") }));
    } finally {
      setBusy(false);
    }
  }

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.postingRules.title")}
      subtitle={t("accounting.postingRules.subtitle")}
    >
      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={t("accounting.postingRules.tab.detail")}
        editorIcon="fact_check"
        error={
          loadError ? (
            <ZHPageNotice variant="error" message={t("accounting.postingRules.error.load")} detail={loadError} />
          ) : undefined
        }
        listContent={
          <PostingRulesListTab
            rows={filteredRows}
            totalRows={rows.length}
            loading={loading}
            busy={busy}
            canManage={canManage}
            selectedKey={selectedKey}
            filters={filters}
            moduleOptions={moduleOptions}
            factTypeOptions={factTypeOptions}
            statusOptions={statusOptions}
            onFiltersChange={setFilters}
            onClearFilters={() => setFilters(NO_FILTERS)}
            onRefresh={() => void fetchRules()}
            onSelect={handleSelect}
            onConfigure={handleConfigure}
            t={t}
          />
        }
        editorContent={
          <PostingRuleDetailTab
            row={selectedRow}
            canManage={canManage}
            busy={busy}
            onEdit={handleEdit}
            onToggle={(r) => void handleToggle(r)}
            onConfigure={handleConfigure}
            onBack={() => setActiveTab("list")}
            t={t}
          />
        }
      />

      {editor && (
        <PostingRuleEditorModal
          state={editor}
          accounts={accounts}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null);
            void fetchRules();
          }}
          t={t}
        />
      )}
    </PageShell>
  );
}
