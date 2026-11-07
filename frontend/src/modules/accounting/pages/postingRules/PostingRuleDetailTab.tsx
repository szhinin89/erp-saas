import type { ReactNode } from "react";
import { Badge } from "../../../../components/PageShell";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHInfoRow } from "../../../../components/zh/ZHInfoRow";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { HELP_KEYS } from "../../../../help";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import {
  accountNatureLabel,
  accountTypeLabel,
  amountKindLabel,
  lineDirectionLabel,
  postingOperationLabel,
  sourceModuleLabel,
  type TFunction,
} from "../../labels/accountingLabels";
import { buildEntryRows, postingRuleTypeLabel, type PostingEntryRow, type PostingRuleRow } from "./postingRuleView";
import { PostingRuleStatusBadge } from "./postingRuleBadges";

interface PostingRuleDetailTabProps {
  row: PostingRuleRow | null;
  canManage: boolean;
  busy: boolean;
  onEdit: (row: PostingRuleRow) => void;
  onToggle: (row: PostingRuleRow) => void;
  onConfigure: (row: PostingRuleRow) => void;
  onBack: () => void;
  t: TFunction;
}

function entryColumns(t: TFunction): ZHDataTableColumn<PostingEntryRow>[] {
  return [
    {
      key: "side",
      header: t("accounting.postingRules.column.direction"),
      render: (r) => <ZHDataValue variant="strong">{lineDirectionLabel(t, r.side)}</ZHDataValue>,
    },
    {
      key: "account",
      header: t("accounting.postingRules.column.account"),
      render: (r) =>
        r.origin === "fixed" ? (
          <div className="zh-stack">
            <ZHDataValue>
              <ZHDataValue variant="code">{r.line.accountCode}</ZHDataValue> {r.line.accountName}
            </ZHDataValue>
            <ZHDataValue variant="muted">
              {accountTypeLabel(t, r.line.accountType)} · {accountNatureLabel(t, r.line.accountNature)}
            </ZHDataValue>
          </div>
        ) : (
          <ZHDataValue variant="muted">{r.description}</ZHDataValue>
        ),
    },
    {
      key: "amountKind",
      header: t("accounting.postingRules.column.amountKind"),
      render: (r) =>
        r.origin === "fixed" ? (
          amountKindLabel(t, r.line.amountKind)
        ) : (
          <ZHDataValue variant="muted">{t("accounting.postingRules.entry.sourceAmount")}</ZHDataValue>
        ),
    },
    {
      key: "origin",
      header: t("accounting.postingRules.column.origin"),
      render: (r) =>
        r.origin === "fixed" ? (
          <Badge variant="info" label={t("accounting.postingRules.entry.origin.fixed")} />
        ) : (
          <Badge variant="neutral" label={t("accounting.postingRules.entry.origin.source")} />
        ),
    },
    {
      key: "issues",
      header: t("accounting.postingRules.column.issues"),
      render: (r) =>
        r.origin === "fixed" && r.issues.length > 0 ? (
          <div className="zh-form-actions-row">
            {r.issues.map((issue) => (
              <Badge key={issue} variant="error" label={issue} />
            ))}
          </div>
        ) : null,
    },
  ];
}

function Info({ label, value, wide }: { label: string; value: ReactNode; wide?: boolean }) {
  return <ZHInfoRow wide={wide} label={<ZHFieldLabel size="sm">{label}</ZHFieldLabel>} value={value} />;
}

/** ACCOUNTING-POSTING-RULES-UX-01 — pestaña Detalle: resumen de la operación y el asiento que genera. */
export function PostingRuleDetailTab({
  row,
  canManage,
  busy,
  onEdit,
  onToggle,
  onConfigure,
  onBack,
  t,
}: PostingRuleDetailTabProps) {
  if (!row) {
    return (
      <div className="cfg-tabs-empty">
        <span className="material-symbols-outlined cfg-empty-panel__icon">fact_check</span>
        <p className="cfg-empty-panel__title">{t("accounting.postingRules.detail.emptyTitle")}</p>
        <p className="cfg-empty-panel__sub">{t("accounting.postingRules.detail.emptySub")}</p>
      </div>
    );
  }

  const { rule, fact } = row;
  // Mismas condiciones que la versión anterior de la pantalla (ACCOUNTING-11): editar solo hechos
  // soportados con líneas fijas; reactivar solo hechos soportados; crear solo hechos sin regla.
  const canEdit = canManage && !!rule && !!fact && fact.allowedAmountKinds.length > 0;
  const canToggle = canManage && !!rule && (rule.isActive || !!fact);
  const canConfigure = canManage && !rule && !!fact;
  const entryRows = buildEntryRows(row, t);
  const lastChange = rule ? (rule.updatedAt ?? rule.createdAt) : null;
  // El estado real de la regla (una no soportada también está activa o inactiva).
  let ruleStatusKey = "accounting.postingRules.status.missing";
  if (rule) {
    ruleStatusKey = rule.isActive ? "accounting.postingRules.status.active" : "accounting.postingRules.status.inactive";
  }

  return (
    <>
      <ZHCard
        className="zh-mb-16"
        title={postingOperationLabel(t, row.sourceModule, row.factType)}
        actions={
          <div className="zh-form-actions-row">
            <PostingRuleStatusBadge status={row.status} t={t} />
            <ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_POSTING_RULES_STATUS} />
            {canEdit && (
              <ZHBtn variant="ghost" size="sm" type="button" disabled={busy} onClick={() => onEdit(row)}>
                {t("accounting.postingRules.action.edit")}
              </ZHBtn>
            )}
            {canToggle && rule && (
              <ZHBtn
                variant={rule.isActive ? "destructive" : "ghost"}
                size="sm"
                type="button"
                disabled={busy}
                onClick={() => onToggle(row)}
              >
                {rule.isActive
                  ? t("accounting.postingRules.action.disable")
                  : t("accounting.postingRules.action.enable")}
              </ZHBtn>
            )}
            {canConfigure && (
              <ZHBtn size="sm" type="button" disabled={busy} onClick={() => onConfigure(row)}>
                {t("accounting.postingRules.action.configure")}
              </ZHBtn>
            )}
          </div>
        }
      >
        {row.status === "missing" && (
          <ZHPageNotice variant="warning" message={t("accounting.postingRules.missing.message")} />
        )}
        {row.problems.length > 0 && (
          <ZHPageNotice
            variant="warning"
            message={t("accounting.postingRules.problem.message")}
            detail={row.problems.join(" · ")}
          />
        )}
        <div className="zh-grid zh-grid--2">
          <div>
            <Info label={t("accounting.postingRules.filter.sourceModule")} value={sourceModuleLabel(t, row.sourceModule)} />
            <Info label={t("accounting.postingRules.column.ruleType")} value={postingRuleTypeLabel(t, row.ruleType)} />
            <Info
              label={t("accounting.postingRules.detail.ruleStatus")}
              value={t(ruleStatusKey)}
            />
          </div>
          <div>
            <Info
              label={t("accounting.postingRules.detail.technicalKey")}
              value={<ZHDataValue variant="code">{row.key}</ZHDataValue>}
            />
            {fact && (
              <Info
                wide
                label={t("accounting.postingRules.detail.amountKinds")}
                value={
                  fact.allowedAmountKinds.length > 0
                    ? fact.allowedAmountKinds.map((k) => amountKindLabel(t, k)).join(" · ")
                    : t("accounting.postingRules.detail.noAmountKinds")
                }
              />
            )}
            {lastChange && <Info label={t("accounting.postingRules.detail.lastChange")} value={formatDateTime(lastChange)} />}
          </div>
        </div>
      </ZHCard>

      <ZHCard
        className="zh-mb-16"
        title={t("accounting.postingRules.entry.title")}
        actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_POSTING_RULES_ENTRY} />}
      >
        <ZHDataTable
          columns={entryColumns(t)}
          rows={entryRows}
          rowKey={(r) => r.key}
          tableClassName="table--compact"
          emptyMessage={t("accounting.postingRules.entry.empty")}
        />
      </ZHCard>

      <ZHBtn variant="ghost" size="sm" type="button" onClick={onBack}>
        {t("accounting.postingRules.detail.back")}
      </ZHBtn>
    </>
  );
}
