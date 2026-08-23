import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
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
  purchaseService,
  type PurchaseInvoiceDto,
  type PurchaseInvoiceTaxSummaryDto,
} from "../api/purchaseService";
import {
  purchaseCreditNoteService,
  type PurchaseCreditNoteApplicationType,
  type PurchaseCreditNoteDto,
} from "../api/purchaseCreditNoteService";
import type { PurchaseReturnDto } from "../api/purchaseReturnService";
import { PurchaseCreditNoteDiscountLinesEditor } from "../components/PurchaseCreditNoteDiscountLinesEditor";
import { PurchaseCreditNoteTaxSummaryLinesEditor } from "../components/PurchaseCreditNoteTaxSummaryLinesEditor";
import { PurchaseInvoiceLinesDetailTable } from "../components/PurchaseInvoiceLinesDetailTable";
import { PurchaseReturnDraftFormSection } from "../components/PurchaseReturnDraftFormSection";
import {
  purchaseCreditNoteDraftSchema,
  emptyPurchaseCreditNoteDraftForm,
  type PurchaseCreditNoteDraftFormValues,
} from "../schemas/purchaseCreditNoteSchema";
import "../styles/purchase-credit-note.css";

type CreditNoteType = PurchaseCreditNoteApplicationType;

/** Espejo de ERP.Domain.Common.SriTaxCalculator — solo vista previa, el backend recalcula autoritativamente. */
function computeTaxPreview(taxableBase: number, vatRate: number, iceRate: number) {
  const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
  const ice = iceRate > 0 ? round2((taxableBase * iceRate) / 100) : 0;
  const vatBase = taxableBase + ice;
  const vat = vatRate > 0 ? round2((vatBase * vatRate) / 100) : 0;
  return { ice, vat };
}

/**
 * Pantalla centralizada de Nota de Crédito de Compra (FLOW-READY-02C-R1.2 sobre FLOW-READY-02C-R1.1).
 * `PurchaseCreditNote` es la cabecera fiscal única: el usuario llena/guarda los datos fiscales junto
 * con el tipo de aplicación (`ApplicationType`), y solo después se habilita el paso siguiente:
 * - Devolución: `PurchaseReturnDraftFormSection` (motor exclusivo de inventario/CxP/SupplierCredit,
 *   reutilizado sin cambios) — la cabecera fiscal sigue capturando líneas libres (sin cambios,
 *   FLOW-READY-02C-R1.2 §9 solo restringe el flujo Discount, no Return).
 * - Descuento/promoción: ya NO usa líneas libres como flujo principal — captura una línea por
 *   resumen fiscal real de la compra (`PurchaseCreditNoteTaxSummaryLinesEditor`), con base/impuesto
 *   heredados de `PurchaseInvoiceTaxSummary` (FLOW-READY-02D.1) y disponible ya descontando lo
 *   acreditado por otras NC. El backend recalcula IVA/ICE autoritativamente.
 * Siempre muestra "Detalle de compra afectada" (`PurchaseInvoiceLinesDetailTable`, líneas reales de
 * `invoice.lines`, solo lectura) antes de elegir el tipo — nunca datos inventados en frontend.
 *
 * Auditoría de reutilización (frontend/CLAUDE.md): se revisaron `PurchaseReturnableLinesEditor.tsx`
 * (patrón de filas fijas del servidor + `useFieldArray` upsert, adaptado en
 * `PurchaseCreditNoteTaxSummaryLinesEditor`, no reutilizable directamente por columnas/reglas
 * distintas) y `PurchaseCreditNoteDiscountLinesEditor` (conservado sin cambios, ahora exclusivo del
 * paso Return). `PurchaseReturnDraftFormSection`/`PurchaseReturnableLinesEditor` no se tocan.
 */
export function PurchaseCreditNoteFormPage() {
  const [searchParams] = useSearchParams();
  const invoiceId = searchParams.get("invoiceId") ?? "";
  const receptionDocumentId = searchParams.get("receptionDocumentId") ?? "";
  const navigate = useNavigate();
  const { t } = useI18n();

  const [invoice, setInvoice] = useState<PurchaseInvoiceDto | null>(null);
  const [taxSummaries, setTaxSummaries] = useState<PurchaseInvoiceTaxSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [creditNoteType, setCreditNoteType] = useState<CreditNoteType | null>(null);
  // Nota de crédito fiscal ya guardada — gate obligatorio antes de mostrar el paso de Devolución
  // (regla explícita: nunca redirigir/operar la devolución antes de guardar la NC fiscal).
  const [savedCreditNote, setSavedCreditNote] = useState<PurchaseCreditNoteDto | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    control,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseCreditNoteDraftFormValues>({
    resolver: zodResolver(purchaseCreditNoteDraftSchema),
    defaultValues: emptyPurchaseCreditNoteDraftForm(),
  });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  const {
    fields: taxSummaryFields,
    append: appendTaxSummaryLine,
    remove: removeTaxSummaryLine,
  } = useFieldArray({ control, name: "taxSummaryLines" });
  const lines = watch("lines");
  const taxSummaryLines = watch("taxSummaryLines");

  const isDiscount = creditNoteType === "Discount";
  const linesSubtotal = lines.reduce((sum, l) => sum + (Number(l.subtotal) || 0), 0);
  const linesVat = lines.reduce((sum, l) => sum + (Number(l.vatAmount) || 0), 0);

  const taxSummaryTotals = taxSummaryLines.reduce(
    (acc, l) => {
      const source = taxSummaries.find((s) => s.id === l.sourcePurchaseInvoiceTaxSummaryId);
      if (!source) return acc;
      const base = Number(l.taxableBase) || 0;
      const preview = computeTaxPreview(base, source.vatRate, source.iceRate);
      return {
        subtotal: acc.subtotal + base,
        ice: acc.ice + preview.ice,
        vat: acc.vat + preview.vat,
      };
    },
    { subtotal: 0, ice: 0, vat: 0 },
  );

  const subtotal = isDiscount ? taxSummaryTotals.subtotal : linesSubtotal;
  const iceAmount = isDiscount ? taxSummaryTotals.ice : 0;
  const vatAmount = isDiscount ? taxSummaryTotals.vat : linesVat;
  const totalAmount = subtotal + iceAmount + vatAmount;

  const anyTaxSummaryLineExceeds = taxSummaryLines.some((l) => {
    const source = taxSummaries.find((s) => s.id === l.sourcePurchaseInvoiceTaxSummaryId);
    return source ? Number(l.taxableBase) > source.availableTaxableBase : false;
  });

  useEffect(() => {
    if (!invoiceId) {
      setLoadError(t("purchases.creditNote.errors.missingInvoice", "Falta la factura afectada."));
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    Promise.all([purchaseService.getById(invoiceId), purchaseService.getTaxSummaries(invoiceId)])
      .then(([inv, summaries]) => {
        if (cancelled) return;
        setInvoice(inv);
        setTaxSummaries(summaries);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setLoadError(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.loadInvoice",
              "No se pudo cargar la factura afectada.",
            ),
          }),
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoiceId]);

  // FLOW-READY-02C-R1.1: al crearse el PurchaseReturn (motor exclusivo de inventario/CxP/
  // SupplierCredit — nunca reimplementado aquí), se vincula la NC fiscal ya guardada de forma
  // puramente documental. Si el vínculo falla, la devolución ya quedó creada correctamente — se
  // informa el error de vínculo por separado y de todos modos se navega al detalle de la devolución.
  const handleReturnCreated = async (dto: PurchaseReturnDto) => {
    if (savedCreditNote) {
      try {
        await purchaseCreditNoteService.linkReturn(savedCreditNote.id, {
          purchaseReturnId: dto.id,
          clientRequestId: crypto.randomUUID(),
        });
      } catch (err: unknown) {
        message.error(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.linkReturnFailed",
              "La devolución se creó pero no se pudo vincular a la nota de crédito fiscal.",
            ),
          }),
        );
      }
    }
    message.success(
      t("purchases.creditNote.messages.returnCreated", "Borrador de devolución creado correctamente."),
    );
    navigate(`/purchases/returns/${dto.id}`, { replace: true });
  };

  const onSubmitFiscal = async (values: PurchaseCreditNoteDraftFormValues) => {
    if (!invoice || !creditNoteType) return;
    try {
      const dto = await purchaseCreditNoteService.createDraft({
        clientRequestId: crypto.randomUUID(),
        purchaseInvoiceId: invoice.id,
        receptionDocumentId: receptionDocumentId || null,
        applicationType: creditNoteType,
        creditNoteNumber: values.creditNoteNumber,
        accessKey: values.accessKey?.trim() || null,
        authorizationNumber: values.authorizationNumber?.trim() || null,
        authorizationDate: values.authorizationDate?.trim() || null,
        issueDate: values.issueDate,
        reason: values.reason,
        lines:
          creditNoteType === "Return"
            ? values.lines.map((l) => ({
                description: l.description,
                subtotal: l.subtotal,
                vatCode: l.vatCode ?? null,
                vatRate: l.vatRate ?? null,
                vatAmount: l.vatAmount,
              }))
            : [],
        taxSummaryLines:
          creditNoteType === "Discount"
            ? values.taxSummaryLines.map((l) => ({
                sourcePurchaseInvoiceTaxSummaryId: l.sourcePurchaseInvoiceTaxSummaryId,
                taxableBase: l.taxableBase,
              }))
            : [],
      });

      if (creditNoteType === "Discount") {
        message.success(
          t("purchases.creditNote.messages.created", "Borrador de nota de crédito creado correctamente."),
        );
        navigate(`/purchases/credit-notes/${dto.id}`, { replace: true });
        return;
      }

      // Return: la NC fiscal queda guardada — habilita el paso de devolución en esta misma pantalla,
      // nunca redirige antes de este punto.
      setSavedCreditNote(dto);
      message.success(
        t(
          "purchases.creditNote.messages.fiscalSaved",
          "Nota de crédito fiscal guardada. Ahora registra la devolución de productos.",
        ),
      );
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: t(
              "purchases.creditNote.errors.createFailed",
              "No se pudo crear el borrador de la nota de crédito.",
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

  if (loadError || !invoice) {
    return (
      <PageShell title={t("purchases.creditNote.title", "Nota de crédito de compra")}>
        <ZHPageNotice
          variant="error"
          message={t("purchases.creditNote.errors.cannotStart", "No se pudo iniciar la nota de crédito")}
          detail={loadError}
        />
      </PageShell>
    );
  }

  const showFiscalForm = creditNoteType !== null && !(creditNoteType === "Return" && savedCreditNote);
  const showReturnStep = creditNoteType === "Return" && savedCreditNote !== null;
  const linesCount = creditNoteType === "Discount" ? taxSummaryFields.length : fields.length;

  return (
    <PageShell
      title={t("purchases.creditNote.title", "Nota de crédito de compra")}
      subtitle={`${invoice.invoiceNumber} — ${invoice.supplierName}`}
    >
      {/* Sección 1: Factura afectada */}
      <ZHCard title={t("purchases.creditNote.affectedInvoice.title", "Factura afectada")}>
        <div className="pcn-summary-grid">
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.supplier", "Proveedor")}
            </span>
            <span className="pcn-summary-grid__value">{invoice.supplierName}</span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.ruc", "RUC")}
            </span>
            <span className="pcn-summary-grid__value">{invoice.supplierTaxId}</span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.invoiceNumber", "N.º factura")}
            </span>
            <span className="pcn-summary-grid__value">{invoice.invoiceNumber}</span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.issueDate", "Fecha")}
            </span>
            <span className="pcn-summary-grid__value">{formatDate(invoice.issueDate)}</span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.total", "Total")}
            </span>
            <span className="pcn-summary-grid__value">
              <ZHMoneyValue value={invoice.grandTotal} currencySymbol="" />
            </span>
          </div>
          <div>
            <span className="pcn-summary-grid__label">
              {t("purchases.creditNote.affectedInvoice.balanceDue", "Saldo pendiente")}
            </span>
            <span className="pcn-summary-grid__value">—</span>
          </div>
        </div>
        <p className="pcn-hint">
          {t(
            "purchases.creditNote.affectedInvoice.balanceHint",
            "El saldo pendiente se valida en el servidor al crear/autorizar la nota de crédito.",
          )}
        </p>
      </ZHCard>

      {/* Sección 2: Detalle de compra afectada — siempre visible, líneas reales, solo lectura */}
      <ZHCard title={t("purchases.creditNote.invoiceLines.title", "Detalle de compra afectada")}>
        <PurchaseInvoiceLinesDetailTable lines={invoice.lines} />
      </ZHCard>

      {/* Sección 3: Tipo de nota de crédito — inmutable una vez guardada la NC fiscal */}
      <ZHCard
        title={
          <>
            {t("purchases.creditNote.type.title", "Tipo de nota de crédito")}
            <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_CREDIT_NOTE} />
          </>
        }
      >
        <div className="zh-radio-group">
          <label className="zh-radio-option">
            <input
              type="radio"
              name="creditNoteType"
              checked={creditNoteType === "Return"}
              disabled={savedCreditNote !== null}
              onChange={() => setCreditNoteType("Return")}
            />
            <span className="pcn-radio-label">
              {t("purchases.creditNote.type.return", "Devolución de productos")}
            </span>
          </label>
          <label className="zh-radio-option">
            <input
              type="radio"
              name="creditNoteType"
              checked={creditNoteType === "Discount"}
              disabled={savedCreditNote !== null}
              onChange={() => setCreditNoteType("Discount")}
            />
            <span className="pcn-radio-label">
              {t("purchases.creditNote.type.discount", "Descuento / promoción")}
            </span>
          </label>
        </div>
      </ZHCard>

      {/* Sección 4: Datos fiscales de la NC — común a ambos tipos, obligatoria antes de continuar */}
      {showFiscalForm && (
        <form onSubmit={(e) => void handleSubmit(onSubmitFiscal)(e)}>
          <ZHCard title={t("purchases.creditNote.fiscal.title", "Nota de crédito recibida / datos fiscales")}>
            <div className="pcn-fiscal-grid">
              <ZHField
                label={t("purchases.creditNote.fiscal.number", "Número NC")}
                required
                fieldError={errors.creditNoteNumber?.message}
              >
                <ZhTextInput className="zh-input" maxLength={17} {...register("creditNoteNumber")} />
              </ZHField>
              <ZHField
                label={t("purchases.creditNote.fiscal.accessKey", "Clave de acceso")}
                fieldError={errors.accessKey?.message}
              >
                <ZhTextInput className="zh-input" maxLength={49} {...register("accessKey")} />
              </ZHField>
              <ZHField
                label={t("purchases.creditNote.fiscal.authorizationNumber", "Autorización")}
                fieldError={errors.authorizationNumber?.message}
              >
                <ZhTextInput
                  className="zh-input"
                  maxLength={49}
                  {...register("authorizationNumber")}
                />
              </ZHField>
              <ZHField
                label={t("purchases.creditNote.fiscal.issueDate", "Fecha emisión")}
                required
                fieldError={errors.issueDate?.message}
              >
                <ZhDateInput {...register("issueDate")} />
              </ZHField>
            </div>
          </ZHCard>

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
              />
            </ZHField>
          </ZHCard>

          {creditNoteType === "Discount" ? (
            <ZHCard
              title={t(
                "purchases.creditNote.taxSummaryLines.title",
                "Descuento por resumen fiscal de compra",
              )}
            >
              <PurchaseCreditNoteTaxSummaryLinesEditor
                taxSummaries={taxSummaries}
                selected={taxSummaryFields}
                append={appendTaxSummaryLine}
                remove={removeTaxSummaryLine}
                disabled={isSubmitting}
              />
            </ZHCard>
          ) : (
            <ZHCard
              title={t("purchases.creditNote.lines.titleReturn", "Valores declarados de la nota de crédito")}
            >
              <PurchaseCreditNoteDiscountLinesEditor
                register={register}
                fields={fields}
                append={append}
                remove={remove}
                disabled={isSubmitting}
              />
              {errors.lines && !Array.isArray(errors.lines) ? (
                <ZHPageNotice variant="error" message={errors.lines.message ?? ""} />
              ) : null}
            </ZHCard>
          )}

          <ZHCard title={t("purchases.creditNote.summary.title", "Resumen de afectación")}>
            <div className="pcn-totals">
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.lines.subtotal", "Subtotal")}
                </span>
                <span className="pcn-summary-grid__value">
                  <ZHMoneyValue value={subtotal} currencySymbol="" />
                </span>
              </div>
              {isDiscount && (
                <div>
                  <span className="pcn-summary-grid__label">
                    {t("purchases.creditNote.taxSummaryLines.iceCredit", "ICE crédito")}
                  </span>
                  <span className="pcn-summary-grid__value">
                    <ZHMoneyValue value={iceAmount} currencySymbol="" />
                  </span>
                </div>
              )}
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.lines.vatAmount", "IVA")}
                </span>
                <span className="pcn-summary-grid__value">
                  <ZHMoneyValue value={vatAmount} currencySymbol="" />
                </span>
              </div>
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.lines.total", "Total crédito")}
                </span>
                <span className="pcn-summary-grid__value">
                  <ZHMoneyValue value={totalAmount} currencySymbol="" />
                </span>
              </div>
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.summary.reducesPayable", "Reduce CxP")}
                </span>
                <span className="pcn-summary-grid__value">{t("common.yes", "Sí")}</span>
              </div>
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.summary.movesInventory", "Mueve inventario")}
                </span>
                <span className="pcn-summary-grid__value">
                  {creditNoteType === "Return" ? t("common.yes", "Sí") : t("common.no", "No")}
                </span>
              </div>
              <div>
                <span className="pcn-summary-grid__label">
                  {t("purchases.creditNote.summary.accounting", "Contabilidad")}
                </span>
                <span className="pcn-summary-grid__value">
                  {creditNoteType === "Return"
                    ? t(
                        "purchases.creditNote.summary.accountingGeneratedByReturn",
                        "Se genera automáticamente (flujo de devolución existente)",
                      )
                    : t("purchases.creditNote.summary.noAccountingYet", "No se genera en esta fase")}
                </span>
              </div>
            </div>
          </ZHCard>

          {receptionDocumentId && (
            <ZHCard
              title={t("purchases.creditNote.xmlDetail.title", "Detalle XML recibido")}
            >
              <span className="badge badge--neutral">
                {t("purchases.creditNote.xmlDetail.badge", "Referencia del proveedor")}
              </span>
              <p className="pcn-hint">
                {t(
                  "purchases.creditNote.xmlDetail.help",
                  "Este detalle proviene del XML/TXT del proveedor y se usa solo para revisión.",
                )}
              </p>
              <p className="pcn-hint">
                {t("purchases.creditNote.xmlDetail.documentId", "Documento de recepción")}:{" "}
                {receptionDocumentId}
              </p>
            </ZHCard>
          )}

          <ZHFormActions
            onCancel={() => navigate("/purchases")}
            onSave={() => void handleSubmit(onSubmitFiscal)()}
            hideDraft
            disableSave={isSubmitting || linesCount === 0 || (isDiscount && anyTaxSummaryLineExceeds)}
            labels={{
              cancel: t("common.back", "Volver"),
              save: isSubmitting
                ? t("common.saving", "Guardando...")
                : creditNoteType === "Discount"
                  ? t("purchases.creditNote.actions.create", "Crear nota de crédito")
                  : t("purchases.creditNote.actions.saveFiscal", "Guardar nota de crédito fiscal"),
            }}
          />
        </form>
      )}

      {/* Sección 5: Devolución de productos — solo tras guardar la NC fiscal (Return) */}
      {showReturnStep && (
        <>
          <ZHCard>
            <ZHPageNotice
              variant="info"
              message={t(
                "purchases.creditNote.type.returnExplanation",
                "Nota de crédito fiscal guardada. Registra ahora la devolución de productos que aplica esta nota de crédito.",
              )}
            />
          </ZHCard>

          <PurchaseReturnDraftFormSection invoice={invoice} onCreated={handleReturnCreated} />
        </>
      )}
    </PageShell>
  );
}
