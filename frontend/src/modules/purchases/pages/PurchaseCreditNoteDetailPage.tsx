import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhTextarea } from "../../../components/zh/inputs";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  purchaseCreditNoteService,
  type PurchaseCreditNoteDto,
} from "../api/purchaseCreditNoteService";
import { PurchaseCreditNoteDiscountLinesEditor } from "../components/PurchaseCreditNoteDiscountLinesEditor";
import {
  purchaseCreditNoteDraftSchema,
  cancelPurchaseCreditNoteSchema,
  type PurchaseCreditNoteDraftFormValues,
  type CancelPurchaseCreditNoteFormValues,
} from "../schemas/purchaseCreditNoteSchema";
import {
  getPurchaseCreditNoteStatusLabel,
  PURCHASE_CREDIT_NOTE_STATUS_BADGE as STATUS_BADGE,
} from "../utils/purchaseCreditNoteStatus";
import "../styles/purchase-credit-note.css";

/**
 * Detalle de una nota de crédito de compra (descuento/promoción, v1): edición del borrador,
 * autorización y cancelación — mismo patrón de página que `PurchaseReturnDetailPage.tsx`. Nunca
 * muestra ni pide item/bodega/cantidad (§0.1/§2.2 del diseño — esta entidad nunca mueve
 * inventario). El bloqueo por excedente de saldo es siempre responsabilidad del backend; aquí solo
 * se muestra una advertencia preventiva si ya se conoce `invoiceBalanceDue`.
 */
export function PurchaseCreditNoteDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useI18n();

  const [editing, setEditing] = useState<PurchaseCreditNoteDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [authorizing, setAuthorizing] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const draftForm = useForm<PurchaseCreditNoteDraftFormValues>({
    resolver: zodResolver(purchaseCreditNoteDraftSchema),
    defaultValues: { creditNoteNumber: "", accessKey: "", authorizationNumber: "", authorizationDate: "", issueDate: "", reason: "", lines: [] },
  });
  const { fields, append, remove } = useFieldArray({
    control: draftForm.control,
    name: "lines",
  });

  const cancelForm = useForm<CancelPurchaseCreditNoteFormValues>({
    resolver: zodResolver(cancelPurchaseCreditNoteSchema),
    defaultValues: { reason: "" },
  });

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    purchaseCreditNoteService
      .getById(id)
      .then((dto) => {
        if (cancelled) return;
        setEditing(dto);
        draftForm.reset({
          creditNoteNumber: dto.creditNoteNumber,
          accessKey: dto.accessKey ?? "",
          authorizationNumber: dto.authorizationNumber ?? "",
          authorizationDate: dto.authorizationDate ?? "",
          issueDate: dto.issueDate.slice(0, 10),
          reason: dto.reason,
          lines: dto.lines.map((l) => ({
            description: l.description,
            subtotal: l.subtotal,
            vatCode: l.vatCode,
            vatRate: l.vatRate,
            vatAmount: l.vatAmount,
          })),
        });
      })
      .catch((err: unknown) => {
        message.error(
          formatApiRequestError(err, {
            generic: t("purchases.creditNote.errors.loadFailed", "No se pudo cargar la nota de crédito."),
          }),
        );
        navigate("/purchases");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, navigate]);

  const handleSaveDraft = async (values: PurchaseCreditNoteDraftFormValues) => {
    if (!editing) return;
    try {
      const dto = await purchaseCreditNoteService.updateDraft(editing.id, {
        creditNoteNumber: values.creditNoteNumber,
        accessKey: values.accessKey?.trim() || null,
        authorizationNumber: values.authorizationNumber?.trim() || null,
        authorizationDate: values.authorizationDate?.trim() || null,
        issueDate: values.issueDate,
        reason: values.reason,
        lines: values.lines.map((l) => ({
          description: l.description,
          subtotal: l.subtotal,
          vatCode: l.vatCode ?? null,
          vatRate: l.vatRate ?? null,
          vatAmount: l.vatAmount,
        })),
      });
      setEditing(dto);
      message.success(t("purchases.creditNote.messages.updated", "Borrador actualizado correctamente."));
    } catch (err: unknown) {
      const applied = applyServerErrors(err, draftForm.setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.updateFailed",
              "No se pudieron guardar los cambios del borrador.",
            ),
          }),
        );
      }
    }
  };

  const handleAuthorize = async () => {
    if (!editing) return;
    setAuthorizing(true);
    try {
      const dto = await purchaseCreditNoteService.authorize(editing.id, crypto.randomUUID());
      setEditing(dto);
      message.success(
        t("purchases.creditNote.messages.authorized", "Nota de crédito autorizada correctamente."),
      );
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: t(
            "purchases.creditNote.errors.exceedsBalance",
            "El valor supera el saldo pendiente.",
          ),
        }),
      );
    } finally {
      setAuthorizing(false);
    }
  };

  const handleCancel = async (values: CancelPurchaseCreditNoteFormValues) => {
    if (!editing) return;
    const confirmed = await message.confirm({
      title: t("purchases.creditNote.actions.cancel", "Cancelar nota de crédito"),
      message:
        editing.status === "Authorized"
          ? t(
              "purchases.creditNote.confirm.cancelAuthorized",
              "¿Cancelar esta nota de crédito? Se reversará el monto aplicado a la cuenta por pagar. Esta acción no se puede deshacer.",
            )
          : t(
              "purchases.creditNote.confirm.cancelDraft",
              "¿Cancelar este borrador? Esta acción no se puede deshacer.",
            ),
      variant: "danger",
      confirmLabel: t("purchases.creditNote.actions.cancel", "Cancelar nota de crédito"),
    });
    if (!confirmed) return;
    try {
      const dto = await purchaseCreditNoteService.cancel(editing.id, {
        reason: values.reason,
        clientRequestId: crypto.randomUUID(),
      });
      setEditing(dto);
      setCancelling(false);
      message.success(t("purchases.creditNote.messages.cancelled", "Nota de crédito cancelada."));
    } catch (err: unknown) {
      const applied = applyServerErrors(err, cancelForm.setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.cancelFailed",
              "No se pudo cancelar la nota de crédito.",
            ),
          }),
        );
      }
    }
  };

  if (loading) {
    return (
      <PageShell
        title={t("purchases.creditNote.title", "Nota de crédito de compra")}
        subtitle={t("common.loading", "Cargando...")}
      >
        <ZHCard>
          <p>{t("common.loading", "Cargando...")}</p>
        </ZHCard>
      </PageShell>
    );
  }

  if (!editing) {
    return (
      <PageShell title={t("purchases.creditNote.title", "Nota de crédito de compra")}>
        <ZHPageNotice
          variant="error"
          message={t("purchases.creditNote.errors.notFound", "Nota de crédito no encontrada")}
        />
      </PageShell>
    );
  }

  const isDraft = editing.status === "Draft";
  const isAuthorized = editing.status === "Authorized";
  const isCancelled = editing.status === "Cancelled";
  // FLOW-READY-02C-R1.1: las notas de crédito tipo Devolución nunca se autorizan aquí — se aplican
  // mediante el PurchaseReturn vinculado (backend rechaza Authorize() para este tipo).
  const isDiscountType = editing.applicationType === "Discount";
  const exceedsBalance =
    editing.invoiceBalanceDue !== null && editing.totalAmount > editing.invoiceBalanceDue;

  return (
    <PageShell
      title={`${t("purchases.creditNote.title", "Nota de crédito de compra")} ${editing.creditNoteNumber}`}
      subtitle={
        editing.invoiceNumber
          ? `${t("purchases.creditNote.affectedInvoice.title", "Factura afectada")}: ${editing.invoiceNumber} — ${editing.supplierName ?? ""}`
          : undefined
      }
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/purchases")}>
          {t("common.back", "Volver")}
        </ZHBtn>
      }
    >
      <ZHCard
        title={t("purchases.creditNote.general.title", "Datos generales")}
        actions={
          <Badge
            label={getPurchaseCreditNoteStatusLabel(editing.status, t)}
            variant={STATUS_BADGE[editing.status] ?? "gray"}
          />
        }
      >
        <div className="pcn-summary-grid">
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.fiscal.number", "Número NC")}
            </span>
            <span className="pcn-summary-grid__value">{editing.creditNoteNumber}</span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.balanceDue", "Saldo pendiente")}
            </span>
            <span className="pcn-summary-grid__value">
              <ZHMoneyValue value={editing.invoiceBalanceDue} currencySymbol="" />
            </span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.lines.total", "Total crédito")}
            </span>
            <span className="pcn-summary-grid__value">
              <ZHMoneyValue value={editing.totalAmount} currencySymbol="" />
            </span>
          </div>
          {editing.appliedToPayableAmount !== null && (
            <div>
              <span className="pcn-summary-grid__label">
                {t("purchases.creditNote.summary.reducesPayable", "Reduce CxP")}
              </span>
              <span className="pcn-summary-grid__value">
                <ZHMoneyValue value={editing.appliedToPayableAmount} currencySymbol="" />
              </span>
            </div>
          )}
        </div>
        {!isDiscountType && (
          <>
            <ZHPageNotice
              variant="info"
              message={t(
                "purchases.creditNote.notices.returnTypeInfo",
                "Esta nota de crédito es de tipo Devolución: se aplica mediante la devolución de compra vinculada, no se autoriza aquí.",
              )}
            />
            {editing.linkedPurchaseReturnId && (
              <div className="pcn-actions-row">
                <ZHBtn
                  type="button"
                  variant="ghost"
                  onClick={() => navigate(`/purchases/returns/${editing.linkedPurchaseReturnId}`)}
                >
                  {t("purchases.creditNote.actions.viewLinkedReturn", "Ver devolución vinculada")}
                </ZHBtn>
              </div>
            )}
          </>
        )}
        {isDraft && exceedsBalance && (
          <ZHPageNotice
            variant="warning"
            message={t(
              "purchases.creditNote.errors.exceedsBalance",
              "El valor supera el saldo pendiente.",
            )}
          />
        )}
      </ZHCard>

      <ZHCard title={t("purchases.creditNote.fiscal.title", "Nota de crédito recibida / datos fiscales")}>
        {isDraft ? (
          <div className="pcn-fiscal-grid">
            <ZHField
              label={t("purchases.creditNote.fiscal.number", "Número NC")}
              required
              fieldError={draftForm.formState.errors.creditNoteNumber?.message}
            >
              <ZhTextInput className="zh-input" maxLength={17} {...draftForm.register("creditNoteNumber")} />
            </ZHField>
            <ZHField label={t("purchases.creditNote.fiscal.accessKey", "Clave de acceso")}>
              <ZhTextInput className="zh-input" maxLength={49} {...draftForm.register("accessKey")} />
            </ZHField>
            <ZHField label={t("purchases.creditNote.fiscal.authorizationNumber", "Autorización")}>
              <ZhTextInput
                className="zh-input"
                maxLength={49}
                {...draftForm.register("authorizationNumber")}
              />
            </ZHField>
            <ZHField
              label={t("purchases.creditNote.fiscal.issueDate", "Fecha emisión")}
              required
              fieldError={draftForm.formState.errors.issueDate?.message}
            >
              <ZhDateInput {...draftForm.register("issueDate")} />
            </ZHField>
          </div>
        ) : (
          <div className="pcn-summary-grid">
            <div>
              <span className="pcn-summary-grid__label">
                {t("purchases.creditNote.fiscal.accessKey", "Clave de acceso")}
              </span>
              <span className="pcn-summary-grid__value">{editing.accessKey ?? "—"}</span>
            </div>
            <div>
              <span className="pcn-summary-grid__label">
                {t("purchases.creditNote.fiscal.authorizationNumber", "Autorización")}
              </span>
              <span className="pcn-summary-grid__value">{editing.authorizationNumber ?? "—"}</span>
            </div>
            <div>
              <span className="pcn-summary-grid__label">
                {t("purchases.creditNote.fiscal.issueDate", "Fecha emisión")}
              </span>
              <span className="pcn-summary-grid__value">{formatDate(editing.issueDate)}</span>
            </div>
          </div>
        )}
      </ZHCard>

      <ZHCard title={t("purchases.creditNote.reason.title", "Concepto / motivo")}>
        {isDraft ? (
          <ZHField
            label={t("purchases.creditNote.reason.label", "Motivo")}
            required
            fieldError={draftForm.formState.errors.reason?.message}
          >
            <ZhTextarea className="zh-input" rows={3} maxLength={500} {...draftForm.register("reason")} />
          </ZHField>
        ) : (
          <p className="pcn-hint">{editing.reason}</p>
        )}
      </ZHCard>

      {editing.taxSummaries.length > 0 && (
        <ZHCard
          title={t(
            "purchases.creditNote.taxSummaryLines.title",
            "Descuento por resumen fiscal de compra",
          )}
        >
          <div className="table-scroll">
            <table className="table table--compact table--neutral pcn-lines-table">
              <thead>
                <tr>
                  <th>{t("purchases.creditNote.taxSummaryLines.tax", "Impuesto")}</th>
                  <th className="zh-text-align-right">
                    {t("purchases.creditNote.taxSummaryLines.discountBase", "Base descuento a aplicar")}
                  </th>
                  <th className="zh-text-align-right">
                    {t("purchases.creditNote.taxSummaryLines.iceCredit", "ICE crédito")}
                  </th>
                  <th className="zh-text-align-right">
                    {t("purchases.creditNote.taxSummaryLines.vatCredit", "IVA crédito")}
                  </th>
                  <th className="zh-text-align-right">
                    {t("purchases.creditNote.taxSummaryLines.totalCredit", "Total NC")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {editing.taxSummaries.map((summary) => (
                  <tr key={summary.id}>
                    <td>
                      {summary.vatName ?? summary.vatCode}
                      {summary.iceCode && ` + ${summary.iceName ?? summary.iceCode}`}
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={summary.taxableBase} currencySymbol="" />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={summary.iceAmount} currencySymbol="" />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={summary.vatAmount} currencySymbol="" />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={summary.totalAmount} currencySymbol="" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ZHCard>
      )}

      {(isDraft || editing.lines.length > 0) && (
        <ZHCard title={t("purchases.creditNote.lines.title", "Líneas de descuento / promoción")}>
          {isDraft ? (
            <>
              <PurchaseCreditNoteDiscountLinesEditor
                register={draftForm.register}
                fields={fields}
                append={append}
                remove={remove}
                disabled={draftForm.formState.isSubmitting}
              />
              <ZHFormActions
                hideCancel
                onSave={() => void draftForm.handleSubmit(handleSaveDraft)()}
                hideDraft
                disableSave={draftForm.formState.isSubmitting || fields.length === 0}
                labels={{
                  save: draftForm.formState.isSubmitting
                    ? t("common.saving", "Guardando...")
                    : t("common.saveChanges", "Guardar cambios"),
                }}
              />
            </>
          ) : (
            <div className="table-scroll">
              <table className="table table--compact table--neutral pcn-lines-table">
                <thead>
                  <tr>
                    <th>{t("purchases.creditNote.lines.description", "Concepto")}</th>
                    <th className="zh-text-align-right">
                      {t("purchases.creditNote.lines.subtotal", "Subtotal")}
                    </th>
                    <th className="zh-text-align-right">
                      {t("purchases.creditNote.lines.vatAmount", "IVA")}
                    </th>
                    <th className="zh-text-align-right">
                      {t("purchases.creditNote.lines.total", "Total crédito")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {editing.lines.map((line) => (
                    <tr key={line.id}>
                      <td>{line.description}</td>
                      <td className="zh-table-cell--num">
                        <ZHMoneyValue value={line.subtotal} currencySymbol="" />
                      </td>
                      <td className="zh-table-cell--num">
                        <ZHMoneyValue value={line.vatAmount} currencySymbol="" />
                      </td>
                      <td className="zh-table-cell--num">
                        <ZHMoneyValue value={line.totalAmount} currencySymbol="" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </ZHCard>
      )}

      {editing.receptionDocumentId && (
        <ZHCard title={t("purchases.creditNote.xmlDetail.title", "Detalle XML recibido")}>
          <span className="badge badge--neutral">
            {t("purchases.creditNote.xmlDetail.badge", "Referencia del proveedor")}
          </span>
          <p className="pcn-hint">
            {t(
              "purchases.creditNote.xmlDetail.help",
              "Este detalle proviene del XML/TXT del proveedor y se usa solo para revisión.",
            )}
          </p>
          {editing.receptionDocumentAccessKey && (
            <p className="pcn-hint">
              {t("purchases.creditNote.fiscal.accessKey", "Clave de acceso")}:{" "}
              {editing.receptionDocumentAccessKey}
            </p>
          )}
        </ZHCard>
      )}

      {(isDraft || isAuthorized) && (
        <ZHCard title={t("purchases.creditNote.actions.title", "Acciones")}>
          {!cancelling ? (
            <div className="pcn-actions-row">
              <ZHBtn type="button" variant="destructive" onClick={() => setCancelling(true)}>
                {t("purchases.creditNote.actions.cancel", "Cancelar nota de crédito")}
              </ZHBtn>
              {isDraft && isDiscountType && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  onClick={() => void handleAuthorize()}
                  disabled={
                    authorizing || (editing.lines.length === 0 && editing.taxSummaries.length === 0)
                  }
                >
                  {authorizing
                    ? t("common.saving", "Guardando...")
                    : t("purchases.creditNote.actions.authorize", "Autorizar nota de crédito")}
                </ZHBtn>
              )}
            </div>
          ) : (
            <form onSubmit={(e) => void cancelForm.handleSubmit(handleCancel)(e)}>
              <ZHField
                label={t("purchases.creditNote.reason.cancelLabel", "Motivo de la cancelación")}
                required
                fieldError={cancelForm.formState.errors.reason?.message}
              >
                <ZhTextarea className="zh-input" rows={3} maxLength={500} {...cancelForm.register("reason")} />
              </ZHField>
              <ZHFormActions
                onCancel={() => setCancelling(false)}
                onSave={() => void cancelForm.handleSubmit(handleCancel)()}
                hideDraft
                disableSave={cancelForm.formState.isSubmitting}
                labels={{
                  cancel: t("common.back", "Volver"),
                  save: cancelForm.formState.isSubmitting
                    ? t("common.saving", "Guardando...")
                    : t("common.confirm", "Confirmar"),
                }}
              />
            </form>
          )}
        </ZHCard>
      )}

      {isCancelled && editing.cancellationReason && (
        <ZHPageNotice
          variant="info"
          message={t("purchases.creditNote.status.cancelled", "Nota de crédito cancelada")}
          detail={editing.cancellationReason}
        />
      )}
    </PageShell>
  );
}
