import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { accountingApi, type PostingRuleDto, type PostingRuleLineDto } from "../api/accountingApi";
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

interface RuleProblem {
  label: string;
}

function findRuleProblems(rule: PostingRuleDto, t: TFunction): RuleProblem[] {
  const problems: RuleProblem[] = [];
  if (rule.lines.length < 2) {
    problems.push({ label: t("accounting.postingRules.problem.lessThanTwoLines") });
  }
  const debitLines = rule.lines.filter((l) => l.nature === "Debit");
  const creditLines = rule.lines.filter((l) => l.nature === "Credit");
  if (debitLines.length === 0) {
    problems.push({ label: t("accounting.postingRules.problem.noDebitLines") });
  }
  if (creditLines.length === 0) {
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

function PostingRuleCard({ rule, t }: { rule: PostingRuleDto; t: TFunction }) {
  const debitLines = rule.lines
    .filter((l) => l.nature === "Debit")
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const creditLines = rule.lines
    .filter((l) => l.nature === "Credit")
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const problems = findRuleProblems(rule, t);
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
 * Contabilidad → Reglas contables (ACCOUNTING-POSTING-RULES-UI-12). Auditoría de reutilización:
 * revisadas `ChartOfAccountsPage.tsx` (mismo módulo, filtros vía `ZHCard actions` para 2-3
 * campos) y `ElectronicDocumentsFilters.tsx` (referencia real de `ZHFilterBar`+`ZHField
 * density="compact"` para una barra de filtros con varios campos — usada aquí en vez del slot
 * `actions` de `ZHCard` porque son 3 filtros independientes). Reutiliza PageShell/ZHCard/
 * ZHFilterBar/ZHField/ZhSelect/ZHDataTable/Badge/ZHPageNotice — sin componentes nuevos. Solo
 * lectura: consume exclusivamente `GET /api/v1/accounting/posting-rules`
 * (ACCOUNTING-POSTING-RULES-SEED-11B ya sembró las reglas; esta pantalla no crea/edita/deshabilita
 * ninguna, aunque el endpoint ya lo admite — ver ticket, fuera de alcance). Pantalla principal,
 * accesible solo desde el menú (`AccountingModule.cs`) — sin botones cruzados hacia Asientos
 * contables/Plan de cuentas/Reportes (navegación duplicada del menú, mismo criterio de
 * ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C).
 */
export function PostingRulesPage() {
  const { t } = useI18n();
  const [rules, setRules] = useState<PostingRuleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [sourceModule, setSourceModule] = useState("");
  const [factType, setFactType] = useState("");
  const [status, setStatus] = useState("");

  const sourceModuleOptions = useMemo(
    () => [
      { value: "", label: t("accounting.postingRules.filter.allModules") },
      { value: "Sales", label: sourceModuleLabel(t, "Sales") },
      { value: "Purchases", label: sourceModuleLabel(t, "Purchases") },
      { value: "Finance", label: sourceModuleLabel(t, "Finance") },
    ],
    [t],
  );

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
      const list = await accountingApi.listPostingRules();
      setRules(list);
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

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.postingRules.title")}
      subtitle={t("accounting.postingRules.subtitle")}
    >
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
        <PostingRuleCard key={rule.id} rule={rule} t={t} />
      ))}
    </PageShell>
  );
}
