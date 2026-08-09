import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextarea } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { PurchaseReturnableLinesEditor } from "./PurchaseReturnableLinesEditor";
import type { PurchaseInvoiceDto } from "../api/purchaseService";
import {
  purchaseReturnService,
  type PurchaseReturnDto,
  type ReturnableLineDto,
} from "../api/purchaseReturnService";
import {
  purchaseReturnDraftSchema,
  emptyPurchaseReturnDraftForm,
  type PurchaseReturnDraftFormValues,
} from "../schemas/purchaseReturnSchema";

interface Props {
  invoice: PurchaseInvoiceDto;
  onCreated: (dto: PurchaseReturnDto) => void;
  onCancel?: () => void;
}

/**
 * FLOW-READY-02C.3R — sección de creación de un borrador de `PurchaseReturn`, extraída de
 * `PurchaseReturnFormPage.tsx` para reutilizarla también dentro de la pantalla centralizada
 * `PurchaseCreditNoteFormPage.tsx` cuando el usuario elige "Devolución de productos" (diseño
 * FLOW-READY-02C §0.1: la UI se centraliza, pero la lógica de negocio de devolución sigue siendo
 * exclusivamente `PurchaseReturn` — este componente llama a `purchaseReturnService.createDraft`
 * directamente, nunca a `purchaseCreditNoteService`). El componente recibe la factura ya cargada
 * por el padre (ambas pantallas ya la cargan por separado vía `?invoiceId=`) — solo resuelve las
 * líneas devolvibles, que sí son específicas de este flujo.
 *
 * `onCreated` deja la navegación post-creación a cargo del padre: `PurchaseReturnFormPage` navega a
 * `/purchases/returns/:id` (sin cambios de comportamiento) y `PurchaseCreditNoteFormPage` navega al
 * mismo destino (regla explícita del encargo — nunca crea un `PurchaseCreditNote` para devolución).
 */
export function PurchaseReturnDraftFormSection({ invoice, onCreated, onCancel }: Readonly<Props>) {
  const { t } = useI18n();
  const [returnableLines, setReturnableLines] = useState<ReturnableLineDto[]>([]);
  const [loadingLines, setLoadingLines] = useState(true);
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
    let cancelled = false;
    setLoadingLines(true);
    purchaseReturnService
      .getReturnableLines(invoice.id)
      .then((lines) => {
        if (cancelled) return;
        setReturnableLines(lines);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setLoadError(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.loadReturnableLines",
              "No se pudieron cargar las líneas devolvibles de la factura.",
            ),
          }),
        );
      })
      .finally(() => {
        if (!cancelled) setLoadingLines(false);
      });
    return () => {
      cancelled = true;
    };
  }, [invoice.id, t]);

  const anyLineExceedsRemaining = returnableLines.some((line) => {
    const selected = selectedLines.find(
      (l) => l.originalInvoiceDetailId === line.invoiceDetailId,
    );
    return (selected?.quantity ?? 0) > line.remainingQuantity;
  });

  const onSubmit = async (values: PurchaseReturnDraftFormValues) => {
    try {
      const dto = await purchaseReturnService.createDraft({
        clientRequestId: crypto.randomUUID(),
        purchaseInvoiceId: invoice.id,
        reason: values.reason,
        lines: values.lines,
      });
      onCreated(dto);
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.createReturnFailed",
              "No se pudo crear el borrador de la devolución.",
            ),
          }),
        );
      }
    }
  };

  if (loadingLines) {
    return (
      <ZHCard>
        <p>{t("common.loading", "Cargando...")}</p>
      </ZHCard>
    );
  }

  if (loadError) {
    return <ZHPageNotice variant="error" message={loadError} />;
  }

  return (
    <form onSubmit={(e) => void handleSubmit(onSubmit)(e)}>
      <ZHCard title={t("purchases.creditNote.reason.title", "Concepto / motivo")}>
        <ZHField
          label={t("purchases.creditNote.reason.label", "Motivo")}
          required
          fieldError={errors.reason?.message}
        >
          <ZhTextarea
            className="zh-input"
            rows={3}
            maxLength={500}
            {...register("reason")}
            placeholder={t(
              "purchases.creditNote.reason.returnPlaceholder",
              "Describa el motivo de la devolución...",
            )}
          />
        </ZHField>
      </ZHCard>

      <ZHCard title={t("purchases.creditNote.return.linesTitle", "Líneas a devolver")}>
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
      </ZHCard>

      <ZHFormActions
        onCancel={onCancel}
        hideCancel={!onCancel}
        onSave={() => void handleSubmit(onSubmit)()}
        hideDraft
        disableSave={isSubmitting || anyLineExceedsRemaining || fields.length === 0}
        labels={{
          cancel: t("common.back", "Volver"),
          save: isSubmitting
            ? t("common.saving", "Guardando...")
            : t("purchases.creditNote.return.createButton", "Crear devolución"),
        }}
      />
    </form>
  );
}
