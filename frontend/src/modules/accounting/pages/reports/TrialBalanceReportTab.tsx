import { useCallback, useEffect, useId, useState } from "react";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import { accountingApi, type TrialBalanceLineDto } from "../../api/accountingApi";

function firstDayOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Balance de Comprobación (ACCOUNTING-REPORTS-09) — saldo inicial/movimiento/saldo final por
 * cuenta en convención deudora/acreedora, consume `GET /accounting/reports/trial-balance`.
 * Solo lectura, sin recálculo. ACCOUNTING-REPORTS-DS-QA-FIX-10E: mismo ajuste de filtros/totales
 * que Libro Diario; el checkbox (HTML crudo permitido fuera de `ZHToggle` por regla del proyecto)
 * se mueve del header a la barra de filtros con `<label htmlFor>` asociado.
 */
export function TrialBalanceReportTab() {
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(today());
  const [includeZeroMovementAccounts, setIncludeZeroMovementAccounts] = useState(false);
  const [lines, setLines] = useState<TrialBalanceLineDto[]>([]);
  const [totals, setTotals] = useState({
    openingDebit: 0,
    openingCredit: 0,
    periodDebit: 0,
    periodCredit: 0,
    closingDebit: 0,
    closingCredit: 0,
    isBalanced: true,
  });
  const [loading, setLoading] = useState(false);
  const includeZeroId = useId();

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getTrialBalanceReport({
        fromDate,
        toDate,
        includeZeroMovementAccounts,
      });
      setLines(r.lines);
      setTotals({
        openingDebit: r.totalOpeningDebit,
        openingCredit: r.totalOpeningCredit,
        periodDebit: r.totalPeriodDebit,
        periodCredit: r.totalPeriodCredit,
        closingDebit: r.totalClosingDebit,
        closingCredit: r.totalClosingCredit,
        isBalanced: r.isBalanced,
      });
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el Balance de Comprobación." }),
      );
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, includeZeroMovementAccounts]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  const columns: ZHDataTableColumn<TrialBalanceLineDto>[] = [
    { key: "accountCode", header: "Código", render: (r) => <code className="prd-sku">{r.accountCode}</code> },
    { key: "accountName", header: "Cuenta", render: (r) => r.accountName },
    { key: "accountType", header: "Tipo", render: (r) => r.accountType },
    { key: "openingDebit", header: "Saldo inicial deudor", align: "right", render: (r) => <ZHMoneyValue value={r.openingDebit > 0 ? r.openingDebit : null} /> },
    { key: "openingCredit", header: "Saldo inicial acreedor", align: "right", render: (r) => <ZHMoneyValue value={r.openingCredit > 0 ? r.openingCredit : null} /> },
    { key: "periodDebit", header: "Movimiento debe", align: "right", render: (r) => <ZHMoneyValue value={r.periodDebit > 0 ? r.periodDebit : null} /> },
    { key: "periodCredit", header: "Movimiento haber", align: "right", render: (r) => <ZHMoneyValue value={r.periodCredit > 0 ? r.periodCredit : null} /> },
    { key: "closingDebit", header: "Saldo final deudor", align: "right", render: (r) => <ZHMoneyValue value={r.closingDebit > 0 ? r.closingDebit : null} emphasis="strong" /> },
    { key: "closingCredit", header: "Saldo final acreedor", align: "right", render: (r) => <ZHMoneyValue value={r.closingCredit > 0 ? r.closingCredit : null} emphasis="strong" /> },
  ];

  return (
    <ZHCard title="Balance de Comprobación">
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
        <div className="zh-filterbar__field zh-filterbar__field--grow zh-form-actions-row">
          <input
            id={includeZeroId}
            type="checkbox"
            checked={includeZeroMovementAccounts}
            onChange={(e) => setIncludeZeroMovementAccounts(e.target.checked)}
          />
          <label htmlFor={includeZeroId}>Incluir cuentas sin movimiento</label>
        </div>
        <ZHBtn type="button" variant="primary" onClick={() => void fetchReport()} disabled={loading}>
          Buscar
        </ZHBtn>
      </ZHFilterBar>

      {!totals.isBalanced && (
        <ZHPageNotice
          variant="error"
          message="El total Debe del período no coincide con el total Haber — revise los asientos del rango."
        />
      )}
      <ZHDataTable
        columns={columns}
        rows={lines}
        rowKey={(r) => r.accountId}
        loading={loading}
        emptyMessage="No hay cuentas con actividad contabilizada (Posted) en el rango seleccionado."
      />
      {lines.length > 0 && (
        <div className="zh-actions">
          <span>Total Debe <ZHMoneyValue value={totals.periodDebit} emphasis="total" /></span>
          <span>Total Haber <ZHMoneyValue value={totals.periodCredit} emphasis="total" /></span>
        </div>
      )}
    </ZHCard>
  );
}
