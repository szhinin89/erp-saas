import type { MouseEvent } from "react";
import { Badge } from "../../../../components/PageShell";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZhSelect } from "../../../../components/zh/inputs";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { HELP_KEYS } from "../../../../help";
import { postingOperationLabel, sourceModuleLabel, type TFunction } from "../../labels/accountingLabels";
import { postingRuleTypeLabel, type PostingRuleRow } from "./postingRuleView";
import { PostingRuleStatusBadge } from "./postingRuleBadges";

export interface PostingRuleFilters {
  sourceModule: string;
  factType: string;
  status: string;
}

interface Option {
  value: string;
  label: string;
}

interface PostingRulesListTabProps {
  rows: PostingRuleRow[];
  totalRows: number;
  loading: boolean;
  busy: boolean;
  canManage: boolean;
  selectedKey: string | null;
  filters: PostingRuleFilters;
  moduleOptions: Option[];
  factTypeOptions: Option[];
  statusOptions: Option[];
  onFiltersChange: (filters: PostingRuleFilters) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
  onSelect: (row: PostingRuleRow) => void;
  onConfigure: (row: PostingRuleRow) => void;
  t: TFunction;
}

/** ACCOUNTING-POSTING-RULES-UX-01 — pestaña Lista: filtros + una sola tabla de operaciones. */
export function PostingRulesListTab({
  rows,
  totalRows,
  loading,
  busy,
  canManage,
  selectedKey,
  filters,
  moduleOptions,
  factTypeOptions,
  statusOptions,
  onFiltersChange,
  onClearFilters,
  onRefresh,
  onSelect,
  onConfigure,
  t,
}: PostingRulesListTabProps) {
  const columns: ZHDataTableColumn<PostingRuleRow>[] = [
    {
      key: "operation",
      header: t("accounting.postingRules.column.operation"),
      render: (row) => (
        <ZHDataValue variant="strong">{postingOperationLabel(t, row.sourceModule, row.factType)}</ZHDataValue>
      ),
    },
    {
      key: "module",
      header: t("accounting.postingRules.column.sourceModule"),
      render: (row) => sourceModuleLabel(t, row.sourceModule),
    },
    {
      key: "ruleType",
      header: t("accounting.postingRules.column.ruleType"),
      render: (row) => postingRuleTypeLabel(t, row.ruleType),
    },
    {
      key: "status",
      header: t("common.status"),
      render: (row) => <PostingRuleStatusBadge status={row.status} t={t} />,
    },
    {
      key: "diagnosis",
      header: t("accounting.postingRules.column.diagnosis"),
      render: (row) =>
        row.problems.length > 0 ? (
          <Badge
            variant="warning"
            label={t("accounting.postingRules.diagnosis.count", { count: row.problems.length })}
            title={row.problems.join(" · ")}
          />
        ) : null,
    },
    {
      key: "actions",
      header: t("common.actions"),
      cellClassName: "zh-table-cell--actions",
      render: (row) => {
        const configure = canManage && row.status === "missing";
        return (
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            disabled={busy}
            onClick={(e: MouseEvent<HTMLButtonElement>) => {
              e.stopPropagation();
              if (configure) onConfigure(row);
              else onSelect(row);
            }}
          >
            {configure ? t("accounting.postingRules.action.configure") : t("accounting.postingRules.action.view")}
          </ZHBtn>
        );
      },
    },
  ];

  const set = (patch: Partial<PostingRuleFilters>) => onFiltersChange({ ...filters, ...patch });

  return (
    <ZHCard
      title={t("accounting.postingRules.listTitle")}
      actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_POSTING_RULES_OVERVIEW} />}
    >
      <ZHFilterBar onClear={onClearFilters} disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField label={t("accounting.postingRules.filter.sourceModule")} density="compact">
            <ZhSelect
              value={filters.sourceModule}
              disabled={loading}
              onChange={(e) => set({ sourceModule: e.target.value, factType: "" })}
            >
              {moduleOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField label={t("accounting.postingRules.filter.factType")} density="compact">
            <ZhSelect
              value={filters.factType}
              disabled={loading}
              onChange={(e) => set({ factType: e.target.value })}
            >
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
            <ZhSelect value={filters.status} disabled={loading} onChange={(e) => set({ status: e.target.value })}>
              {statusOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHBtn variant="ghost" size="sm" type="button" onClick={onRefresh} disabled={loading}>
            {t("common.refresh")}
          </ZHBtn>
        </div>
      </ZHFilterBar>

      <ZHDataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.key}
        loading={loading}
        onRowClick={onSelect}
        rowClassName={(row) => (row.key === selectedKey ? "cfg-row--selected" : undefined)}
        emptyMessage={
          totalRows === 0
            ? t("accounting.postingRules.empty.noRules")
            : t("accounting.postingRules.empty.noFilterResults")
        }
      />
    </ZHCard>
  );
}
