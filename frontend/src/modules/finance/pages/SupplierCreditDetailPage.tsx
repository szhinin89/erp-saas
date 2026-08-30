import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  supplierCreditService,
  type SupplierCreditDto,
  type SupplierCreditMovementDto,
} from "../api/supplierCreditService";
import { ApplySupplierCreditModal } from "../components/ApplySupplierCreditModal";
import { RegisterSupplierCreditRefundModal } from "../components/RegisterSupplierCreditRefundModal";

const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  Application: "Aplicación",
  ReversalOfApplication: "Reversa de aplicación",
  Refund: "Reembolso",
  ReversalOfRefund: "Reversa de reembolso",
  SourceReturnCancelled: "Anulación del origen",
};

/**
 * Detalle de un crédito de proveedor: saldo disponible (cacheado del servidor, §4.2 del diseño —
 * nunca recalculado en el cliente), historial de movimientos, aplicar/reembolsar, y reversa de
 * aplicaciones/reembolsos activos.
 */
export function SupplierCreditDetailPage() {
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
          formatApiRequestError(err, { generic: "No se pudo cargar el crédito de proveedor." }),
        );
        navigate("/finance/supplier-credits");
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
    if (!credit || !movement.targetPurchasePayableId) return;
    const confirmed = await message.confirm({
      title: "Revertir aplicación",
      message: `¿Revertir la aplicación de ${formatMoney(movement.amount)}? Esta acción no se puede deshacer.`,
      variant: "danger",
      confirmLabel: "Revertir aplicación",
    });
    if (!confirmed) return;
    setReversing(movement.id);
    try {
      await supplierCreditService.reverseApplication(credit.id, movement.id, {
        targetPurchasePayableId: movement.targetPurchasePayableId,
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
        effectiveDate: new Date().toISOString().slice(0, 10),
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

  const reversedMovementIds = new Set(
    (credit?.movements ?? [])
      .map((m) => m.reversalOfMovementId)
      .filter((v): v is string => !!v),
  );

  if (loading) {
    return (
      <PageShell title="Crédito de proveedor" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!credit) {
    return (
      <PageShell title="Crédito de proveedor">
        <ZHPageNotice variant="error" message="Crédito no encontrado" />
      </PageShell>
    );
  }

  const movementColumns: ZHDataTableColumn<SupplierCreditMovementDto>[] = [
    { key: "type", header: "Tipo", render: (m) => MOVEMENT_TYPE_LABEL[m.movementType] ?? m.movementType },
    { key: "amount", header: "Monto", align: "right", cellClassName: "zh-table-cell--num", render: (m) => formatMoney(m.amount) },
    { key: "date", header: "Fecha", render: (m) => formatDateTime(m.createdAtUtc) },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (m) => {
        const alreadyReversed = reversedMovementIds.has(m.id);
        const isReversal =
          m.movementType === "ReversalOfApplication" ||
          m.movementType === "ReversalOfRefund" ||
          m.movementType === "SourceReturnCancelled";
        if (m.movementType === "Application" && !alreadyReversed) {
          return (
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              disabled={reversing === m.id}
              onClick={() => void handleReverseApplication(m)}
            >
              Revertir
            </ZHBtn>
          );
        }
        if (m.movementType === "Refund" && !alreadyReversed) {
          return (
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              disabled={reversing === m.id}
              onClick={() => void handleReverseRefund(m)}
            >
              Revertir
            </ZHBtn>
          );
        }
        if (!isReversal && alreadyReversed) {
          return <span className="zh-text-muted">Revertido</span>;
        }
        return null;
      },
    },
  ];

  return (
    <PageShell
      title={`Crédito de proveedor — ${credit.supplierId}`}
      subtitle={`Moneda: ${credit.currencyCode}`}
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/finance/supplier-credits")}>
          Volver al listado
        </ZHBtn>
      }
    >
      <ZHCard
        title="Saldo"
        actions={
          <Badge label={credit.isOpen ? "Abierto" : "Cerrado"} variant={credit.isOpen ? "green" : "gray"} />
        }
      >
        <div className="sr-general-grid">
          <div>
            <span className="sr-general-grid__label">Monto original</span>
            <span className="sr-general-grid__value">{formatMoney(credit.originalAmount)}</span>
          </div>
          <div>
            <span className="sr-general-grid__label">Saldo disponible</span>
            <span className="sr-general-grid__value">
              <strong>{formatMoney(credit.availableAmount)}</strong>
            </span>
          </div>
        </div>
      </ZHCard>

      {credit.isOpen && (
        <ZHCard title="Acciones">
          <div className="sr-draft-actions">
            <ZHBtn type="button" variant="primary" onClick={() => setApplyOpen(true)}>
              Aplicar crédito
            </ZHBtn>
            <ZHBtn type="button" variant="secondary" onClick={() => setRefundOpen(true)}>
              Registrar reembolso
            </ZHBtn>
          </div>
        </ZHCard>
      )}

      <ZHCard title="Movimientos">
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
