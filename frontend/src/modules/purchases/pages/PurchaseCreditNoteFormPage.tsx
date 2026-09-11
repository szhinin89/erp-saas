import { purchaseReceptionService } from "../api/purchaseReceptionService";
import { useEffect, useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
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
  type PurchaseListItemDto,
} from "../api/purchaseService";
import {
  purchaseCreditNoteService,
  type PurchaseCreditNoteApplicationType,
} from "../api/purchaseCreditNoteService";
import { purchaseReturnService, type ReturnableLineDto } from "../api/purchaseReturnService";
import { purchaseReturnPreview } from "../utils/purchaseReturnPreview";
import { PurchaseReturnableLinesEditor } from "../components/PurchaseReturnableLinesEditor";
import { PurchaseCreditNoteTaxSummaryLinesEditor } from "../components/PurchaseCreditNoteTaxSummaryLinesEditor";
import { PurchaseInvoiceLinesDetailTable } from "../components/PurchaseInvoiceLinesDetailTable";
import { SupplierPicker } from "../components/SupplierPicker";
import { PurchaseInvoicePicker } from "../components/PurchaseInvoicePicker";
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
 * PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — una sola pantalla de ingreso para dos
 * orígenes: (1) Recepción XML/SRI → "Procesar NC" abre esta misma ruta con `?invoiceId=` (y
 * `?receptionDocumentId=` para precargar datos fiscales del XML); (2) menú "Notas de Crédito de
 * Compra → Nueva" abre la MISMA ruta sin parámetros — modo manual: el usuario elige proveedor y
 * factura `Confirmed` primero (`SupplierPicker` + `PurchaseInvoicePicker`, reutilizados/extendidos,
 * ningún picker paralelo) y a partir de ahí el resto del formulario, la validación de cantidades y
 * el guardado son EXACTAMENTE el mismo código que el modo XML (mismo estado, mismo efecto de carga,
 * mismo `onSubmitFiscal`) — nunca un formulario ni un motor duplicado. El modo se decide una sola
 * vez al montar (según si `invoiceId` llegó por URL); cuando no llegó, `manualInvoiceId` alimenta la
 * misma variable `invoiceId` que dispara el effect de carga ya existente.
 */
export function PurchaseCreditNoteFormPage() {
  const [searchParams] = useSearchParams();
  const invoiceIdParam = searchParams.get("invoiceId") ?? "";
  const receptionDocumentId = searchParams.get("receptionDocumentId") ?? "";
  const navigate = useNavigate();
  const { t } = useI18n();

  // searchParams no cambia durante la selección manual (nunca se llama setSearchParams aquí).
  const isManualMode = !invoiceIdParam;
  const [manualSupplierId, setManualSupplierId] = useState<string | null>(null);
  const [manualInvoice, setManualInvoice] = useState<PurchaseListItemDto | null>(null);
  const invoiceId = invoiceIdParam || manualInvoice?.id || "";

  const [invoice, setInvoice] = useState<PurchaseInvoiceDto | null>(null);
  const [taxSummaries, setTaxSummaries] = useState<PurchaseInvoiceTaxSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [creditNoteType, setCreditNoteType] = useState<CreditNoteType | null>(null);
  const [returnableLines, setReturnableLines] = useState<ReturnableLineDto[]>([]);
  const [returnLoadError, setReturnLoadError] = useState("");
  const [receivedTotal, setReceivedTotal] = useState<number | null>(null);
  const [clientRequestId] = useState(() => crypto.randomUUID());

  const {
    register,
    setValue,
    handleSubmit,
    setError,
    control,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseCreditNoteDraftFormValues>({
    resolver: zodResolver(purchaseCreditNoteDraftSchema),
    defaultValues: emptyPurchaseCreditNoteDraftForm(),
  });
  const { fields: returnFields, append: appendReturn, remove: removeReturn } = useFieldArray({ control, name: "returnLines" });
  const {
    fields: taxSummaryFields,
    append: appendTaxSummaryLine,
    remove: removeTaxSummaryLine,
  } = useFieldArray({ control, name: "taxSummaryLines" });
  const returnLines = watch("returnLines");
  const taxSummaryLines = watch("taxSummaryLines");

  const isDiscount = creditNoteType === "Discount";
  const returnTotals = returnLines.reduce((totals, line) => {
    const preview = purchaseReturnPreview(invoice?.lines.find((l) => l.id === line.originalInvoiceDetailId), Number(line.quantity));
    return { base: totals.base + preview.base, vat: totals.vat + preview.vat,
      ice: totals.ice + preview.ice, irbpnr: totals.irbpnr + preview.irbpnr, total: totals.total + preview.total };
  }, { base: 0, vat: 0, ice: 0, irbpnr: 0, total: 0 });
  const invalidReturn = returnLines.some((line) => {
    const source = returnableLines.find((l) => l.invoiceDetailId === line.originalInvoiceDetailId);
    return !source || line.quantity <= 0 || line.quantity > source.remainingQuantity;
  });

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

  const subtotal = isDiscount ? taxSummaryTotals.subtotal : returnTotals.base;
  const iceAmount = isDiscount ? taxSummaryTotals.ice : returnTotals.ice;
  const vatAmount = isDiscount ? taxSummaryTotals.vat : returnTotals.vat;
  const totalAmount = subtotal + iceAmount + vatAmount + (isDiscount ? 0 : returnTotals.irbpnr);
  const xmlMismatch = !isDiscount && receivedTotal !== null && Math.abs(totalAmount - receivedTotal) > 0.0100001;

  const anyTaxSummaryLineExceeds = taxSummaryLines.some((l) => {
    const source = taxSummaries.find((s) => s.id === l.sourcePurchaseInvoiceTaxSummaryId);
    return source ? Number(l.taxableBase) > source.availableTaxableBase : false;
  });

  useEffect(() => {
    if (!invoiceId) {
      // Modo manual: aún no se eligió proveedor/factura — no es un error, se muestra el
      // selector (ver el render de abajo); el modo XML/`invoiceId` por URL sigue exigiéndolo.
      if (!isManualMode) {
        setLoadError(t("purchases.creditNote.errors.missingInvoice", "Falta la factura afectada."));
      }
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    Promise.all([purchaseService.getById(invoiceId), purchaseService.getTaxSummaries(invoiceId), purchaseReturnService.getReturnableLines(invoiceId).catch(() => {
      if (!cancelled) setReturnLoadError("No se pudieron cargar las cantidades disponibles para devolución.");
      return [];
    }),
      receptionDocumentId ? purchaseReceptionService.getXmlView(receptionDocumentId) : Promise.resolve(null)])
      .then(([inv, summaries, returnable, reception]) => {
        if (cancelled) return;
        if (reception) {
          if (reception.documentType !== "CREDIT_NOTE") throw new Error("El documento recibido no es una nota de crédito.");
          setReceivedTotal(reception.totalAmount);
          setValue("accessKey", reception.accessKey);
          setValue("creditNoteNumber", reception.documentNumber);
          setValue("issueDate", reception.issueDate);
          setValue("authorizationNumber", reception.authorizationNumber ?? "");
          setValue("authorizationDate", reception.authorizationDate ?? "");
          setValue("reason", reception.modificationReason ?? "");
        }
        setReturnableLines(returnable);
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
  }, [invoiceId, receptionDocumentId, setValue]);

  const onSubmitFiscal = async (values: PurchaseCreditNoteDraftFormValues) => {
    if (!invoice || !creditNoteType) return;
    if (creditNoteType === "Return" && (invalidReturn || xmlMismatch || returnLines.length === 0)) return;
    try {
      const dto = await purchaseCreditNoteService.createDraft({
        clientRequestId,
        purchaseInvoiceId: invoice.id,
        receptionDocumentId: receptionDocumentId || null,
        applicationType: creditNoteType,
        creditNoteNumber: values.creditNoteNumber,
        accessKey: values.accessKey?.trim() || null,
        authorizationNumber: values.authorizationNumber?.trim() || null,
        authorizationDate: values.authorizationDate?.trim() || null,
        issueDate: values.issueDate,
        reason: values.reason,
        lines: [],
        returnLines: creditNoteType === "Return" ? values.returnLines : [],
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

      message.success("Nota de crédito y borrador de devolución creados.");
      navigate(`/purchases/returns/${dto.linkedPurchaseReturnId}`, { replace: true });
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

  if (loadError) {
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

  // PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — modo manual sin factura elegida todavía:
  // mismo `PageShell`/`ZHCard` de siempre, selector de proveedor + factura Confirmed en vez del
  // error "Falta la factura afectada" del modo XML. Al elegir factura, `invoiceId` deja de estar
  // vacío y el effect de arriba (idéntico al de siempre) carga todo lo demás.
  if (!invoice) {
    return (
      <PageShell
        title={t("purchases.creditNote.title", "Nota de crédito de compra")}
        subtitle={t(
          "purchases.creditNote.manual.subtitle",
          "Seleccione el proveedor y la factura de compra afectada",
        )}
        action={
          // PURCHASE-CREDIT-NOTE-FULL-UX-FLOW-01 — esta pantalla pertenece al módulo Notas de
          // Crédito de Compra: "Volver" regresa a su listado, nunca a Facturas de compra.
          <ZHBtn
            type="button"
            variant="ghost"
            onClick={() => navigate("/purchases/credit-notes")}
          >
            {t("common.back", "Volver")}
          </ZHBtn>
        }
      >
        <ZHCard title={t("purchases.creditNote.affectedInvoice.title", "Factura afectada")}>
          <ZHField
            label={t("purchases.creditNote.affectedInvoice.supplier", "Proveedor")}
            required
          >
            <SupplierPicker
              value={manualSupplierId}
              onChange={(supplier) => {
                setManualSupplierId(supplier?.id ?? null);
                setManualInvoice(null);
              }}
            />
          </ZHField>
          {manualSupplierId && (
            <ZHField
              label={t(
                "purchases.creditNote.affectedInvoice.invoiceNumber",
                "Factura de compra confirmada",
              )}
              required
            >
              <PurchaseInvoicePicker
                supplierId={manualSupplierId}
                value={manualInvoice}
                onChange={setManualInvoice}
              />
            </ZHField>
          )}
        </ZHCard>
      </PageShell>
    );
  }

  const showFiscalForm = creditNoteType !== null;
  const linesCount = creditNoteType === "Discount" ? taxSummaryFields.length : returnFields.length;

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
              disabled={isSubmitting}
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
              disabled={isSubmitting}
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
                <ZhTextInput className="zh-input" maxLength={49} readOnly={!!receptionDocumentId} {...register("accessKey")} />
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
            <ZHCard title="Productos a devolver">
              {returnLoadError && <ZHPageNotice variant="error" message={returnLoadError} />}
              <PurchaseReturnableLinesEditor returnableLines={returnableLines} invoiceLines={invoice.lines}
                selected={returnFields} append={appendReturn} remove={removeReturn} disabled={isSubmitting} />
              {xmlMismatch && <ZHPageNotice variant="error" message={`El total a devolver debe coincidir con la NC/XML recibido (${receivedTotal?.toFixed(2)}).`} />}
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
              {(isDiscount || iceAmount > 0) && (
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
              {!isDiscount && returnTotals.irbpnr > 0 && <div>
                <span className="pcn-summary-grid__label">IRBPNR</span>
                <ZHMoneyValue value={returnTotals.irbpnr} currencySymbol="" />
              </div>}
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
            // PURCHASE-CREDIT-NOTE-FULL-UX-FLOW-01 — "Volver" desde /purchases/credit-notes/new
            // (XML o manual) regresa al listado de NC, nunca a Facturas de compra.
            onCancel={() => navigate("/purchases/credit-notes")}
            onSave={() => void handleSubmit(onSubmitFiscal)()}
            hideDraft
            disableSave={isSubmitting || linesCount === 0 || (isDiscount ? anyTaxSummaryLineExceeds : invalidReturn || xmlMismatch)}
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

    </PageShell>
  );
}
