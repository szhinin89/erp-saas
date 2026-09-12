import { useMemo, useState } from "react";
import { useFieldArray, useFormContext } from "react-hook-form";
import { Badge } from "../../../components/PageShell";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDecimalInput } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import type { PendingInstallmentOption } from "../api/pendingPayablesFacade";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  installments: PendingInstallmentOption[];
  loading?: boolean;
  disabled?: boolean;
}

const STATUS_LABELS: Record<string, string> = {
  pending: "Pendiente",
  partiallypaid: "Parcial",
};

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * SUPPLIER-PAYMENT-PROVIDER-PORTFOLIO-VISIBILITY-01 — cartera pendiente del proveedor visible en
 * `/supplier-payments/new`, alimentada por `pendingPayablesFacade` (mismo origen que ya consumía
 * `SupplierPaymentApplicationsEditor`, sin endpoint nuevo). Esta grilla es la fuente de verdad que
 * alimenta `applicationLines` — comparte el mismo `useFieldArray` (name="applicationLines") que
 * `SupplierPaymentApplicationsEditor`, por lo que ambos quedan sincronizados vía RHF sin estado
 * duplicado; el usuario puede seguir editando la cuota/monto desde cualquiera de las dos secciones.
 */
export function SupplierPayablesPortfolio({ installments, loading, disabled }: Props) {
  const { control, watch } = useFormContext<RegisterSupplierPaymentFormValues>();
  const { replace } = useFieldArray({ control, name: "applicationLines" });
  const applicationLines = watch("applicationLines") ?? [];

  // SUPPLIER-PAYMENT-PORTFOLIO-DECIMAL-INPUT-01 — el input de "Monto a aplicar" estaba 100%
  // controlado por el número ya parseado (`value={value}`), que `ZhDecimalInput` reformatea con
  // `toFixed(2)` en cada render. Al escribir "20." el valor parseado es 20, y el siguiente render
  // mostraba "20.00" de nuevo — el punto decimal (y cualquier dígito tecleado después) desaparecía
  // en cada tecla, haciendo imposible teclear decimales (mismo bug ya corregido en
  // PurchaseCreditNoteTaxSummaryLinesEditor). `rawInputById` guarda el texto tal como lo escribe
  // el usuario por cuota; `applicationLines` sigue recibiendo siempre el número ya parseado/topado
  // al saldo pendiente.
  const [rawInputById, setRawInputById] = useState<Record<string, string>>({});

  const appliedByInstallment = new Map<string, number>();
  applicationLines.forEach((l) => {
    if (l.accountsPayableInstallmentId) {
      appliedByInstallment.set(l.accountsPayableInstallmentId, l.amountApplied || 0);
    }
  });

  const today = todayIso();

  const summary = useMemo(() => {
    const totalPending = installments.reduce((sum, i) => sum + i.outstandingAmount, 0);
    const totalOverdue = installments
      .filter((i) => i.dueDate < today)
      .reduce((sum, i) => sum + i.outstandingAmount, 0);
    const nextDueDate = installments.reduce<string | null>(
      (min, i) => (min === null || i.dueDate < min ? i.dueDate : min),
      null,
    );
    return { totalPending, totalOverdue, nextDueDate, pendingCount: installments.length };
  }, [installments, today]);

  function setAmount(installmentId: string, amount: number) {
    const next = applicationLines.filter((l) => l.accountsPayableInstallmentId !== installmentId);
    if (amount > 0) next.push({ accountsPayableInstallmentId: installmentId, amountApplied: amount });
    replace(next);
  }

  function applyFull(row: PendingInstallmentOption) {
    setRawInputById((prev) => ({ ...prev, [row.installmentId]: String(row.outstandingAmount) }));
    setAmount(row.installmentId, row.outstandingAmount);
  }

  function payAll() {
    setRawInputById((prev) => {
      const next = { ...prev };
      installments.forEach((i) => {
        next[i.installmentId] = String(i.outstandingAmount);
      });
      return next;
    });
    replace(
      installments.map((i) => ({
        accountsPayableInstallmentId: i.installmentId,
        amountApplied: i.outstandingAmount,
      })),
    );
  }

  function handleAmountChange(row: PendingInstallmentOption, raw: string) {
    const normalized = raw.trim().replace(",", ".");

    if (normalized === "") {
      setRawInputById((prev) => ({ ...prev, [row.installmentId]: raw }));
      setAmount(row.installmentId, 0);
      return;
    }

    const num = Number(normalized);
    if (Number.isNaN(num)) {
      setRawInputById((prev) => ({ ...prev, [row.installmentId]: raw }));
      return;
    }

    if (num > row.outstandingAmount) {
      // Tope estricto al saldo pendiente — se refleja de inmediato en el texto mostrado, sin
      // esperar al blur, para que nunca quede un monto por encima del saldo aplicado al form.
      setRawInputById((prev) => ({ ...prev, [row.installmentId]: String(row.outstandingAmount) }));
      setAmount(row.installmentId, row.outstandingAmount);
      return;
    }

    setRawInputById((prev) => ({ ...prev, [row.installmentId]: raw }));
    setAmount(row.installmentId, Math.max(0, num));
  }

  const columns: ZHDataTableColumn<PendingInstallmentOption>[] = [
    {
      key: "doc",
      header: "N.º documento",
      render: (r) => `${r.documentType} ${r.documentNumber} — Cuota #${r.installmentNumber}`,
    },
    { key: "issue", header: "F. emisión", render: (r) => formatDate(r.issueDate) },
    { key: "due", header: "F. vencimiento", render: (r) => formatDate(r.dueDate) },
    {
      key: "total",
      header: "Valor total",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (r) => formatMoney(r.totalAmount),
    },
    {
      key: "paid",
      header: "Pagado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (r) => formatMoney(r.paidAmount),
    },
    {
      key: "outstanding",
      header: "Saldo pendiente",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (r) => formatMoney(r.outstandingAmount),
    },
    {
      key: "status",
      header: "Estado",
      render: (r) => {
        const overdue = r.dueDate < today;
        const label = overdue ? "Vencida" : (STATUS_LABELS[r.status.toLowerCase()] ?? r.status);
        let variant: "error" | "warning" | "info" = "info";
        if (overdue) variant = "error";
        else if (r.status.toLowerCase() === "partiallypaid") variant = "warning";
        return <Badge label={label} variant={variant} />;
      },
    },
    {
      key: "apply",
      header: "Monto a aplicar",
      align: "right",
      render: (r) => {
        const value = appliedByInstallment.get(r.installmentId) ?? 0;
        const displayValue = rawInputById[r.installmentId] ?? (value > 0 ? String(value) : "");
        return (
          <div className="sp-portfolio-apply">
            <ZhDecimalInput
              aria-label={`Monto a aplicar: ${r.documentType} ${r.documentNumber} — Cuota #${r.installmentNumber}`}
              decimals={2}
              positiveOnly
              density="compact"
              disabled={disabled}
              value={displayValue}
              onChange={(e) => handleAmountChange(r, e.target.value)}
            />
            <ZHBtn
              type="button"
              variant="ghost"
              size="xs"
              disabled={disabled}
              onClick={() => applyFull(r)}
            >
              Aplicar saldo completo
            </ZHBtn>
          </div>
        );
      },
    },
  ];

  if (installments.length === 0 && !loading) {
    return <p className="sp-line-hint">Este proveedor no tiene cuentas por pagar pendientes.</p>;
  }

  return (
    <div className="sp-portfolio">
      <div className="pg-kpis">
        <ReportKpiCard
          layout="horizontal"
          icon="account_balance_wallet"
          tone="primary"
          label="Total pendiente"
          value={formatMoney(summary.totalPending)}
        />
        <ReportKpiCard
          layout="horizontal"
          icon="warning"
          tone={summary.totalOverdue > 0 ? "error" : "neutral"}
          label="Total vencido"
          value={formatMoney(summary.totalOverdue)}
        />
        <ReportKpiCard
          layout="horizontal"
          icon="event"
          tone="info"
          label="Próximo vencimiento"
          value={summary.nextDueDate ? formatDate(summary.nextDueDate) : "—"}
        />
        <ReportKpiCard
          layout="horizontal"
          icon="receipt_long"
          tone="secondary"
          label="Cuotas pendientes"
          value={String(summary.pendingCount)}
        />
      </div>

      <div className="sp-portfolio-actions">
        <ZHBtn type="button" variant="secondary" size="sm" disabled={disabled} onClick={payAll}>
          Pagar todo el saldo pendiente
        </ZHBtn>
      </div>

      <ZHDataTable columns={columns} rows={installments} rowKey={(r) => r.installmentId} loading={loading} />
    </div>
  );
}
