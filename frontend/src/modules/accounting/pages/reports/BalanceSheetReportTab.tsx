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

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

const LINE_COLUMNS: ZHDataTableColumn<FinancialStatementLineDto>[] = [
  { key: "accountCode", header: "Código", render: (r) => <code className="prd-sku">{r.accountCode}</code> },
  { key: "accountName", header: "Cuenta", render: (r) => r.accountName },
  { key: "amount", header: "Saldo", align: "right", render: (r) => <ZHMoneyValue value={r.amount} /> },
];

/**
 * Balance General (ACCOUNTING-FINANCIAL-STATEMENTS-10) — consume
 * `GET /accounting/reports/balance-sheet`. Saldo acumulado (desde el inicio del historial Posted)
 * de Activos/Pasivos/Patrimonio hasta la fecha de corte. Solo lectura, sin recálculo. Sin cierre
 * contable todavía: puede legítimamente no cuadrar mientras exista actividad de Ingresos/Costos/
 * Gastos sin cerrar a Patrimonio — se muestra un aviso, nunca se oculta el desbalance.
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E: mismo ajuste de filtros/secciones/totales que Estado de
 * Resultados (`ZHFilterBar`, `ZHCard` anidado con `zh-section-title`, `ZHMoneyValue`).
 */
export function BalanceSheetReportTab() {
  const [asOfDate, setAsOfDate] = useState(today());
  const [data, setData] = useState<{
    assetLines: FinancialStatementLineDto[];
    totalAssets: number;
    liabilityLines: FinancialStatementLineDto[];
    totalLiabilities: number;
    equityLines: FinancialStatementLineDto[];
    totalEquity: number;
    difference: number;
    isBalanced: boolean;
  } | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getBalanceSheetReport({ asOfDate });
      setData(r);
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

      {data && !data.isBalanced && (
        <ZHPageNotice
          variant="warning"
          message="Activo no cuadra con Pasivo + Patrimonio."
          detail="Sin cierre contable, la utilidad/pérdida del período aún no se traslada a Patrimonio — esta diferencia es esperada mientras exista actividad de Ingresos/Costos/Gastos sin cerrar."
        />
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
              <span>Total Activos <ZHMoneyValue value={data.totalAssets} emphasis="total" /></span>
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
              <span>Total Pasivos <ZHMoneyValue value={data.totalLiabilities} emphasis="total" /></span>
            </div>
          </ZHCard>

          <ZHCard title={<span className="zh-section-title">Patrimonio</span>} className="zh-mb-16">
            <ZHDataTable
              columns={LINE_COLUMNS}
              rows={data.equityLines}
              rowKey={(r) => r.accountId}
              loading={loading}
              emptyMessage="Sin cuentas de patrimonio con saldo a la fecha de corte."
            />
            <div className="zh-actions">
              <span>Total Patrimonio <ZHMoneyValue value={data.totalEquity} emphasis="total" /></span>
            </div>
          </ZHCard>

          <div className="zh-actions">
            <span>Total Activo <ZHMoneyValue value={data.totalAssets} emphasis="grand" /></span>
            <span>Total Pasivo + Patrimonio <ZHMoneyValue value={data.totalLiabilities + data.totalEquity} emphasis="grand" /></span>
          </div>
        </>
      )}
    </ZHCard>
  );
}
