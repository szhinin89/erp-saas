import { useCallback, useEffect, useMemo, useState } from "react";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  accountingApi,
  type AccountingCompleteness,
  type FinancialStatementLineDto,
  type GetBalanceSheetReportResponse,
} from "../../api/accountingApi";
import { todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";

/** ACCOUNTING-11 — fila de Patrimonio: cuenta real o fila calculada (sin cuenta ni link). */
type EquityRow = FinancialStatementLineDto & { calculated?: boolean };

const LINE_COLUMNS: ZHDataTableColumn<EquityRow>[] = [
  {
    key: "accountCode",
    header: "Código",
    render: (r) => (r.calculated ? <em>{r.accountCode}</em> : <code className="prd-sku">{r.accountCode}</code>),
  },
  { key: "accountName", header: "Cuenta", render: (r) => r.accountName },
  { key: "amount", header: "Saldo", align: "right", render: (r) => <ZHMoneyValue precision="accounting" value={r.amount} /> },
];

/**
 * Balance General (ACCOUNTING-FINANCIAL-STATEMENTS-10) — consume
 * `GET /accounting/reports/balance-sheet`. Saldo acumulado (desde el inicio del historial Posted)
 * de Activos/Pasivos/Patrimonio hasta la fecha de corte. Solo lectura, sin recálculo.
 * ACCOUNTING-11: Patrimonio incluye las filas calculadas por el backend (resultado del ejercicio y
 * resultados no cerrados de ejercicios anteriores, sin asiento), así que el aviso de descuadre
 * solo aparece ante un descuadre real.
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E: mismo ajuste de filtros/secciones/totales que Estado de
 * Resultados (`ZHFilterBar`, `ZHCard` anidado con `zh-section-title`, `ZHMoneyValue`).
 */
export function BalanceSheetReportTab() {
  const [asOfDate, setAsOfDate] = useState(todayIso());
  const { t } = useI18n();
  const [data, setData] = useState<GetBalanceSheetReportResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [completeness, setCompleteness] = useState<AccountingCompleteness | null>(null);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getBalanceSheetReport({ asOfDate });
      setData(r);
      setCompleteness(r.completeness);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el Balance General." }),
      );
    } finally {
      setLoading(false);
    }
  }, [asOfDate]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  const equityRows = useMemo<EquityRow[]>(() => {
    if (!data) return [];
    const calculated = t("accounting.reports.balanceSheet.calculated");
    return [
      ...data.equityLines,
      {
        accountId: "calculated-current-year-result",
        accountCode: calculated,
        accountName: t("accounting.reports.balanceSheet.currentYearResult"),
        amount: data.currentYearResult,
        calculated: true,
      },
      {
        accountId: "calculated-prior-years-unclosed-result",
        accountCode: calculated,
        accountName: t("accounting.reports.balanceSheet.priorYearsUnclosedResult"),
        amount: data.priorYearsUnclosedResult,
        calculated: true,
      },
    ];
  }, [data, t]);

  return (
    <ZHCard title="Balance General">
      <ZHFilterBar disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField label="Fecha de corte" density="compact">
            <ZhDateInput className="zh-input" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={() => void fetchReport()} disabled={loading}>
          Buscar
        </ZHBtn>
      </ZHFilterBar>
      <ReportCompletenessNotice completeness={completeness} />

      {data && !data.isBalanced && (
        <ZHPageNotice variant="error" message="Activo no cuadra con Pasivo + Patrimonio." />
      )}
      {data && (
        <>
          <ZHCard title={<span className="zh-section-title">Activos</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.assetLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin cuentas de activo con saldo a la fecha de corte."
            />
            <div className="zh-actions">
              <span>Total Activos <ZHMoneyValue precision="accounting" value={data.totalAssets} emphasis="total" /></span>
            </div>
          </ZHCard>

          <ZHCard title={<span className="zh-section-title">Pasivos</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.liabilityLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin cuentas de pasivo con saldo a la fecha de corte."
            />
            <div className="zh-actions">
              <span>Total Pasivos <ZHMoneyValue precision="accounting" value={data.totalLiabilities} emphasis="total" /></span>
            </div>
          </ZHCard>

          <ZHCard title={<span className="zh-section-title">Patrimonio</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={equityRows}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin cuentas de patrimonio con saldo a la fecha de corte."
            />
            <div className="zh-actions">
              <span>Total Patrimonio <ZHMoneyValue precision="accounting" value={data.totalEquity} emphasis="total" /></span>
            </div>
          </ZHCard>

          <div className="zh-actions">
            <span>Total Activo <ZHMoneyValue precision="accounting" value={data.totalAssets} emphasis="grand" /></span>
            <span>Total Pasivo + Patrimonio <ZHMoneyValue precision="accounting" value={data.totalLiabilities + data.totalEquity} emphasis="grand" /></span>
          </div>
        </>
      )}
    </ZHCard>
  );
}
