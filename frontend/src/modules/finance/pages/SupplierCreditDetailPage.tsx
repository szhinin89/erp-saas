import { useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate, formatDateTime, todayIso } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  supplierCreditService,
  type SupplierCreditDto,
  type SupplierCreditMovementDto,
} from "../api/supplierCreditService";
import { payableOriginLabel } from "../../../lib/payableOrigin";
import { supplierCreditSourceLabel, supplierCreditSourceRoute } from "../utils/supplierCreditOrigin";
import { ApplySupplierCreditModal } from "../components/ApplySupplierCreditModal";
import { RegisterSupplierCreditRefundModal } from "../components/RegisterSupplierCreditRefundModal";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

const LIST_ROUTE = "/suppliers/credits";

const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  Application: "Aplicación a CxP",
  ReversalOfApplication: "Reversa de aplicación",
  Refund: "Reembolso",
  ReversalOfRefund: "Reversa de reembolso",
  SourceReturnCancelled: "Anulación de la devolución de origen",
  SourcePaymentReversed: "Reversa del pago de origen",
};

const movementTypeLabel = (type: string) => MOVEMENT_TYPE_LABEL[type] ?? type;

function Label({ children }: { children: ReactNode }) {
  return <ZHFieldLabel size="sm">{children}</ZHFieldLabel>;
}

/** Documento / destino del movimiento: CxP (Compra/Gasto) o caja/banco con su medio de pago. */
function MovementTarget({ m }: { m: SupplierCreditMovementDto }) {
  if (m.accountsPayableId) {
    const origin = m.payableOriginType ? payableOriginLabel(m.payableOriginType) : "CxP";
    const label = `${origin} · ${m.payableDocumentNumber ?? "—"}`;
    return (
      <Link to={`/payables/${m.accountsPayableId}`} className="zh-link">
        {label}
      </Link>
    );
  }
  if (m.destinationType) {
    return (
      <div className="zh-stack">
        <span>
          {m.destinationType === "Cash" ? "Caja" : "Banco"} · {m.destinationName ?? "—"}
        </span>
        <span className="zh-text-muted">{m.paymentMethodName ?? m.paymentMethodCode ?? ""}</span>
      </div>
    );
  }
  return <span className="zh-text-muted">—</span>;
}

/**
 * ZH-SUPPLIER-BALANCES-UX-02D-E — detalle de un saldo a favor de proveedor: resumen, acciones
 * (solo con saldo disponible) e historial cronológico enriquecido (02D-D). `AvailableAmount` es
 * siempre el valor del servidor — nunca se recalcula en el cliente.
 */
export function SupplierCreditDetailPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [credit, setCredit] = useState<SupplierCreditDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [applyOpen, setApplyOpen] = useState(false);
  const [refundOpen, setRefundOpen] = useState(false);
  const [reversing, setReversing] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    supplierCreditService
      .getById(id)
      .then((dto) => {
        if (!cancelled) setCredit(dto);
      })
      .catch((err: unknown) => {
        message.error(
          formatApiRequestError(err, { generic: "No se pudo cargar el saldo a favor del proveedor." }),
        );
        navigate(LIST_ROUTE);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, navigate]);

  const reload = async () => {
    if (!id) return;
    const dto = await supplierCreditService.getById(id);
    setCredit(dto);
  };

  const handleReverseApplication = async (movement: SupplierCreditMovementDto) => {
    if (!credit || !movement.accountsPayableId) return;
    const confirmed = await message.confirm({
      title: "Revertir aplicación",
      message: `¿Revertir la aplicación de ${formatMoney(movement.amount, moneyDecimals)}? Esta acción no se puede deshacer.`,
      variant: "danger",
      confirmLabel: "Revertir aplicación",
    });
    if (!confirmed) return;
    setReversing(movement.id);
    try {
      await supplierCreditService.reverseApplication(credit.id, movement.id, {
        targetPurchasePayableId: movement.accountsPayableId,
        clientRequestId: crypto.randomUUID(),
      });
      message.success("Aplicación revertida correctamente.");
      await reload();
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo revertir la aplicación." }),
      );
    } finally {
      setReversing(null);
    }
  };

  const handleReverseRefund = async (movement: SupplierCreditMovementDto) => {
    if (!credit) return;
    const reason = await message.prompt({
      title: "Revertir reembolso",
      label: "Motivo de la reversa del reembolso",
      message: "Esta acción no se puede deshacer.",
      variant: "danger",
      confirmLabel: "Revertir reembolso",
      required: true,
    });
    if (!reason?.trim()) return;
    setReversing(movement.id);
    try {
      await supplierCreditService.reverseRefund(credit.id, movement.id, {
        reason: reason.trim(),
        effectiveDate: todayIso(),
        clientRequestId: crypto.randomUUID(),
      });
      message.success("Reembolso revertido correctamente.");
      await reload();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo revertir el reembolso." }));
    } finally {
      setReversing(null);
    }
  };

  if (loading) {
    return (
      <PageShell title="Saldo a favor del proveedor" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!credit) {
    return (
      <PageShell title="Saldo a favor del proveedor">
        <ZHPageNotice variant="error" message="Saldo a favor no encontrado" />
      </PageShell>
    );
  }

  const movementsById = new Map(credit.movements.map((m) => [m.id, m]));
  const sourceRoute = supplierCreditSourceRoute(credit);
  const hasBalance = credit.availableAmount > 0;

  const referenceOrReason = (m: SupplierCreditMovementDto): ReactNode => {
    const original = m.reversalOfMovementId ? movementsById.get(m.reversalOfMovementId) : undefined;
    return (
      <div className="zh-stack">
        {m.referenceNumber && <span>Ref. {m.referenceNumber}</span>}
        {m.reason && <span>Motivo: {m.reason}</span>}
        {original && (
          <span className="zh-text-muted">
            Revierte: {movementTypeLabel(original.movementType)} del {formatDateTime(original.createdAtUtc)}
          </span>
        )}
        {!m.referenceNumber && !m.reason && !original && <span className="zh-text-muted">—</span>}
      </div>
    );
  };

  const movementColumns: ZHDataTableColumn<SupplierCreditMovementDto>[] = [
    { key: "date", header: "Fecha", render: (m) => formatDateTime(m.createdAtUtc) },
    { key: "type", header: "Tipo", render: (m) => movementTypeLabel(m.movementType) },
    { key: "target", header: "Documento / Destino", render: (m) => <MovementTarget m={m} /> },
    {
      key: "amount",
      header: "Monto",
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (m) => <ZHMoneyValue value={m.amount} precision="money" />,
    },
    { key: "user", header: "Usuario", render: (m) => m.createdByName ?? "—" },
    { key: "reference", header: "Referencia / Motivo", render: referenceOrReason },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (m) => {
        if (m.movementType !== "Application" && m.movementType !== "Refund") return null;
        if (m.reversedByMovementId) return <span className="zh-text-muted">Revertido</span>;
        return (
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            disabled={reversing === m.id}
            onClick={() =>
              void (m.movementType === "Application"
                ? handleReverseApplication(m)
                : handleReverseRefund(m))
            }
          >
            Revertir
          </ZHBtn>
        );
      },
    },
  ];

  return (
    <PageShell
      kicker="Saldos a favor de proveedores"
      title={credit.supplierName ?? "Saldo a favor del proveedor"}
      subtitle={`${supplierCreditSourceLabel(credit.sourceType)} · Moneda: ${credit.currencyCode}`}
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate(LIST_ROUTE)}>
          Volver a saldos a favor
        </ZHBtn>
      }
    >
      <ZHCard
        title="Resumen"
        actions={
          <Badge label={credit.isOpen ? "Abierto" : "Cerrado"} variant={credit.isOpen ? "green" : "gray"} />
        }
      >
        <ZHInfoRow label={<Label>Proveedor</Label>} value={<ZHDataValue>{credit.supplierName ?? "—"}</ZHDataValue>} />
        <ZHInfoRow
          label={<Label>Origen</Label>}
          value={<ZHDataValue>{supplierCreditSourceLabel(credit.sourceType)}</ZHDataValue>}
        />
        <ZHInfoRow
          label={<Label>Documento origen</Label>}
          value={
            sourceRoute && credit.sourceDocumentNumber ? (
              <Link to={sourceRoute} className="zh-link">
                {credit.sourceDocumentNumber}
              </Link>
            ) : (
              <ZHDataValue>{credit.sourceDocumentNumber ?? "—"}</ZHDataValue>
            )
          }
        />
        <ZHInfoRow label={<Label>Fecha origen</Label>} value={<ZHDataValue>{formatDate(credit.sourceDate)}</ZHDataValue>} />
        <ZHInfoRow
          label={<Label>Monto original</Label>}
          value={<ZHMoneyValue value={credit.originalAmount} precision="money" />}
        />
        <ZHInfoRow
          label={<Label>Saldo disponible</Label>}
          value={<ZHMoneyValue value={credit.availableAmount} precision="money" emphasis="strong" />}
        />
      </ZHCard>

      {hasBalance && (
        <ZHCard title="Acciones">
          <div className="sr-draft-actions">
            <ZHBtn type="button" variant="primary" onClick={() => setApplyOpen(true)}>
              Aplicar a CxP
            </ZHBtn>
            <ZHBtn type="button" variant="secondary" onClick={() => setRefundOpen(true)}>
              Registrar reembolso
            </ZHBtn>
          </div>
        </ZHCard>
      )}

      <ZHCard title="Historial">
        <ZHDataTable
          columns={movementColumns}
          rows={credit.movements}
          rowKey={(m) => m.id}
          tableClassName="table--compact table--neutral"
          emptyMessage="Sin movimientos registrados."
        />
      </ZHCard>

      <ApplySupplierCreditModal
        open={applyOpen}
        credit={credit}
        onClose={() => setApplyOpen(false)}
        onApplied={setCredit}
      />
      <RegisterSupplierCreditRefundModal
        open={refundOpen}
        credit={credit}
        onClose={() => setRefundOpen(false)}
        onRegistered={setCredit}
      />
    </PageShell>
  );
}
