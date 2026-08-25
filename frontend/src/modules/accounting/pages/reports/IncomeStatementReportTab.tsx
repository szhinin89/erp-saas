import { useCallback, useEffect, useState } from "react";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import { accountingApi, type FinancialStatementLineDto } from "../../api/accountingApi";

function firstDayOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

const LINE_COLUMNS: ZHDataTableColumn<FinancialStatementLineDto>[] = [
  { key: "accountCode", header: "Código", render: (r) => <code className="prd-sku">{r.accountCode}</code> },
  { key: "accountName", header: "Cuenta", render: (r) => r.accountName },
  { key: "amount", header: "Monto", align: "right", render: (r) => <ZHMoneyValue value={r.amount} /> },
];

/**
 * Estado de Resultados (ACCOUNTING-FINANCIAL-STATEMENTS-10) — consume
 * `GET /accounting/reports/income-statement`. Solo período (sin saldo inicial): Ingresos, Costos,
 * Utilidad bruta, Gastos, Utilidad neta básica. Solo lectura, sin recálculo.
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E: filtros a `ZHFilterBar`; cada sección (Ingresos/Costos/
 * Gastos) pasa a un `ZHCard` anidado con título `zh-section-title` (antes `<strong>` plano, sin
 * jerarquía visual real) y totales con `ZHMoneyValue` (`emphasis="grand"` para utilidad
 * bruta/neta, `"total"` para subtotales de sección).
 */
export function IncomeStatementReportTab() {
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(today());
  const [data, setData] = useState<{
    incomeLines: FinancialStatementLineDto[];
    totalIncome: number;
    costLines: FinancialStatementLineDto[];
    totalCost: number;
    grossProfit: number;
    expenseLines: FinancialStatementLineDto[];
    totalExpense: number;
    netProfit: number;
  } | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getIncomeStatementReport({ fromDate, toDate });
      setData(r);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el Estado de Resultados." }),
      );
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  return (
    <ZHCard title="Estado de Resultados">
      <ZHFilterBar disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField label="Desde" density="compact">
            <ZhDateInput className="zh-input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField label="Hasta" density="compact">
            <ZhDateInput className="zh-input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={() => void fetchReport()} disabled={loading}>
          Buscar
        </ZHBtn>
      </ZHFilterBar>

      {!data && !loading && (
        <ZHPageNotice variant="neutral" message="Seleccione un rango de fechas para calcular el Estado de Resultados." />
      )}
      {data && (
        <>
          <ZHCard title={<span className="zh-section-title">Ingresos</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.incomeLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin ingresos contabilizados en el rango seleccionado."
            />
            <div className="zh-actions">
              <span>Total Ingresos <ZHMoneyValue value={data.totalIncome} emphasis="total" /></span>
            </div>
          </ZHCard>

          <ZHCard title={<span className="zh-section-title">Costos</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.costLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin costos contabilizados en el rango seleccionado."
            />
            <div className="zh-actions">
              <span>Total Costos <ZHMoneyValue value={data.totalCost} emphasis="total" /></span>
            </div>
          </ZHCard>

          <div className="zh-actions">
            <span>Utilidad bruta <ZHMoneyValue value={data.grossProfit} emphasis="grand" /></span>
          </div>

          <ZHCard title={<span className="zh-section-title">Gastos</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.expenseLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin gastos contabilizados en el rango seleccionado."
            />
            <div className="zh-actions">
              <span>Total Gastos <ZHMoneyValue value={data.totalExpense} emphasis="total" /></span>
            </div>
          </ZHCard>

          <div className="zh-actions">
            <span>Utilidad neta <ZHMoneyValue value={data.netProfit} emphasis="grand" /></span>
          </div>
        </>
      )}
    </ZHCard>
  );
}
