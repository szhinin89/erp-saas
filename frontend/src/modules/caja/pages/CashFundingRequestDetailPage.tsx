import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Badge, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { payableOriginLabel } from "../../../lib/payableOrigin";
import { message } from "../../../lib/messages";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { formatApiRequestError } from "../../lib/apiError";
import {
  cashFundingRequestService,
  type CashFundingRequestApplicationLine,
  type CashFundingRequestDto,
  type CashFundingRequestSourceLine,
} from "../api/cashFundingRequestService";
import {
  cashFundingRequestStatusBadge,
  cashFundingRequestStatusLabel,
} from "../constants/cashFundingRequestStatus";
import { CASH_FUNDING_REQUESTS_ROUTE } from "../facades/cashFundingRequestFacade";

function Label({ children }: { children: ReactNode }) {
  return <ZHFieldLabel size="sm">{children}</ZHFieldLabel>;
}

type Action = "fulfill" | "reject" | "cancel";

/**
 * ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — detalle de una solicitud de efectivo: resumen, origen de
 * fondos (caja/banco), aplicaciones a CxP y resolución. Las acciones se muestran EXCLUSIVAMENTE según
 * `canFulfill`/`canReject`/`canCancel` del servidor; el backend revalida todo al ejecutar.
 */
export function CashFundingRequestDetailPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [request, setRequest] = useState<CashFundingRequestDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState<Action | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setRequest(await cashFundingRequestService.getById(id));
    } catch (err: unknown) {
      setRequest(null);
      message.error(formatApiRequestError(err, { generic: "No se pudo cargar la solicitud de efectivo." }));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const run = async (action: Action, execute: () => Promise<CashFundingRequestDto>, success: string) => {
    setRunning(action);
    try {
      const updated = await execute();
      setRequest(updated);
      message.success(success);
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo completar la acción." }));
      await load();
    } finally {
      setRunning(null);
    }
  };

  const handleFulfill = async () => {
    if (!request) return;
    const confirmed = await message.confirm({
      title: "Entregar efectivo",
      message: `Confirmo que estoy entregando ${formatMoney(request.cashAmount, moneyDecimals)} en efectivo para completar este pago.`,
      confirmLabel: "Entregar efectivo",
    });
    if (!confirmed) return;
    await run("fulfill", () => cashFundingRequestService.fulfill(request.id), "Efectivo entregado: el pago a proveedor quedó registrado.");
  };

  const askReason = (title: string, confirmLabel: string) =>
    message.prompt({ title, label: "Motivo", message: "El motivo es obligatorio.", variant: "danger", confirmLabel, required: true });

  const handleReject = async () => {
    if (!request) return;
    const reason = (await askReason("Rechazar solicitud", "Rechazar"))?.trim();
    if (!reason) return;
    await run("reject", () => cashFundingRequestService.reject(request.id, reason), "Solicitud de efectivo rechazada.");
  };

  const handleCancel = async () => {
    if (!request) return;
    const reason = (await askReason("Cancelar solicitud", "Cancelar solicitud"))?.trim();
    if (!reason) return;
    await run("cancel", () => cashFundingRequestService.cancel(request.id, reason), "Solicitud de efectivo cancelada.");
  };

  if (loading && !request) {
    return (
      <PageShell title="Solicitud de efectivo" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!request) {
    return (
      <PageShell title="Solicitud de efectivo">
        <ZHPageNotice variant="error" message="Solicitud de efectivo no encontrada" />
      </PageShell>
    );
  }

  const sourceColumns: ZHDataTableColumn<CashFundingRequestSourceLine>[] = [
    {
      key: "source",
      header: "Origen",
      render: (s) => (s.kind === "Cash" ? `Caja · ${s.cashRegisterName ?? "—"}` : `Banco · ${s.bankAccountName ?? "—"}`),
    },
    { key: "method", header: "Medio", render: (s) => s.paymentMethodName || "—" },
    {
      key: "amount",
      header: "Monto",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (s) => <ZHMoneyValue value={s.amount} precision="money" />,
    },
    { key: "date", header: "Fecha", render: (s) => (s.transactionDate ? formatDate(s.transactionDate) : "—") },
    {
      key: "reference",
      header: "Referencia",
      render: (s) => s.referenceNumber ?? (s.checkNumber ? `Cheque ${s.checkNumber}` : "—"),
    },
  ];

  const applicationColumns: ZHDataTableColumn<CashFundingRequestApplicationLine>[] = [
    { key: "origin", header: "Origen", render: (a) => (a.originType ? payableOriginLabel(a.originType) : "—") },
    {
      key: "document",
      header: "Documento",
      render: (a) =>
        a.accountsPayableId && a.documentNumber ? (
          <Link to={`/payables/${a.accountsPayableId}`} className="zh-link">
            {a.documentNumber}
          </Link>
        ) : (
          (a.documentNumber ?? "—")
        ),
    },
    { key: "installment", header: "Cuota", render: (a) => (a.installmentNumber ? `Cuota ${a.installmentNumber}` : "—") },
    {
      key: "amount",
      header: "Monto aplicado",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (a) => <ZHMoneyValue value={a.amountApplied} precision="money" />,
    },
  ];

  const hasActions = request.canFulfill || request.canReject || request.canCancel;
  const resolved = request.status !== "Pending";

  return (
    <PageShell
      kicker="Solicitudes de efectivo"
      title={request.supplierName || "Solicitud de efectivo"}
      subtitle={`Efectivo solicitado: ${formatMoney(request.cashAmount, moneyDecimals)} · Pago total: ${formatMoney(request.totalAmount, moneyDecimals)}`}
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate(CASH_FUNDING_REQUESTS_ROUTE)}>
          Volver a solicitudes
        </ZHBtn>
      }
    >
      <ZHCard
        title="Resumen"
        actions={
          <Badge
            label={cashFundingRequestStatusLabel(request.status)}
            variant={cashFundingRequestStatusBadge(request.status)}
          />
        }
      >
        <ZHInfoRow label={<Label>Proveedor</Label>} value={<ZHDataValue>{request.supplierName || "—"}</ZHDataValue>} />
        <ZHInfoRow label={<Label>Solicitado por</Label>} value={<ZHDataValue>{request.requestedByName || "—"}</ZHDataValue>} />
        <ZHInfoRow label={<Label>Caja</Label>} value={<ZHDataValue>{request.cashRegisterName || "—"}</ZHDataValue>} />
        <ZHInfoRow label={<Label>Sucursal</Label>} value={<ZHDataValue>{request.branchName || "—"}</ZHDataValue>} />
        <ZHInfoRow label={<Label>Fecha</Label>} value={<ZHDataValue>{formatDateTime(request.requestedAtUtc)}</ZHDataValue>} />
        <ZHInfoRow
          label={<Label>Estado</Label>}
          value={<ZHDataValue>{cashFundingRequestStatusLabel(request.status)}</ZHDataValue>}
        />
        <ZHInfoRow
          label={<Label>Efectivo solicitado</Label>}
          value={<ZHMoneyValue value={request.cashAmount} precision="money" emphasis="strong" />}
        />
        <ZHInfoRow label={<Label>Pago total</Label>} value={<ZHMoneyValue value={request.totalAmount} precision="money" />} />
        {request.paymentDate && (
          <ZHInfoRow label={<Label>Fecha del pago</Label>} value={<ZHDataValue>{formatDate(request.paymentDate)}</ZHDataValue>} />
        )}
        {request.receiptNumber && (
          <ZHInfoRow label={<Label>Comprobante</Label>} value={<ZHDataValue>{request.receiptNumber}</ZHDataValue>} />
        )}
      </ZHCard>

      {hasActions && (
        <ZHCard title="Acciones">
          <div className="sr-draft-actions">
            {request.canFulfill && (
              <ZHBtn type="button" variant="primary" disabled={running !== null} onClick={() => void handleFulfill()}>
                Entregar efectivo
              </ZHBtn>
            )}
            {request.canReject && (
              <ZHBtn type="button" variant="secondary" disabled={running !== null} onClick={() => void handleReject()}>
                Rechazar
              </ZHBtn>
            )}
            {request.canCancel && (
              <ZHBtn type="button" variant="secondary" disabled={running !== null} onClick={() => void handleCancel()}>
                Cancelar solicitud
              </ZHBtn>
            )}
          </div>
        </ZHCard>
      )}

      <ZHCard title="Origen de fondos">
        <ZHDataTable
          columns={sourceColumns}
          rows={request.sources}
          rowKey={(s) => `${s.kind}-${s.paymentMethodId}-${s.cashRegisterId ?? s.companyBankAccountId ?? ""}-${s.amount}`}
          tableClassName="table--compact table--neutral"
          emptyMessage="Sin información de origen de fondos."
        />
      </ZHCard>

      <ZHCard title="Aplicaciones a cuentas por pagar">
        <ZHDataTable
          columns={applicationColumns}
          rows={request.applications}
          rowKey={(a) => a.accountsPayableInstallmentId}
          tableClassName="table--compact table--neutral"
          emptyMessage="El pago no se aplica a cuentas por pagar (queda como anticipo)."
        />
      </ZHCard>

      {resolved && (
        <ZHCard title="Resolución">
          <ZHInfoRow label={<Label>Resuelta por</Label>} value={<ZHDataValue>{request.resolvedByName || "—"}</ZHDataValue>} />
          <ZHInfoRow
            label={<Label>Fecha</Label>}
            value={<ZHDataValue>{request.resolvedAtUtc ? formatDateTime(request.resolvedAtUtc) : "—"}</ZHDataValue>}
          />
          {request.resolutionReason && (
            <ZHInfoRow label={<Label>Motivo</Label>} value={<ZHDataValue>{request.resolutionReason}</ZHDataValue>} />
          )}
          {request.supplierPaymentId && (
            <ZHInfoRow
              label={<Label>Pago a proveedor</Label>}
              value={
                <Link to={`/supplier-payments/${request.supplierPaymentId}`} className="zh-link">
                  Ver pago generado
                </Link>
              }
            />
          )}
        </ZHCard>
      )}
    </PageShell>
  );
}

export default CashFundingRequestDetailPage;
