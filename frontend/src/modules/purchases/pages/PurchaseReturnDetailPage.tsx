import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { PurchaseReturnableLinesEditor } from "../components/PurchaseReturnableLinesEditor";
import { PurchaseReturnCreditNoteSection } from "../components/PurchaseReturnCreditNoteSection";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import {
  purchaseReturnService,
  type PurchaseReturnDto,
  type ReturnableLineDto,
} from "../api/purchaseReturnService";
import {
  purchaseReturnDraftSchema,
  cancelPurchaseReturnSchema,
  type PurchaseReturnDraftFormValues,
  type CancelPurchaseReturnFormValues,
} from "../schemas/purchaseReturnSchema";
import {
  PURCHASE_RETURN_STATUS_LABEL as STATUS_LABEL,
  PURCHASE_RETURN_STATUS_BADGE as STATUS_BADGE,
  PURCHASE_RETURN_FISCAL_STATUS_LABEL as FISCAL_STATUS_LABEL,
} from "../utils/purchaseReturnStatus";
import "../../sales/styles/sales-return.css";

/**
 * Detalle de una devolución de compra: edición del borrador (Draft),
 * autorización, cancelación (con reversa completa si estaba Authorized) y
 * vínculo de Nota de Crédito del proveedor — todo en una sola página, mismo
 * criterio que `SalesReturnFormPage.tsx` para el modo "consulta/edición"
 * (aquí separado de la creación por decisión explícita del plan Fase 12).
 * React Hook Form + Zod en ambos formularios internos (edición de Draft y
 * cancelación) — F-V1/F-V2.
 */
export function PurchaseReturnDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [editing, setEditing] = useState<PurchaseReturnDto | null>(null);
  const [invoice, setInvoice] = useState<PurchaseInvoiceDto | null>(null);
  const [returnableLines, setReturnableLines] = useState<ReturnableLineDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [authorizing, setAuthorizing] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const draftForm = useForm<PurchaseReturnDraftFormValues>({
    resolver: zodResolver(purchaseReturnDraftSchema),
    defaultValues: { reason: "", lines: [] },
  });
  const { fields, append, remove } = useFieldArray({
    control: draftForm.control,
    name: "lines",
  });
  const selectedLines = draftForm.watch("lines");

  const cancelForm = useForm<CancelPurchaseReturnFormValues>({
    resolver: zodResolver(cancelPurchaseReturnSchema),
    defaultValues: { reason: "" },
  });

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    purchaseReturnService
      .getById(id)
      .then(async (dto) => {
        if (cancelled) return;
        setEditing(dto);
        draftForm.reset({
          reason: dto.reason,
          lines: dto.lines.map((l) => ({
            originalInvoiceDetailId: l.originalInvoiceDetailId,
            quantity: l.quantity,
          })),
        });
        try {
          const inv = await purchaseService.getById(dto.purchaseInvoiceId);
          if (cancelled) return;
          setInvoice(inv);
        } catch {
          // La factura pudo no cargarse (p. ej. permisos) — no bloquea la vista de la devolución.
        }
        if (dto.status === "Draft") {
          const lines = await purchaseReturnService.getReturnableLines(
            dto.purchaseInvoiceId,
          );
          if (cancelled) return;
          setReturnableLines(lines);
        }
      })
      .catch((err: unknown) => {
        message.error(
          formatApiRequestError(err, { generic: "No se pudo cargar la devolución." }),
        );
        navigate("/purchases/returns");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, navigate]);

  const anyLineExceedsRemaining = returnableLines.some((line) => {
    const selected = selectedLines.find(
      (l) => l.originalInvoiceDetailId === line.invoiceDetailId,
    );
    return (selected?.quantity ?? 0) > line.remainingQuantity;
  });

  const handleSaveDraft = async (values: PurchaseReturnDraftFormValues) => {
    if (!editing) return;
    try {
      const dto = await purchaseReturnService.updateDraft(editing.id, values);
      setEditing(dto);
      message.success("Borrador actualizado correctamente.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, draftForm.setError, (msg) =>
        message.error(msg),
      );
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudieron guardar los cambios del borrador.",
          }),
        );
      }
    }
  };

  const handleAuthorize = async () => {
    if (!editing) return;
    setAuthorizing(true);
    try {
      const dto = await purchaseReturnService.authorize(editing.id, crypto.randomUUID());
      setEditing(dto);
      message.success("Devolución autorizada correctamente.");
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo autorizar la devolución." }),
      );
    } finally {
      setAuthorizing(false);
    }
  };

  const handleCancel = async (values: CancelPurchaseReturnFormValues) => {
    if (!editing) return;
    const confirmed = await message.confirm({
      title: "Cancelar devolución",
      message:
        editing.status === "Authorized"
          ? `¿Cancelar la devolución ${editing.returnNumber}? Se reversará el inventario, la cuenta por pagar y el crédito de proveedor si aplica. Esta acción no se puede deshacer.`
          : `¿Cancelar el borrador ${editing.returnNumber ?? ""}? Esta acción no se puede deshacer.`,
      variant: "danger",
      confirmLabel: "Cancelar devolución",
    });
    if (!confirmed) return;
    try {
      const dto = await purchaseReturnService.cancel(editing.id, {
        reason: values.reason,
        clientRequestId: crypto.randomUUID(),
      });
      setEditing(dto);
      setCancelling(false);
      message.success("Devolución cancelada.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, cancelForm.setError, (msg) =>
        message.error(msg),
      );
      if (!applied) {
        message.error(
          formatApiRequestError(err, { generic: "No se pudo cancelar la devolución." }),
        );
      }
    }
  };

  if (loading) {
    return (
      <PageShell title="Devolución de compra" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando devolución...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!editing) {
    return (
      <PageShell title="Devolución de compra">
        <ZHPageNotice variant="error" message="Devolución no encontrada" />
      </PageShell>
    );
  }

  const isDraft = editing.status === "Draft";
  const isAuthorized = editing.status === "Authorized";
  const isCancelled = editing.status === "Cancelled";

  return (
    <PageShell
      title={`Devolución ${editing.returnNumber ?? "(borrador)"}`}
      subtitle={
        invoice
          ? `Factura origen: ${invoice.invoiceNumber} — ${invoice.supplierName}`
          : `Factura origen: ${editing.purchaseInvoiceId}`
      }
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/purchases/returns")}>
          Volver al listado
        </ZHBtn>
      }
    >
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
            <span className="sr-general-grid__value">{editing.returnNumber ?? "—"}</span>
          </div>
          <div>
            <span className="sr-general-grid__label">Estado fiscal</span>
            <span className="sr-general-grid__value">
              {FISCAL_STATUS_LABEL[editing.fiscalStatus] ?? editing.fiscalStatus}
            </span>
          </div>
          <div>
            <span className="sr-general-grid__label">Creada</span>
            <span className="sr-general-grid__value">{formatDateTime(editing.createdAt)}</span>
          </div>
          {editing.authorizedGrandTotal !== null && (
            <div>
              <span className="sr-general-grid__label">Total autorizado</span>
              <span className="sr-general-grid__value">
                {formatMoney(editing.authorizedGrandTotal)}
              </span>
            </div>
          )}
        </div>
      </ZHCard>

      <ZHCard title="Motivo">
        {isDraft ? (
          <ZHField
            label="Motivo de la devolución"
            required
            fieldError={draftForm.formState.errors.reason?.message}
          >
            <textarea
              className="zh-input"
              rows={3}
              maxLength={500}
              {...draftForm.register("reason")}
            />
          </ZHField>
        ) : (
          <p className="sr-reason-readonly">{editing.reason}</p>
        )}
      </ZHCard>

      {isDraft && (
        <ZHCard title="Líneas a devolver">
          <PurchaseReturnableLinesEditor
            returnableLines={returnableLines}
            selected={fields}
            append={append}
            remove={remove}
            disabled={draftForm.formState.isSubmitting}
          />
          <ZHFormActions
            hideCancel
            onSave={() => void draftForm.handleSubmit(handleSaveDraft)()}
            hideDraft
            disableSave={
              draftForm.formState.isSubmitting ||
              anyLineExceedsRemaining ||
              fields.length === 0
            }
            labels={{
              save: draftForm.formState.isSubmitting ? "Guardando..." : "Guardar cambios",
            }}
          />
        </ZHCard>
      )}

      {!isDraft && (
        <ZHCard title="Líneas devueltas">
          <div className="table-scroll">
            <table className="table table--compact table--neutral sr-lines-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th className="zh-text-align-right">Cantidad</th>
                  <th>Bodega</th>
                </tr>
              </thead>
              <tbody>
                {editing.lines.map((line) => (
                  <tr key={line.id}>
                    <td>{line.itemId}</td>
                    <td className="zh-table-cell--num">{line.quantity}</td>
                    <td>{line.warehouseId}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ZHCard>
      )}

      {isAuthorized && (
        <PurchaseReturnCreditNoteSection purchaseReturn={editing} onLinked={setEditing} />
      )}

      {(isDraft || isAuthorized) && (
        <ZHCard title="Acciones">
          {!cancelling ? (
            <div className="sr-draft-actions">
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => setCancelling(true)}
              >
                Cancelar devolución
              </ZHBtn>
              {isDraft && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  onClick={() => void handleAuthorize()}
                  disabled={authorizing || editing.lines.length === 0}
                >
                  {authorizing ? "Autorizando..." : "Autorizar devolución"}
                </ZHBtn>
              )}
            </div>
          ) : (
            <form onSubmit={(e) => void cancelForm.handleSubmit(handleCancel)(e)}>
              <ZHField
                label="Motivo de la cancelación"
                required
                fieldError={cancelForm.formState.errors.reason?.message}
              >
                <textarea
                  className="zh-input"
                  rows={3}
                  maxLength={500}
                  {...cancelForm.register("reason")}
                />
              </ZHField>
              <ZHFormActions
                onCancel={() => setCancelling(false)}
                onSave={() => void cancelForm.handleSubmit(handleCancel)()}
                hideDraft
                disableSave={cancelForm.formState.isSubmitting}
                labels={{
                  cancel: "Volver",
                  save: cancelForm.formState.isSubmitting
                    ? "Cancelando..."
                    : "Confirmar cancelación",
                }}
              />
            </form>
          )}
        </ZHCard>
      )}

      {isCancelled && editing.cancellationReason && (
        <ZHPageNotice
          variant="info"
          message="Devolución cancelada"
          detail={editing.cancellationReason}
        />
      )}
    </PageShell>
  );
}
