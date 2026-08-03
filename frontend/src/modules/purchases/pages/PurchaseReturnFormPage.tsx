import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { PurchaseReturnableLinesEditor } from "../components/PurchaseReturnableLinesEditor";
import { purchaseService, type PurchaseInvoiceDto } from "../api/purchaseService";
import {
  purchaseReturnService,
  type ReturnableLineDto,
} from "../api/purchaseReturnService";
import {
  purchaseReturnDraftSchema,
  emptyPurchaseReturnDraftForm,
  type PurchaseReturnDraftFormValues,
} from "../schemas/purchaseReturnSchema";

/**
 * Crea un borrador de devolución de compra contra una factura confirmada. La
 * factura origen llega siempre por `?invoiceId=` (botón "Devolución" en
 * `PurchasesPage.tsx`, detalle de la factura) — a diferencia de
 * `SalesReturnFormPage.tsx`, esta página no incluye un selector de factura
 * propio (`SalesReturnInvoicePicker` no aplica aquí, Fase 12 solo crea, la
 * edición de un Draft ya existente vive en `PurchaseReturnDetailPage.tsx`).
 * React Hook Form + Zod desde el inicio (F-V1/F-V2, plan Fase 12 — no repite
 * la deuda técnica de `SalesReturnFormPage.tsx` con `useState` manual).
 */
export function PurchaseReturnFormPage() {
  const [searchParams] = useSearchParams();
  const invoiceId = searchParams.get("invoiceId") ?? "";
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState<PurchaseInvoiceDto | null>(null);
  const [returnableLines, setReturnableLines] = useState<ReturnableLineDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  const {
    register,
    handleSubmit,
    setError,
    control,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseReturnDraftFormValues>({
    resolver: zodResolver(purchaseReturnDraftSchema),
    defaultValues: emptyPurchaseReturnDraftForm(),
  });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  const selectedLines = watch("lines");

  useEffect(() => {
    if (!invoiceId) {
      setLoadError("Falta la factura de compra origen.");
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    Promise.all([
      purchaseService.getById(invoiceId),
      purchaseReturnService.getReturnableLines(invoiceId),
    ])
      .then(([inv, lines]) => {
        if (cancelled) return;
        setInvoice(inv);
        setReturnableLines(lines);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setLoadError(
          formatApiRequestError(err, {
            generic: "No se pudo cargar la factura de compra o sus líneas devolvibles.",
          }),
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [invoiceId]);

  const anyLineExceedsRemaining = returnableLines.some((line) => {
    const selected = selectedLines.find(
      (l) => l.originalInvoiceDetailId === line.invoiceDetailId,
    );
    return (selected?.quantity ?? 0) > line.remainingQuantity;
  });

  const onSubmit = async (values: PurchaseReturnDraftFormValues) => {
    if (!invoice) return;
    try {
      const dto = await purchaseReturnService.createDraft({
        clientRequestId: crypto.randomUUID(),
        purchaseInvoiceId: invoice.id,
        reason: values.reason,
        lines: values.lines,
      });
      message.success("Borrador de devolución creado correctamente.");
      navigate(`/purchases/returns/${dto.id}`, { replace: true });
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo crear el borrador de la devolución.",
          }),
        );
      }
    }
  };

  if (loading) {
    return (
      <PageShell title="Nueva devolución de compra" subtitle="Cargando...">
        <ZHCard>
          <p>Cargando...</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (loadError || !invoice) {
    return (
      <PageShell title="Nueva devolución de compra">
        <ZHPageNotice
          variant="error"
          message="No se pudo iniciar la devolución"
          detail={loadError}
        />
      </PageShell>
    );
  }

  return (
    <PageShell
      title="Nueva devolución de compra"
      subtitle={`Factura origen: ${invoice.invoiceNumber} — ${invoice.supplierName}`}
    >
      <form onSubmit={(e) => void handleSubmit(onSubmit)(e)}>
        <ZHCard title="Motivo">
          <ZHField label="Motivo de la devolución" required fieldError={errors.reason?.message}>
            <textarea
              className="zh-input"
              rows={3}
              maxLength={500}
              {...register("reason")}
              placeholder="Describa el motivo de la devolución..."
            />
          </ZHField>
        </ZHCard>

        <ZHCard title="Líneas a devolver">
          <PurchaseReturnableLinesEditor
            returnableLines={returnableLines}
            selected={fields}
            append={append}
            remove={remove}
            disabled={isSubmitting}
          />
          {errors.lines && !Array.isArray(errors.lines) ? (
            <ZHPageNotice variant="error" message={errors.lines.message ?? ""} />
          ) : null}

          <ZHFormActions
            onCancel={() => navigate("/purchases")}
            onSave={() => void handleSubmit(onSubmit)()}
            hideDraft
            disableSave={isSubmitting || anyLineExceedsRemaining || fields.length === 0}
            labels={{
              cancel: "Volver",
              save: isSubmitting ? "Guardando..." : "Crear borrador",
            }}
          />
        </ZHCard>
      </form>
    </PageShell>
  );
}
