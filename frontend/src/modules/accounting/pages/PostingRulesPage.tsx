import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountDto,
  type PostingFactCatalogItemDto,
  type PostingRuleDto,
  type PostingRuleLineDto,
} from "../api/accountingApi";
import { PostingRuleEditorModal, type PostingRuleEditorState } from "./PostingRuleEditorModal";
import {
  accountNatureLabel,
  accountTypeLabel,
  amountKindLabel,
  factTypeLabel,
  lineDirectionLabel,
  sourceModuleLabel,
  type TFunction,
} from "../labels/accountingLabels";

import "../../../styles/shared/items-catalog.css";

const MANAGE_PERMISSION = "accounting.rules.manage";

const factKey = (sourceModule: string, factType: string) => `${sourceModule}/${factType}`;

interface RuleProblem {
  label: string;
}

function findRuleProblems(
  rule: PostingRuleDto,
  fact: PostingFactCatalogItemDto | undefined,
  t: TFunction,
): RuleProblem[] {
  const problems: RuleProblem[] = [];
  if (!fact) {
    return [{ label: t("accounting.postingRules.problem.unsupported") }];
  }
  // ACCOUNTING-11: misma forma que valida el backend (PostingFactCatalog) — un lado dinámico lo
  // completa el origen; una regla habilitadora no lleva líneas fijas.
  if (fact.allowedAmountKinds.length === 0) return problems;
  const hasDynamic = fact.dynamicDebit || fact.dynamicCredit;
  if (!hasDynamic && rule.lines.length < 2) {
    problems.push({ label: t("accounting.postingRules.problem.lessThanTwoLines") });
  }
  const debitLines = rule.lines.filter((l) => l.nature === "Debit");
  const creditLines = rule.lines.filter((l) => l.nature === "Credit");
  if (debitLines.length === 0 && !fact.dynamicDebit) {
    problems.push({ label: t("accounting.postingRules.problem.noDebitLines") });
  }
  if (creditLines.length === 0 && !fact.dynamicCredit) {
    problems.push({ label: t("accounting.postingRules.problem.noCreditLines") });
  }
  const badAccounts = rule.lines.filter((l) => !l.accountIsActive || !l.accountAllowsPosting);
  for (const l of badAccounts) {
    if (!l.accountIsActive) {
      problems.push({
        label: t("accounting.postingRules.problem.inactiveAccount", {
          code: l.accountCode,
          name: l.accountName,
        }),
      });
    }
    if (!l.accountAllowsPosting) {
      problems.push({
        label: t("accounting.postingRules.problem.nonPostableAccount", {
          code: l.accountCode,
          name: l.accountName,
        }),
      });
    }
  }
  return problems;
}

function createLineColumns(t: TFunction): ZHDataTableColumn<PostingRuleLineDto>[] {
  return [
    {
      key: "account",
      header: t("accounting.postingRules.column.account"),
      render: (row) => (
        <span>
          <code className="prd-sku">{row.accountCode}</code> {row.accountName}
        </span>
      ),
    },
    {
      key: "accountType",
      header: t("accounting.postingRules.column.accountType"),
      render: (row) => accountTypeLabel(t, row.accountType),
    },
    {
      key: "accountNature",
      header: t("accounting.postingRules.column.accountNature"),
      render: (row) => accountNatureLabel(t, row.accountNature),
    },
    {
      key: "amountKind",
      header: t("accounting.postingRules.column.amountKind"),
      render: (row) => amountKindLabel(t, row.amountKind),
    },
    {
      key: "accountIsActive",
      header: t("accounting.postingRules.column.accountIsActive"),
      align: "center",
      render: (row) => (
        <Badge
          label={row.accountIsActive ? t("common.yes") : t("common.no")}
          variant={row.accountIsActive ? "green" : "red"}
        />
      ),
    },
    {
      key: "accountAllowsPosting",
      header: t("accounting.postingRules.column.accountAllowsPosting"),
      align: "center",
      render: (row) => (
        <Badge
          label={row.accountAllowsPosting ? t("common.yes") : t("common.no")}
          variant={row.accountAllowsPosting ? "green" : "red"}
        />
      ),
    },
  ];
}

interface PostingRuleCardProps {
  rule: PostingRuleDto;
  fact: PostingFactCatalogItemDto | undefined;
  canManage: boolean;
  busy: boolean;
  onEdit: (rule: PostingRuleDto) => void;
  onToggle: (rule: PostingRuleDto) => void;
  t: TFunction;
}

function PostingRuleCard({ rule, fact, canManage, busy, onEdit, onToggle, t }: PostingRuleCardProps) {
  const debitLines = rule.lines
    .filter((l) => l.nature === "Debit")
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const creditLines = rule.lines
    .filter((l) => l.nature === "Credit")
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const problems = findRuleProblems(rule, fact, t);
  const editable = canManage && !!fact && fact.allowedAmountKinds.length > 0;
  const lineColumns = createLineColumns(t);

  return (
    <ZHCard
      title={
        <span>
          <strong>{sourceModuleLabel(t, rule.sourceModule)}</strong> / {factTypeLabel(t, rule.factType)}
        </span>
      }
      actions={
        <div className="zh-form-actions-row">
          <Badge
            label={
              rule.isActive
                ? t("accounting.postingRules.status.active")
                : t("accounting.postingRules.status.inactive")
            }
            variant={rule.isActive ? "green" : "gray"}
          />
          <Badge label={t("accounting.postingRules.linesCount", { count: rule.lines.length })} variant="blue" />
          {editable && (
            <ZHBtn variant="ghost" size="sm" type="button" disabled={busy} onClick={() => onEdit(rule)}>
              {t("accounting.postingRules.action.edit")}
            </ZHBtn>
          )}
          {canManage && (rule.isActive || fact) && (
            <ZHBtn
              variant={rule.isActive ? "destructive" : "ghost"}
              size="sm"
              type="button"
              disabled={busy}
              onClick={() => onToggle(rule)}
            >
              {rule.isActive ? t("accounting.postingRules.action.disable") : t("accounting.postingRules.action.enable")}
            </ZHBtn>
          )}
        </div>
      }
    >
      {problems.length > 0 && (
        <ZHPageNotice
          variant="warning"
          message={t("accounting.postingRules.problem.message")}
          detail={problems.map((p) => p.label).join(" · ")}
        />
      )}

      {fact?.allowedAmountKinds.length === 0 && (
        <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.enabling")} />
      )}
      {fact?.dynamicDebit && fact.allowedAmountKinds.length > 0 && (
        <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.debit")} />
      )}
      {fact?.dynamicCredit && fact.allowedAmountKinds.length > 0 && (
        <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.credit")} />
      )}

      <div className="zh-mb-16">
        <h4>{lineDirectionLabel(t, "Debit")}</h4>
        <ZHDataTable
          columns={lineColumns}
          rows={debitLines}
          rowKey={(row) => row.id}
          emptyMessage={t("accounting.postingRules.empty.debitLines")}
        />
      </div>

      <div>
        <h4>{lineDirectionLabel(t, "Credit")}</h4>
        <ZHDataTable
          columns={lineColumns}
          rows={creditLines}
          rowKey={(row) => row.id}
          emptyMessage={t("accounting.postingRules.empty.creditLines")}
        />
      </div>
    </ZHCard>
  );
}

/**
 * Contabilidad → Reglas contables (ACCOUNTING-POSTING-RULES-UI-12; administración ACCOUNTING-11).
 * Auditoría de reutilización: revisadas `ChartOfAccountsPage.tsx`, `AccountingPeriodsPage.tsx`
 * (permiso por acción con `usePermissionsUi`, `ZHModal`, `message.confirm`) y
 * `ManualJournalEditorPage.tsx` (editor de líneas RHF+Zod+useFieldArray). Reutiliza PageShell/
 * ZHCard/ZHFilterBar/ZHField/ZhSelect/ZHDataTable/ZHModal/Badge/ZHPageNotice — sin componentes
 * nuevos. Lectura con `accounting.view`; crear la regla de un hecho faltante del catálogo
 * (`GET posting-rules/catalog`), reemplazar líneas y activar/desactivar con
 * `accounting.rules.manage`. Nunca claves libres: solo hechos soportados. Accesible solo desde el
 * menú (`AccountingModule.cs`), sin botones cruzados (ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C).
 */
export function PostingRulesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canManage = canShow(MANAGE_PERMISSION);
  const [rules, setRules] = useState<PostingRuleDto[]>([]);
  const [catalog, setCatalog] = useState<PostingFactCatalogItemDto[]>([]);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [editor, setEditor] = useState<PostingRuleEditorState | null>(null);
  const [sourceModule, setSourceModule] = useState("");
  const [factType, setFactType] = useState("");
  const [status, setStatus] = useState("");

  const factsByKey = useMemo(
    () => new Map(catalog.map((f) => [factKey(f.sourceModule, f.factType), f])),
    [catalog],
  );
  const missingFacts = useMemo(() => catalog.filter((f) => f.ruleId === null), [catalog]);

  // ACCOUNTING-11: módulos derivados del catálogo y de las reglas existentes (SSOT dinámico), no una lista fija.
  const sourceModuleOptions = useMemo(() => {
    const modules = Array.from(new Set([...catalog, ...rules].map((r) => r.sourceModule))).sort();
    return [
      { value: "", label: t("accounting.postingRules.filter.allModules") },
      ...modules.map((m) => ({ value: m, label: sourceModuleLabel(t, m) })),
    ];
  }, [catalog, rules, t]);

  const statusOptions = useMemo(
    () => [
      { value: "", label: t("accounting.postingRules.filter.allStatuses") },
      { value: "active", label: t("accounting.postingRules.status.active") },
      { value: "inactive", label: t("accounting.postingRules.status.inactive") },
    ],
    [t],
  );

  const fetchRules = useCallback(async () => {
    setLoading(true);
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
      message.error(
        formatApiRequestError(err, { generic: t("accounting.postingRules.error.load") }),
      );
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void fetchRules();
  }, [fetchRules]);

  const factTypeOptions = useMemo(() => {
    const scoped = sourceModule ? rules.filter((r) => r.sourceModule === sourceModule) : rules;
    const unique = Array.from(new Set(scoped.map((r) => r.factType))).sort();
    return [
      { value: "", label: t("accounting.postingRules.filter.allFactTypes") },
      ...unique.map((f) => ({ value: f, label: factTypeLabel(t, f) })),
    ];
  }, [rules, sourceModule, t]);

  const filteredRules = useMemo(() => {
    return rules
      .filter((r) => (sourceModule ? r.sourceModule === sourceModule : true))
      .filter((r) => (factType ? r.factType === factType : true))
      .filter((r) => {
        if (status === "active") return r.isActive;
        if (status === "inactive") return !r.isActive;
        return true;
      })
      .sort((a, b) => a.sourceModule.localeCompare(b.sourceModule) || a.factType.localeCompare(b.factType));
  }, [rules, sourceModule, factType, status]);

  const handleClearFilters = () => {
    setSourceModule("");
    setFactType("");
    setStatus("");
  };

  const handleEdit = (rule: PostingRuleDto) => {
    const fact = factsByKey.get(factKey(rule.sourceModule, rule.factType));
    if (fact) setEditor({ mode: "edit", fact, rule });
  };

  async function handleToggle(rule: PostingRuleDto) {
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

  const missingColumns: ZHDataTableColumn<PostingFactCatalogItemDto>[] = [
    {
      key: "module",
      header: t("accounting.postingRules.filter.sourceModule"),
      render: (r) => sourceModuleLabel(t, r.sourceModule),
    },
    {
      key: "fact",
      header: t("accounting.postingRules.filter.factType"),
      render: (r) => factTypeLabel(t, r.factType),
    },
    {
      key: "actions",
      header: t("common.actions"),
      cellClassName: "zh-table-cell--actions",
      render: (r) =>
        canManage ? (
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            disabled={busy}
            onClick={() => setEditor({ mode: "create", fact: r })}
          >
            {t("accounting.postingRules.action.configure")}
          </ZHBtn>
        ) : null,
    },
  ];

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.postingRules.title")}
      subtitle={t("accounting.postingRules.subtitle")}
    >
      {!loading && missingFacts.length > 0 && (
        <ZHCard title={t("accounting.postingRules.missing.title")} className="zh-mb-16">
          <ZHPageNotice variant="warning" message={t("accounting.postingRules.missing.message")} />
          <ZHDataTable
            columns={missingColumns}
            rows={missingFacts}
            rowKey={(r) => factKey(r.sourceModule, r.factType)}
          />
        </ZHCard>
      )}

      <ZHCard title={t("accounting.postingRules.filtersTitle")}>
        <ZHFilterBar onClear={handleClearFilters} disabled={loading}>
          <div className="zh-filterbar__field">
            <ZHField label={t("accounting.postingRules.filter.sourceModule")} density="compact">
              <ZhSelect
                value={sourceModule}
                disabled={loading}
                onChange={(e) => {
                  setSourceModule(e.target.value);
                  setFactType("");
                }}
              >
                {sourceModuleOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label={t("accounting.postingRules.filter.factType")} density="compact">
              <ZhSelect value={factType} disabled={loading} onChange={(e) => setFactType(e.target.value)}>
                {factTypeOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label={t("common.status")} density="compact">
              <ZhSelect value={status} disabled={loading} onChange={(e) => setStatus(e.target.value)}>
                {statusOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchRules()} disabled={loading}>
              {t("common.refresh")}
            </ZHBtn>
          </div>
        </ZHFilterBar>
      </ZHCard>

      {!loading && rules.length === 0 && (
        <ZHPageNotice
          variant="warning"
          message={t("accounting.postingRules.empty.noRules")}
        />
      )}

      {!loading && rules.length > 0 && filteredRules.length === 0 && (
        <ZHPageNotice variant="info" message={t("accounting.postingRules.empty.noFilterResults")} />
      )}

      {filteredRules.map((rule) => (
        <PostingRuleCard
          key={rule.id}
          rule={rule}
          fact={factsByKey.get(factKey(rule.sourceModule, rule.factType))}
          canManage={canManage}
          busy={busy}
          onEdit={handleEdit}
          onToggle={(r) => void handleToggle(r)}
          t={t}
        />
      ))}

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
