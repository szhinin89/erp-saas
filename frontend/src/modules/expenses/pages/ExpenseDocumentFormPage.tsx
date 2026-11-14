import { formatDecimalDisplay } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { purchaseReceptionExpenseFacade, type ExpenseReceptionDraft } from "../../purchases/facades/purchaseReceptionExpenseFacade";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormAlert, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { todayIso, toDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import {
  formatApiRequestError,
  readApiErrorCode,
  parseValidationErrors,
} from "../../lib/apiError";
import {
  sriLookupFacade,
  type SriDocTypeLookup,
  type SriTaxSupportLookup,
  type SriVatRateLookup,
} from "../../items/facades/sriLookupFacade";
import { paymentTermLookupFacade, type PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";
import type { SupplierPickerRow } from "../../masterData/facades/supplierPickerFacade";
import {
  expenseCategoryService,
  type ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";
import {
  expenseDocumentService,
  type ExpenseDocumentDetailDto,
  type RetentionEligibilityResult,
} from "../api/expenseDocumentService";
import {
  ExpenseDocumentHeader,
  type ExpenseDocumentHeaderErrors,
  type ExpenseDocumentHeaderState,
} from "../components/ExpenseDocumentHeader";
import {
  type ExpenseDraftLineState,
  type ExpenseLineFieldErrors,
} from "../components/ExpenseDocumentLinesEditor";
import { ExpenseDocumentLinesEditor } from "../components/ExpenseDocumentLinesEditor";
import { ExpenseDocumentStatusBadge } from "../components/ExpenseDocumentStatusBadge";
import { ExpenseDocumentTotals } from "../components/ExpenseDocumentTotals";
import { ExpenseRetentionSection } from "../components/ExpenseRetentionSection";
import { SRI_ANNULMENT_REQUIRED_CODE } from "../../retentions/facades/retentionDocumentFacade";
import {
  hasMappedFieldErrors,
  mapExpenseBackendErrors,
} from "../utils/expenseBackendErrors";
import {
  buildExpenseAccountsById,
  buildExpenseDraftPayload,
  calculateExpenseDocumentTotals,
  documentToHeader,
  documentToLines,
  documentToSupplier,
  findVatCodeForRate,
  flattenExpenseSubcategories,
  hasConfiguredExpenseSubcategory,
  newExpenseDraftLine,
  parseExpenseNumber,
  type ExpenseDraftLineScales,
  type VatRateByCode,
} from "../utils/expenseDocumentDraftModel";
import {
  buildRetentionIntentRequest,
  emptyRetentionIntentState,
  isRetentionIntentBlockedByEligibility,
  isRetentionIntentComplete,
  type RetentionIntentFormState,
} from "../utils/expenseRetentionModel";
import "../styles/expense-documents.css";

const PERMISSIONS = {
  view: "expenses.documents.view",
  create: "expenses.documents.create",
  update: "expenses.documents.update",
  confirm: "expenses.documents.confirm",
  cancel: "expenses.documents.cancel",
  catalogView: "expenses.catalog.view",
} as const;

const EMPTY_HEADER: ExpenseDocumentHeaderState = {
  supplierId: "",
  issueDate: todayIso(),
  accountingDate: todayIso(),
  documentType: "01",
  documentNumber: "",
  paymentTermId: "",
  dueDate: "",
  authorizationNumber: "",
  authorizationDate: "",
  notes: "",
  taxSupportCode: "",
};

export function ExpenseDocumentFormPage() {
  const { t } = useI18n();
  // `load` no debe depender de `t`: cambiar de idioma no puede recargar el documento (perdería la
  // edición en curso). Lee siempre el traductor vigente por referencia.
  const tRef = useRef(t);
  useEffect(() => {
    tRef.current = t;
  }, [t]);
  const { id } = useParams();
  const isNew = !id;
  const [searchParams] = useSearchParams();
  const fromReceptionId = !id ? searchParams.get("fromReceptionId") : null;
  const [reception, setReception] = useState<ExpenseReceptionDraft | null>(null);
  const navigate = useNavigate();
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const canCreate = has(PERMISSIONS.create);
  const canUpdate = has(PERMISSIONS.update);
  const canConfirm = has(PERMISSIONS.confirm);
  const canCancel = has(PERMISSIONS.cancel);
  const canReadCatalog = has(PERMISSIONS.catalogView);

  const [header, setHeader] = useState<ExpenseDocumentHeaderState>(EMPTY_HEADER);
  const [supplier, setSupplier] = useState<SupplierPickerRow | null>(null);
  // Escalas del modelo textual del borrador (04G): mismas semánticas que los inputs de la línea.
  const moneyDecimals = usePrecisionDecimals("money");
  const unitPriceDecimals = usePrecisionDecimals("purchaseUnitPrice");
  const draftScales = useMemo<ExpenseDraftLineScales>(
    () => ({ unitPriceDecimals, moneyDecimals }),
    [unitPriceDecimals, moneyDecimals],
  );
  // `load` lee las escalas vigentes sin depender de ellas: un cambio de policy no debe recargar
  // el documento (perdería la edición en curso).
  const draftScalesRef = useRef(draftScales);
  useEffect(() => {
    draftScalesRef.current = draftScales;
  }, [draftScales]);
  const [lines, setLines] = useState<ExpenseDraftLineState[]>(() => [newExpenseDraftLine(draftScales)]);
  const [tree, setTree] = useState<ExpenseCategoryTreeNodeDto[]>([]);
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermDto[]>([]);
  const [sriDocTypes, setSriDocTypes] = useState<SriDocTypeLookup[]>([]);
  const [sriTaxSupports, setSriTaxSupports] = useState<SriTaxSupportLookup[]>([]);
  const [vatRates, setVatRates] = useState<SriVatRateLookup[]>([]);
  const [document, setDocument] = useState<ExpenseDocumentDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [headerErrors, setHeaderErrors] = useState<ExpenseDocumentHeaderErrors>({});
  const [lineErrors, setLineErrors] = useState<ExpenseLineFieldErrors>({});
  const [confirmModalOpen, setConfirmModalOpen] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelReasonError, setCancelReasonError] = useState<string | undefined>();
  // ZH-RETENTION-SRI-ANNULMENT-01 — retención ya AUTORIZADA: el motivo se conserva para iniciar la
  // anulación ante el SRI sin pedirlo de nuevo.
  const [sriAnnulmentReason, setSriAnnulmentReason] = useState<string | null>(null);
  const [retention, setRetention] = useState<RetentionIntentFormState>(
    emptyRetentionIntentState(),
  );
  const [retentionEligibility, setRetentionEligibility] =
    useState<RetentionEligibilityResult | null>(null);
  const [retentionRefreshKey, setRetentionRefreshKey] = useState(0);

  // EXPENSE-BACKEND-01: la cuenta destino de cada subcategoría viene en el propio árbol de
  // categorías (expenses.catalog.view); el formulario ya no consulta el plan de cuentas.
  const accountsById = useMemo(() => buildExpenseAccountsById(tree), [tree]);
  const vatRateByCode: VatRateByCode = useMemo(
    () => new Map(vatRates.map((rate) => [rate.code, rate.percentage])),
    [vatRates],
  );
  const totals = useMemo(
    () => calculateExpenseDocumentTotals(lines, vatRateByCode),
    [lines, vatRateByCode],
  );
  // Presentación de textos compuestos (04E): semántica declarada, mismo resolver y motor.
  const taxDecimals = usePrecisionDecimals("tax");
  const receptionMismatch = useMemo(() => {
    if (!reception) return null;
    const tolerance = 0.01;
    const diff = Number((totals.grandTotal - reception.total).toFixed(2));
    if (Math.abs(diff) <= tolerance) return null;
    return { calculatedTotal: totals.grandTotal, receivedTotal: reception.total, diff };
  }, [reception, totals.grandTotal]);
  const canSave = isNew ? canCreate : canUpdate;
  const isDraft = document?.status ? document.status === "Draft" : true;
  const disabled = loading || saving || !isDraft || !canSave || (!!fromReceptionId && !reception);
  const canShowConfirmButton = !isNew && canConfirm && document?.status === "Draft";
  const canShowCancelButton = !isNew && canCancel && document?.status === "Confirmed";
  const catalogReady =
    canReadCatalog && hasConfiguredExpenseSubcategory(tree, accountsById);

  const load = useCallback(async () => {
    setLoading(true);
    setPageError(null);
    setReception(null);
    try {
      const requests = [
        canReadCatalog ? expenseCategoryService.getTree(false) : Promise.resolve([]),
        paymentTermLookupFacade.list(),
        sriLookupFacade.docTypes(),
        sriLookupFacade.taxSupportCodes(),
        sriLookupFacade.vatRates(),
        id ? expenseDocumentService.getById(id) : Promise.resolve(null),
      ] as const;
      const [
        categoryTree,
        paymentTermRows,
        sriDocTypeRows,
        sriTaxSupportRows,
        vatRateRows,
        expenseDocument,
      ] = await Promise.all(requests);

      setTree(categoryTree);
      setPaymentTerms(paymentTermRows);
      setSriDocTypes(sriDocTypeRows);
      setSriTaxSupports(sriTaxSupportRows);
      setVatRates(vatRateRows);
      if (expenseDocument) {
        setDocument(expenseDocument);
        setHeader(documentToHeader(expenseDocument, toDateTimeLocalInputValue));
        setSupplier(documentToSupplier(expenseDocument));
        setLines(documentToLines(expenseDocument, draftScalesRef.current));
      } else {
        setDocument(null);
        const source = fromReceptionId
          ? await purchaseReceptionExpenseFacade.prepareExpenseDraft(fromReceptionId) : null;
        setReception(source);
        setHeader(source ? {
          ...EMPTY_HEADER,
          supplierId: source.supplierId,
          issueDate: source.issueDate,
          documentType: source.documentType,
          documentNumber: source.documentNumber,
          authorizationNumber: source.authorizationNumber ?? "",
          authorizationDate: toDateTimeLocalInputValue(source.authorizationDate),
        } : EMPTY_HEADER);
        setSupplier(source ? {
          id: source.supplierId, fullName: source.supplierName,
          identificationNumber: source.supplierTaxId, isActive: true,
          hasSupplierRole: true, supplierConfig: null,
        } : null);
        const initialLine = newExpenseDraftLine(draftScalesRef.current);
        if (source && source.subtotal > 0) {
          const impliedRate = (source.vatAmount / source.subtotal) * 100;
          const matchedCode = findVatCodeForRate(vatRateRows, impliedRate);
          if (matchedCode) initialLine.vatCode = matchedCode;
        }
        setLines([initialLine]);
      }
      setRetention(emptyRetentionIntentState());
      setRetentionRefreshKey((key) => key + 1);
    } catch (error) {
      setPageError(
        formatApiRequestError(error, {
          generic: tRef.current("expenses.form.loadError"),
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [canReadCatalog, id, fromReceptionId]);

  useEffect(() => {
    if (canView || isNew) void load();
  }, [canView, isNew, load]);

  if (!canView && !isNew) return <NoAccessPage title={t("expenses.form.noAccess.edit")} />;
  if (isNew && !canCreate) return <NoAccessPage title={t("expenses.form.title.new")} />;

  const validate = (): boolean => {
    const nextHeader: ExpenseDocumentHeaderErrors = {};
    const nextLines: ExpenseLineFieldErrors = {};
    const subcategories = new Map(
      flattenExpenseSubcategories(tree).map((node) => [node.id, node]),
    );

    if (!header.supplierId) nextHeader.supplierId = t("expenses.form.validation.supplier");
    if (!header.issueDate) nextHeader.issueDate = t("expenses.form.validation.issueDate");
    if (!header.accountingDate)
      nextHeader.accountingDate = t("expenses.form.validation.accountingDate");
    if (!header.documentType.trim())
      nextHeader.documentType = t("expenses.form.validation.documentType");
    if (!header.documentNumber.trim())
      nextHeader.documentNumber = t("expenses.form.validation.documentNumber");
    if (header.dueDate && header.issueDate && header.dueDate < header.issueDate)
      nextHeader.dueDate = t("expenses.form.validation.dueDate");

    lines.forEach((line) => {
      const errors: Partial<Record<keyof ExpenseDraftLineState, string>> = {};
      const subcategory = subcategories.get(line.expenseSubcategoryId);
      const subtotal =
        parseExpenseNumber(line.quantity) * parseExpenseNumber(line.unitPrice);
      if (!line.expenseSubcategoryId) {
        errors.expenseSubcategoryId = t("expenses.form.validation.line.subcategory");
      } else if (
        !subcategory?.isActive ||
        !subcategory.accountingAccountId ||
        !accountsById.has(subcategory.accountingAccountId)
      ) {
        errors.expenseSubcategoryId = t("expenses.form.validation.line.subcategoryInvalid");
      }
      if (parseExpenseNumber(line.quantity) <= 0)
        errors.quantity = t("expenses.form.validation.line.quantity");
      if (parseExpenseNumber(line.unitPrice) < 0)
        errors.unitPrice = t("expenses.form.validation.line.unitPrice");
      if (parseExpenseNumber(line.discountValue) < 0)
        errors.discountValue = t("expenses.form.validation.line.discountNegative");
      if (parseExpenseNumber(line.discountValue) > subtotal)
        errors.discountValue = t("expenses.form.validation.line.discountExceeds");
      if (!line.vatCode.trim()) errors.vatCode = t("expenses.form.validation.line.vatCode");
      if (Object.keys(errors).length > 0) nextLines[line.key] = errors;
    });

    if (lines.length === 0) {
      const line = newExpenseDraftLine(draftScales);
      nextLines[line.key] = { expenseSubcategoryId: t("expenses.form.validation.noLines") };
      setLines([line]);
    }

    setHeaderErrors(nextHeader);
    setLineErrors(nextLines);
    return (
      Object.keys(nextHeader).length === 0 &&
      Object.keys(nextLines).length === 0 &&
      !receptionMismatch
    );
  };

  const handleSave = async () => {
    if (!validate()) {
      if (receptionMismatch) {
        message.error(t("expenses.form.message.mismatchBlocked"));
      } else {
        message.error(t("expenses.form.message.reviewRequired"));
      }
      return;
    }

    setSaving(true);
    setHeaderErrors({});
    setLineErrors({});
    try {
      const payload = {
        ...buildExpenseDraftPayload(header, lines),
        ...(reception ? {
          receptionDocumentId: reception.receptionDocumentId,
          accessKey: reception.accessKey,
        } : {}),
      };
      const saved = isNew
        ? await expenseDocumentService.create(payload)
        : await expenseDocumentService.update(id!, payload);
      message.success(t("expenses.form.message.saved"));
      if (isNew) {
        navigate(`/expenses/documents/${saved.id}`, { replace: true });
      } else {
        setDocument(saved);
        setHeader(documentToHeader(saved, toDateTimeLocalInputValue));
        setSupplier(documentToSupplier(saved));
        setLines(documentToLines(saved, draftScales));
      }
    } catch (error) {
      // EXPENSE-UX-01 (B2): los errores de campo/línea del 422 se muestran donde corresponden; lo que
      // no se puede asociar (errores de negocio del handler, sin línea) sale como mensaje general.
      const validation = parseValidationErrors(error);
      const mapped = validation
        ? mapExpenseBackendErrors(
            validation,
            lines.map((line) => line.key),
            (number, text) => t("expenses.form.message.linePrefix", { number, message: text }),
          )
        : null;
      if (mapped) {
        setHeaderErrors(mapped.header);
        setLineErrors(mapped.lines);
      }
      if (mapped && mapped.unmapped.length > 0) {
        message.error(mapped.unmapped[0]);
      } else if (mapped && hasMappedFieldErrors(mapped)) {
        message.error(t("expenses.form.message.reviewMarkedFields"));
      } else {
        message.error(
          formatApiRequestError(error, {
            generic: t("expenses.form.message.saveError"),
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const handleConfirm = async () => {
    if (isNew || !id) return;

    // RETENTIONS-UI-EXPENSES-01F — la UI bloquea ANTES de llamar al backend cuando ya sabe,
    // con la última elegibilidad conocida, que la retención marcada no puede generarse. El
    // backend sigue revalidando siempre (fail-closed real), esto solo evita una llamada que ya
    // se sabe que va a fallar.
    if (isRetentionIntentBlockedByEligibility(retention, retentionEligibility)) {
      message.error(t("expenses.form.message.retentionBlocked"));
      return;
    }
    if (retention.appliesRetention && !isRetentionIntentComplete(retention)) {
      message.error(t("expenses.form.message.retentionIncomplete"));
      return;
    }

    setConfirming(true);
    try {
      await expenseDocumentService.confirm(id, buildRetentionIntentRequest(retention));
      message.success(t("expenses.form.message.confirmed"));
      setConfirmModalOpen(false);
      // EXPENSES-CONFIRM-FRONTEND-08: recargar desde API (no solo aplicar la respuesta del
      // confirm) para reflejar exactamente lo que el backend persistio, incluyendo el snapshot
      // de cuenta contable recongelado por linea.
      await load();
    } catch (error) {
      message.error(
        formatApiRequestError(error, {
          generic: t("expenses.form.message.confirmError"),
        }),
      );
    } finally {
      setConfirming(false);
    }
  };

  const openCancelModal = () => {
    setCancelReason("");
    setCancelReasonError(undefined);
    setCancelModalOpen(true);
  };

  const closeCancelModal = () => {
    setCancelModalOpen(false);
    setCancelReason("");
    setCancelReasonError(undefined);
  };

  const runCancel = async (reason: string, requestSriAnnulment: boolean) => {
    if (isNew || !id) return;
    setCancelling(true);
    try {
      const result = await expenseDocumentService.cancel(id, reason, requestSriAnnulment);
      closeCancelModal();
      if (result.status === "Cancelled") {
        message.success(t("expenses.form.message.cancelled"));
      } else {
        // La solicitud de anulación ante el SRI NO anula el gasto: sigue vigente hasta el ANULADO.
        message.success(t("expenses.form.message.sriAnnulmentRequested"));
      }
      // Mismo criterio que handleConfirm (EXPENSES-CONFIRM-FRONTEND-08): recargar desde API
      // para reflejar exactamente lo que el backend persistio (reverso contable/CxP incluidos).
      await load();
    } catch (error) {
      if (!requestSriAnnulment && readApiErrorCode(error) === SRI_ANNULMENT_REQUIRED_CODE) {
        closeCancelModal();
        setSriAnnulmentReason(reason);
      } else {
        message.error(
          formatApiRequestError(error, {
            generic: t("expenses.form.message.cancelError"),
          }),
        );
      }
    } finally {
      setCancelling(false);
    }
  };

  const handleCancel = async () => {
    if (isNew || !id) return;
    if (!cancelReason.trim()) {
      setCancelReasonError(t("expenses.form.message.cancelReasonRequired"));
      return;
    }
    setCancelReasonError(undefined);
    await runCancel(cancelReason.trim(), false);
  };

  const handleStartSriAnnulment = async () => {
    const reason = sriAnnulmentReason;
    setSriAnnulmentReason(null);
    if (reason) await runCancel(reason, true);
  };

  return (
    <PageShell
      kicker={t("app.nav.group.expenses")}
      title={isNew ? t("expenses.form.title.new") : t("expenses.form.title.edit")}
      subtitle={t("expenses.form.subtitle")}
      action={
        <div className="exp-doc-actions">
          {document && <ExpenseDocumentStatusBadge status={document.status} />}
          {canShowConfirmButton && (
            <ZHBtn
              type="button"
              variant="primary"
              disabled={confirming}
              onClick={() => setConfirmModalOpen(true)}
            >
              <span className="material-symbols-outlined" aria-hidden="true">
                task_alt
              </span>
              {t("expenses.form.action.confirm")}
            </ZHBtn>
          )}
          {canShowCancelButton && (
            <ZHBtn
              type="button"
              variant="destructive"
              disabled={cancelling}
              onClick={openCancelModal}
            >
              <span className="material-symbols-outlined" aria-hidden="true">
                cancel
              </span>
              {t("expenses.form.action.cancel")}
            </ZHBtn>
          )}
          <ZHBtn
            type="button"
            variant="ghost"
            onClick={() => navigate("/expenses/documents")}
          >
            <span className="material-symbols-outlined" aria-hidden="true">
              arrow_back
            </span>
            {t("common.back")}
          </ZHBtn>
        </div>
      }
    >
      {pageError && (
        <ZHPageNotice
          variant="error"
          message={t("expenses.form.pageError")}
          detail={pageError}
        />
      )}

      {!canReadCatalog && (
        <ZHFormAlert
          type="warning"
          message={t("expenses.form.alert.noCatalogPermission")}
          detail={t("expenses.form.alert.noCatalogPermissionDetail")}
        />
      )}

      {canReadCatalog && !catalogReady && !loading && (
        <ZHFormAlert
          type="attention"
          message={t("expenses.form.alert.noSubcategories")}
          detail={t("expenses.form.alert.noSubcategoriesDetail")}
        />
      )}

      {!isDraft && (
        <ZHFormAlert
          type="neutral"
          message={t("expenses.form.alert.notDraft")}
          detail={t("expenses.form.alert.notDraftDetail")}
        />
      )}

      <div className="exp-doc-form-layout">
        <div className="exp-doc-form-main">
          <ZHCard bodyClassName="exp-doc-card-body">
            {reception && (
              <p>
                {t("expenses.form.reception.summary", {
                  subtotal: formatDecimalDisplay(reception.subtotal, moneyDecimals),
                  vat: formatDecimalDisplay(reception.vatAmount, taxDecimals),
                  total: formatDecimalDisplay(reception.total, moneyDecimals),
                })}
              </p>
            )}
            {receptionMismatch && (
              <ZHFormAlert
                type="warning"
                message={t("expenses.form.reception.mismatchTitle")}
                detail={t("expenses.form.reception.mismatchDetail", {
                  vat: formatDecimalDisplay(reception!.vatAmount, taxDecimals),
                  received: formatDecimalDisplay(receptionMismatch.receivedTotal, moneyDecimals),
                  calculated: formatDecimalDisplay(receptionMismatch.calculatedTotal, moneyDecimals),
                })}
              />
            )}
            <ExpenseDocumentHeader
              key={document?.id ?? "new"}
              value={header}
              supplier={supplier}
              paymentTerms={paymentTerms}
              sriDocTypes={sriDocTypes}
              sriTaxSupports={sriTaxSupports}
              disabled={disabled || loading}
              errors={headerErrors}
              onChange={(patch) =>
                setHeader((current) => ({ ...current, ...patch }))
              }
              onSupplierChange={setSupplier}
            />
          </ZHCard>

          <ZHCard bodyClassName="exp-doc-card-body">
            <ExpenseDocumentLinesEditor
              lines={lines}
              tree={tree}
              accountsById={accountsById}
              vatRates={vatRates}
              vatRateByCode={vatRateByCode}
              disabled={disabled || loading || !catalogReady}
              errors={lineErrors}
              onChange={setLines}
            />
          </ZHCard>

          <ZHCard bodyClassName="exp-doc-card-body">
            <ExpenseRetentionSection
              expenseDocumentId={document?.id ?? null}
              documentStatus={document?.status}
              refreshKey={retentionRefreshKey}
              disabled={disabled}
              value={retention}
              onChange={(patch) => setRetention((current) => ({ ...current, ...patch }))}
              onEligibilityChange={setRetentionEligibility}
              onAnnulmentChanged={() => void load()}
            />
          </ZHCard>
        </div>

        <aside className="exp-doc-form-side">
          <ZHCard bodyClassName="exp-doc-card-body">
            <ExpenseDocumentTotals totals={totals} />
            <ZHFormActions
              onCancel={() => navigate("/expenses/documents")}
              onSave={handleSave}
              hideDraft
              disableSave={disabled || loading || !catalogReady || !!receptionMismatch}
              labels={{ save: saving ? t("expenses.form.action.saving") : t("common.saveDraft") }}
              buttonSize="md"
            />
            {!catalogReady && canReadCatalog && (
              <Link className="exp-doc-catalog-link" to="/expenses/categories">
                {t("expenses.form.action.openCategories")}
              </Link>
            )}
          </ZHCard>
        </aside>
      </div>

      <ZHConfirmModal
        open={confirmModalOpen}
        variant="warning"
        title={t("expenses.form.confirmModal.title")}
        message={t("expenses.form.confirmModal.message")}
        confirmLabel={
          confirming ? t("expenses.form.confirmModal.confirming") : t("expenses.form.action.confirm")
        }
        onCancel={() => setConfirmModalOpen(false)}
        onConfirm={handleConfirm}
      />

      <ZHConfirmModal
        open={cancelModalOpen}
        variant="danger"
        title={t("expenses.form.cancelModal.title")}
        message={
          <>
            <p className="zh-confirm-message">{t("expenses.form.cancelModal.message")}</p>
            <ZHField label={t("expenses.form.cancelModal.reason")} required error={cancelReasonError}>
              <ZhTextarea
                rows={3}
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                maxLength={500}
                aria-required="true"
                aria-label={t("expenses.form.cancelModal.reason")}
              />
            </ZHField>
          </>
        }
        confirmLabel={
          cancelling ? t("expenses.form.cancelModal.confirming") : t("expenses.form.cancelModal.confirm")
        }
        cancelLabel={t("common.cancel")}
        onCancel={closeCancelModal}
        onConfirm={handleCancel}
      />

      {/* ZH-RETENTION-SRI-ANNULMENT-01 — la retención ya está autorizada por el SRI. */}
      <ZHConfirmModal
        open={sriAnnulmentReason !== null}
        variant="warning"
        title={t("expenses.form.sriModal.title")}
        message={t("expenses.form.sriModal.message")}
        confirmLabel={
          cancelling ? t("expenses.form.sriModal.confirming") : t("expenses.form.sriModal.confirm")
        }
        cancelLabel={t("common.cancel")}
        onCancel={() => setSriAnnulmentReason(null)}
        onConfirm={() => void handleStartSriAnnulment()}
      />
    </PageShell>
  );
}

export default ExpenseDocumentFormPage;
