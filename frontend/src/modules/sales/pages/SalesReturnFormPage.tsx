import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { SalesReturnInvoicePicker } from "../components/SalesReturnInvoicePicker";
import { ReturnableLinesEditor } from "../components/ReturnableLinesEditor";
import { AuthorizeSalesReturnModal } from "../components/AuthorizeSalesReturnModal";
import { SalesReturnSummary } from "../components/SalesReturnSummary";
import { SalesReturnCreditNoteSection } from "../components/SalesReturnCreditNoteSection";
import { salesService, type SalesListItemDto } from "../api/salesService";
import {
  salesReturnService,
  type SalesReturnDto,
  type ReturnableLineDto,
  type SalesReturnLineInput,
} from "../api/salesReturnService";
import {
  SALES_RETURN_STATUS_LABEL as STATUS_LABEL,
  SALES_RETURN_STATUS_BADGE as STATUS_BADGE,
} from "../utils/salesReturnStatus";
import "../styles/sales-return.css";

/**
 * Crear / editar (mientras Draft) / consultar una devolución de venta.
 * Consume exclusivamente los endpoints ya implementados de
 * `SalesReturnController` — ninguna regla de negocio (remanente, totales,
 * impuestos) se recalcula aquí: siempre se muestra lo que el servidor
 * devuelve tras crear/actualizar.
 */
export function SalesReturnFormPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isNew = !id;

  const [editing, setEditing] = useState<SalesReturnDto | null>(null);
  const [invoice, setInvoice] = useState<SalesListItemDto | null>(null);
  const [returnableLines, setReturnableLines] = useState<ReturnableLineDto[]>([]);
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(!isNew);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [authorizeOpen, setAuthorizeOpen] = useState(false);

  const isDraft = editing ? editing.status === "Draft" : true;
  const readOnly = !isNew && !!editing && editing.status !== "Draft";

  // ── Carga inicial (modo edición/consulta) ──────────────────────────────
  useEffect(() => {
    if (isNew) return;
    let cancelled = false;
    setLoading(true);
    salesReturnService
      .getById(id!)
      .then(async (dto) => {
        if (cancelled) return;
        setEditing(dto);
        setReason(dto.reason);
        try {
          const inv = await salesService.getById(dto.salesInvoiceId);
          if (cancelled) return;
          setInvoice({
            id: inv.id,
            invoiceNumber: inv.invoiceNumber,
            issueDate: inv.issueDate,
            customerId: inv.customerId,
            customerName: inv.customerName,
            status: inv.status,
            lineCount: inv.lines.length,
            grandTotal: inv.grandTotal,
            createdAt: inv.createdAt,
          });
        } catch {
          // La factura pudo no cargarse (p. ej. permisos) — no bloquea la vista de la devolución.
        }
        if (dto.status === "Draft") {
          const lines = await salesReturnService.getReturnableLines(
            dto.salesInvoiceId,
          );
          if (cancelled) return;
          setReturnableLines(lines);
          const q: Record<string, string> = {};
          for (const line of dto.lines) {
            q[line.originalInvoiceDetailId] = String(line.quantity);
          }
          setQuantities(q);
        }
      })
      .catch((err: unknown) => {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo cargar la devolución.",
          }),
        );
        navigate("/sales/returns");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, isNew, navigate]);

  // ── Selección de factura (modo creación) ───────────────────────────────
  const handleSelectInvoice = useCallback(async (inv: SalesListItemDto | null) => {
    setInvoice(inv);
    setQuantities({});
    setReturnableLines([]);
    if (!inv) return;
    try {
      const lines = await salesReturnService.getReturnableLines(inv.id);
      setReturnableLines(lines);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudieron cargar las líneas devolvibles de la factura.",
        }),
      );
    }
  }, []);

  const handleQuantityChange = (invoiceDetailId: string, value: string) => {
    setQuantities((prev) => ({ ...prev, [invoiceDetailId]: value }));
  };

  const selectedLines = useMemo<SalesReturnLineInput[]>(
    () =>
      Object.entries(quantities)
        .map(([invoiceDetailId, raw]) => ({
          invoiceDetailId,
          quantity: Number(raw),
        }))
        .filter((l) => l.quantity > 0),
    [quantities],
  );

  const anyLineExceedsRemaining = returnableLines.some((line) => {
    const qty = Number(quantities[line.invoiceDetailId] ?? 0);
    return qty > line.remainingQuantity;
  });

  const canSave =
    !!invoice &&
    reason.trim().length > 0 &&
    reason.trim().length <= 500 &&
    selectedLines.length > 0 &&
    !anyLineExceedsRemaining;

  // ── Guardar (crear o actualizar borrador) ──────────────────────────────
  const handleSave = async () => {
    if (!invoice || !canSave) return;
    setSaving(true);
    setSaveError("");
    try {
      const dto = editing
        ? await salesReturnService.updateDraft(editing.id, {
            id: editing.id,
            lines: selectedLines,
          })
        : await salesReturnService.createDraft({
            salesInvoiceId: invoice.id,
            reason: reason.trim(),
            lines: selectedLines,
          });
      setEditing(dto);
      message.success(
        editing
          ? "Borrador actualizado correctamente."
          : "Borrador de devolución creado correctamente.",
      );
      if (!editing) navigate(`/sales/returns/${dto.id}`, { replace: true });
    } catch (err: unknown) {
      setSaveError(
        formatApiRequestError(err, {
          generic: "No se pudieron guardar los cambios del borrador.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  // ── Cancelar borrador ───────────────────────────────────────────────────
  const handleCancelDraft = async () => {
    if (!editing) return;
    const confirmed = await message.confirm({
      title: "Cancelar devolución",
      message: `¿Cancelar el borrador ${editing.returnNumber}? Esta acción no se puede deshacer.`,
      variant: "danger",
      confirmLabel: "Cancelar devolución",
    });
    if (!confirmed) return;
    setSaving(true);
    try {
      const dto = await salesReturnService.cancelDraft(editing.id);
      setEditing(dto);
      message.success("Devolución cancelada.");
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cancelar la devolución." }),
      );
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <PageShell title="Devolución de Venta" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando devolución...</p>
        </ZHCard>
      </PageShell>
    );
  }

  const decimals = getDecimalConfig().totalAmount;

  return (
    <PageShell
      title={editing ? `Devolución ${editing.returnNumber}` : "Nueva devolución de venta"}
      subtitle={
        editing
          ? `Factura origen: ${invoice?.invoiceNumber ?? editing.salesInvoiceId}`
          : "Seleccione la factura autorizada a devolver"
      }
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/sales/returns")}>
          Volver al listado
        </ZHBtn>
      }
    >
      {editing && (
        <ZHCard
          title="Datos generales"
          actions={
            <Badge
              label={STATUS_LABEL[editing.status] ?? editing.status}
              variant={STATUS_BADGE[editing.status] ?? "gray"}
            />
          }
        >
          <div className="sr-general-grid">
            <div>
              <span className="sr-general-grid__label">N.º Devolución</span>
              <span className="sr-general-grid__value">{editing.returnNumber}</span>
            </div>
            <div>
              <span className="sr-general-grid__label">Factura origen</span>
              <span className="sr-general-grid__value">
                {invoice?.invoiceNumber ?? editing.salesInvoiceId}
              </span>
            </div>
            <div>
              <span className="sr-general-grid__label">Cliente</span>
              <span className="sr-general-grid__value">
                {invoice?.customerName ?? editing.customerId}
              </span>
            </div>
            <div>
              <span className="sr-general-grid__label">Creada</span>
              <span className="sr-general-grid__value">
                {formatDateTime(editing.createdAt)}
              </span>
            </div>
          </div>
        </ZHCard>
      )}

      <ZHCard title={isNew && !editing ? "Factura a devolver" : "Motivo"}>
        {isNew && !editing && (
          <ZHField label="Factura" required>
            <SalesReturnInvoicePicker value={invoice} onChange={handleSelectInvoice} />
          </ZHField>
        )}
        <ZHField label="Motivo de la devolución" required readOnly={readOnly}>
          {readOnly ? (
            <p className="sr-reason-readonly">{editing?.reason}</p>
          ) : (
            <textarea
              className="zh-input"
              rows={3}
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Describa el motivo de la devolución..."
            />
          )}
        </ZHField>
      </ZHCard>

      {invoice && (isDraft || !editing) && (
        <ZHCard title="Líneas a devolver">
          <ReturnableLinesEditor
            lines={returnableLines}
            quantities={quantities}
            onChangeQuantity={handleQuantityChange}
            disabled={saving}
          />

          {saveError ? (
            <ZHPageNotice variant="error" message="Error" detail={saveError} />
          ) : null}

          <ZHFormActions
            onCancel={() => navigate("/sales/returns")}
            onSave={() => void handleSave()}
            hideDraft
            disableSave={saving || !canSave}
            labels={{
              cancel: "Volver",
              save: saving
                ? "Guardando..."
                : editing
                  ? "Guardar cambios"
                  : "Crear borrador",
            }}
          />
        </ZHCard>
      )}

      {editing && <SalesReturnSummary salesReturn={editing} decimals={decimals} />}

      {editing?.status === "Authorized" && (
        <SalesReturnCreditNoteSection
          salesReturnId={editing.id}
          returnNumber={editing.returnNumber}
        />
      )}

      {editing && editing.status === "Draft" && (
        <ZHCard title="Acciones">
          <div className="sr-draft-actions">
            <ZHBtn
              type="button"
              variant="destructive"
              onClick={() => void handleCancelDraft()}
              disabled={saving}
            >
              Cancelar devolución
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="primary"
              onClick={() => setAuthorizeOpen(true)}
              disabled={saving || editing.lines.length === 0}
            >
              Autorizar devolución
            </ZHBtn>
          </div>
        </ZHCard>
      )}

      <AuthorizeSalesReturnModal
        open={authorizeOpen}
        salesReturn={editing}
        onClose={() => setAuthorizeOpen(false)}
        onAuthorized={(updated) => setEditing(updated)}
      />
    </PageShell>
  );
}
