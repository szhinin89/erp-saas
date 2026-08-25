import { useCallback, useEffect, useState } from "react";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput, ZhSelect } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { Badge } from "../../../../components/PageShell";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  accountingApi,
  type AccountDto,
  type GeneralLedgerAccountDto,
  type GeneralLedgerMovementDto,
} from "../../api/accountingApi";

function firstDayOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Libro Mayor (ACCOUNTING-REPORTS-09) — saldo inicial/movimiento/saldo final y detalle Kardex
 * por cuenta, consume `GET /accounting/reports/general-ledger`. Solo lectura, sin recálculo.
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E: mismo ajuste de filtros/totales que Libro Diario; el
 * encabezado por cuenta (código/nombre/Badge de naturaleza) pasa a ser un `ZHCard` anidado con
 * `zh-mb-16` real para separación entre cuentas (antes `pg-pad-8`, sin efecto).
 */
export function GeneralLedgerReportTab() {
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(today());
  const [accountId, setAccountId] = useState("");
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [ledgerAccounts, setLedgerAccounts] = useState<GeneralLedgerAccountDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void accountingApi
      .listAccounts()
      .then((list) => setAccounts(list.filter((a) => a.allowsPosting)))
      .catch(() => undefined);
  }, []);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.getGeneralLedgerReport({
        fromDate,
        toDate,
        accountId: accountId || undefined,
      });
      setLedgerAccounts(r.accounts);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el Libro Mayor." }),
      );
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, accountId]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  const movementColumns: ZHDataTableColumn<GeneralLedgerMovementDto>[] = [
    { key: "entryDate", header: "Fecha", render: (r) => formatDate(r.entryDate) },
    { key: "entryNumber", header: "Asiento", render: (r) => r.entryNumber ?? "—" },
    { key: "description", header: "Descripción", render: (r) => r.description },
    {
      key: "source",
      header: "Origen",
      render: (r) => r.sourceDocumentNumber ?? r.sourceModule,
    },
    {
      key: "debit",
      header: "Debe",
      align: "right",
      render: (r) => <ZHMoneyValue value={r.debit > 0 ? r.debit : null} />,
    },
    {
      key: "credit",
      header: "Haber",
      align: "right",
      render: (r) => <ZHMoneyValue value={r.credit > 0 ? r.credit : null} />,
    },
    {
      key: "runningBalance",
      header: "Saldo",
      align: "right",
      render: (r) => <ZHMoneyValue value={r.runningBalance} emphasis="strong" />,
    },
  ];

  return (
    <ZHCard title="Libro Mayor">
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
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label="Cuenta" density="compact">
            <ZhSelect className="zh-input" value={accountId} onChange={(e) => setAccountId(e.target.value)}>
              <option value="">Todas las cuentas</option>
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>{a.code} — {a.name}</option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={() => void fetchReport()} disabled={loading}>
          Buscar
        </ZHBtn>
      </ZHFilterBar>

      {!accountId && (
        <ZHPageNotice
          variant="info"
          message="Sin una cuenta seleccionada se muestran todas las cuentas del Plan de Cuentas — puede tardar más en cargar."
        />
      )}
      {!loading && ledgerAccounts.length === 0 && (
        <ZHPageNotice
          variant="neutral"
          message="No hay cuentas con movimientos contabilizados (Posted) en el rango seleccionado."
        />
      )}
      {ledgerAccounts.map((acc) => (
        <ZHCard
          key={acc.accountId}
          title={
            <span className="zh-form-actions-row">
              {acc.accountCode} — {acc.accountName}
              <Badge label={acc.nature === "Debit" ? "Naturaleza deudora" : "Naturaleza acreedora"} variant="gray" />
            </span>
          }
          className="zh-mb-16"
        >
          <div className="zh-actions">
            <span>Saldo inicial <ZHMoneyValue value={acc.openingBalance} emphasis="strong" /></span>
            <span>Debe <ZHMoneyValue value={acc.periodDebit} /></span>
            <span>Haber <ZHMoneyValue value={acc.periodCredit} /></span>
            <span>Saldo final <ZHMoneyValue value={acc.closingBalance} emphasis="total" /></span>
          </div>
          <ZHDataTable
            columns={movementColumns}
            rows={acc.movements}
            rowKey={(r) => `${r.journalEntryId}-${r.debit}-${r.credit}-${r.runningBalance}`}
            loading={false}
            emptyMessage="Sin movimientos en el rango seleccionado."
          />
        </ZHCard>
      ))}
    </ZHCard>
  );
}
