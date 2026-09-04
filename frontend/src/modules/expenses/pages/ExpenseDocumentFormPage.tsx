import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type SetStateAction,
} from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormAlert, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { todayIso, toDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import {
  formatApiRequestError,
  parseValidationErrors,
} from "../../lib/apiError";
import { accountingApi, type AccountDto } from "../../accounting/api/accountingApi";
import {
  sriLookupFacade,
  type SriTaxSupportLookup,
} from "../../items/facades/sriLookupFacade";
import { paymentTermService, type PaymentTermDto } from "../../masterData/api/paymentTermService";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";
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
import {
  buildExpenseDraftPayload,
  calculateExpenseDocumentTotals,
  documentToHeader,
  documentToLines,
  documentToSupplier,
  flattenExpenseSubcategories,
  hasConfiguredExpenseSubcategory,
  newExpenseDraftLine,
  parseExpenseNumber,
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
  const { id } = useParams();
  const isNew = !id;
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
  const [lines, setLines] = useState<ExpenseDraftLineState[]>([newExpenseDraftLine()]);
  const [tree, setTree] = useState<ExpenseCategoryTreeNodeDto[]>([]);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermDto[]>([]);
  const [sriTaxSupports, setSriTaxSupports] = useState<SriTaxSupportLookup[]>([]);
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
  const [retention, setRetention] = useState<RetentionIntentFormState>(
    emptyRetentionIntentState(),
  );
  const [retentionEligibility, setRetentionEligibility] =
    useState<RetentionEligibilityResult | null>(null);
  const [retentionRefreshKey, setRetentionRefreshKey] = useState(0);

  const accountsById = useMemo(
    () => new Map(accounts.map((account) => [account.id, account])),
    [accounts],
  );
  const totals = useMemo(() => calculateExpenseDocumentTotals(lines), [lines]);
  const canSave = isNew ? canCreate : canUpdate;
  const isDraft = document?.status ? document.status === "Draft" : true;
  const disabled = saving || !isDraft || !canSave;
  const canShowConfirmButton = !isNew && canConfirm && document?.status === "Draft";
  const canShowCancelButton = !isNew && canCancel && document?.status === "Confirmed";
  const catalogReady =
    canReadCatalog && hasConfiguredExpenseSubcategory(tree, accountsById);

  const load = useCallback(async () => {
    setLoading(true);
    setPageError(null);
    try {
      const requests = [
        canReadCatalog ? expenseCategoryService.getTree(false) : Promise.resolve([]),
        accountingApi.listAccounts(),
        paymentTermService.list(),
        sriLookupFacade.taxSupportCodes(),
        id ? expenseDocumentService.getById(id) : Promise.resolve(null),
      ] as const;
      const [categoryTree, accountRows, paymentTermRows, sriTaxSupportRows, expenseDocument] =
        await Promise.all(requests);

      setTree(categoryTree);
      setAccounts(accountRows);
      setPaymentTerms(paymentTermRows);
      setSriTaxSupports(sriTaxSupportRows);
      if (expenseDocument) {
        setDocument(expenseDocument);
        setHeader(documentToHeader(expenseDocument, toDateTimeLocalInputValue));
        setSupplier(documentToSupplier(expenseDocument));
        setLines(documentToLines(expenseDocument));
      } else {
        setDocument(null);
        setHeader(EMPTY_HEADER);
        setSupplier(null);
        setLines([newExpenseDraftLine()]);
      }
      setRetention(emptyRetentionIntentState());
      setRetentionRefreshKey((key) => key + 1);
    } catch (error) {
      setPageError(
        formatApiRequestError(error, {
          generic: "No se pudo cargar el documento de gasto.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [canReadCatalog, id]);

  useEffect(() => {
    if (canView || isNew) void load();
  }, [canView, isNew, load]);

  if (!canView && !isNew) return <NoAccessPage title="Gasto" />;
  if (isNew && !canCreate) return <NoAccessPage title="Nuevo gasto" />;

  const validate = (): boolean => {
    const nextHeader: ExpenseDocumentHeaderErrors = {};
    const nextLines: ExpenseLineFieldErrors = {};
    const subcategories = new Map(
      flattenExpenseSubcategories(tree).map((node) => [node.id, node]),
    );

    if (!header.supplierId) nextHeader.supplierId = "Seleccione un proveedor.";
    if (!header.issueDate) nextHeader.issueDate = "Ingrese la fecha de emision.";
    if (!header.accountingDate)
      nextHeader.accountingDate = "Ingrese la fecha contable.";
    if (!header.documentType.trim())
      nextHeader.documentType = "Ingrese el tipo de documento.";
    if (!header.documentNumber.trim())
      nextHeader.documentNumber = "Ingrese el numero de documento.";
    if (header.dueDate && header.issueDate && header.dueDate < header.issueDate)
      nextHeader.dueDate = "El vencimiento no puede ser anterior a la emision.";

    lines.forEach((line) => {
      const errors: Partial<Record<keyof ExpenseDraftLineState, string>> = {};
      const subcategory = subcategories.get(line.expenseSubcategoryId);
      const subtotal =
        parseExpenseNumber(line.quantity) * parseExpenseNumber(line.unitPrice);
      if (!line.expenseSubcategoryId) {
        errors.expenseSubcategoryId = "Seleccione una subcategoria.";
      } else if (
        !subcategory?.isActive ||
        !subcategory.accountingAccountId ||
        !accountsById.has(subcategory.accountingAccountId)
      ) {
        errors.expenseSubcategoryId =
          "La subcategoria debe estar activa y tener cuenta contable.";
      }
      if (parseExpenseNumber(line.quantity) <= 0)
        errors.quantity = "La cantidad debe ser mayor a cero.";
      if (parseExpenseNumber(line.unitPrice) < 0)
        errors.unitPrice = "El valor unitario no puede ser negativo.";
      if (parseExpenseNumber(line.discountValue) < 0)
        errors.discountValue = "El descuento no puede ser negativo.";
      if (parseExpenseNumber(line.discountValue) > subtotal)
        errors.discountValue = "El descuento no puede superar el subtotal.";
      if (!line.vatCode.trim()) errors.vatCode = "Seleccione el codigo IVA.";
      if (Object.keys(errors).length > 0) nextLines[line.key] = errors;
    });

    if (lines.length === 0) {
      const line = newExpenseDraftLine();
      nextLines[line.key] = { expenseSubcategoryId: "Debe incluir al menos una linea." };
      setLines([line]);
    }

    setHeaderErrors(nextHeader);
    setLineErrors(nextLines);
    return Object.keys(nextHeader).length === 0 && Object.keys(nextLines).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) {
      message.error("Revise los datos requeridos del borrador.");
      return;
    }

    setSaving(true);
    setHeaderErrors({});
    setLineErrors({});
    try {
      const payload = buildExpenseDraftPayload(header, lines);
      const saved = isNew
        ? await expenseDocumentService.create(payload)
        : await expenseDocumentService.update(id!, payload);
      message.success("Borrador de gasto guardado correctamente.");
      if (isNew) {
        navigate(`/expenses/documents/${saved.id}`, { replace: true });
      } else {
        setDocument(saved);
        setHeader(documentToHeader(saved, toDateTimeLocalInputValue));
        setSupplier(documentToSupplier(saved));
        setLines(documentToLines(saved));
      }
    } catch (error) {
      mapBackendErrors(error, setHeaderErrors);
      message.error(
        formatApiRequestError(error, {
          generic: "No se pudo guardar el borrador de gasto.",
        }),
      );
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
      message.error(
        "Este gasto no es elegible para retención con la configuración actual. Revise los motivos mostrados en la sección Retención.",
      );
      return;
    }
    if (retention.appliesRetention && !isRetentionIntentComplete(retention)) {
      message.error("Complete los datos de la retención (punto de emisión, número, fecha y líneas) antes de confirmar.");
      return;
    }

    setConfirming(true);
    try {
      await expenseDocumentService.confirm(id, buildRetentionIntentRequest(retention));
      message.success("Gasto confirmado. Se genero el asiento contable.");
      setConfirmModalOpen(false);
      // EXPENSES-CONFIRM-FRONTEND-08: recargar desde API (no solo aplicar la respuesta del
      // confirm) para reflejar exactamente lo que el backend persistio, incluyendo el snapshot
      // de cuenta contable recongelado por linea.
      await load();
    } catch (error) {
      message.error(
        formatApiRequestError(error, {
          generic: "No se pudo confirmar el gasto.",
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

  const handleCancel = async () => {
    if (isNew || !id) return;
    if (!cancelReason.trim()) {
      setCancelReasonError("Indique el motivo de la anulación.");
      return;
    }

    setCancelling(true);
    setCancelReasonError(undefined);
    try {
      await expenseDocumentService.cancel(id, cancelReason.trim());
      message.success("Gasto anulado correctamente.");
      closeCancelModal();
      // Mismo criterio que handleConfirm (EXPENSES-CONFIRM-FRONTEND-08): recargar desde API
      // para reflejar exactamente lo que el backend persistio (reverso contable/CxP incluidos).
      await load();
    } catch (error) {
      message.error(
        formatApiRequestError(error, {
          generic: "No se pudo anular el gasto.",
        }),
      );
    } finally {
      setCancelling(false);
    }
  };

  return (
    <PageShell
      kicker="Gastos"
      title={isNew ? "Nuevo gasto" : "Editar gasto"}
      subtitle="Borrador por proveedor, con detalle por subcategoria de gasto."
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
              Confirmar gasto
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
              Anular gasto
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
            Volver
          </ZHBtn>
        </div>
      }
    >
      {pageError && (
        <ZHPageNotice
          variant="error"
          message="No se pudo preparar la pantalla"
          detail={pageError}
        />
      )}

      {!canReadCatalog && (
        <ZHFormAlert
          type="warning"
          message="Sin permiso para leer el catalogo de gastos."
          detail="La seleccion de subcategorias requiere expenses.catalog.view."
        />
      )}

      {canReadCatalog && !catalogReady && !loading && (
        <ZHFormAlert
          type="attention"
          message="No hay subcategorias de gasto listas para usar."
          detail="Configure al menos una subcategoria activa con cuenta contable destino."
        />
      )}

      {!isDraft && (
        <ZHFormAlert
          type="neutral"
          message="Este documento ya no esta en borrador."
          detail="La edicion se habilita solo para gastos en estado Draft."
        />
      )}

      <div className="exp-doc-form-layout">
        <div className="exp-doc-form-main">
          <ZHCard bodyClassName="exp-doc-card-body">
            <ExpenseDocumentHeader
              value={header}
              supplier={supplier}
              paymentTerms={paymentTerms}
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
              disableSave={disabled || loading || !catalogReady}
              labels={{ save: saving ? "Guardando..." : "Guardar borrador" }}
              buttonSize="md"
            />
            {!catalogReady && canReadCatalog && (
              <Link className="exp-doc-catalog-link" to="/expenses/categories">
                Abrir catalogo de gastos
              </Link>
            )}
          </ZHCard>
        </aside>
      </div>

      <ZHConfirmModal
        open={confirmModalOpen}
        variant="warning"
        title="Confirmar gasto"
        message="Al confirmar, el gasto generara asiento contable y ya no podra editarse."
        confirmLabel={confirming ? "Confirmando..." : "Confirmar gasto"}
        onCancel={() => setConfirmModalOpen(false)}
        onConfirm={handleConfirm}
      />

      <ZHConfirmModal
        open={cancelModalOpen}
        variant="danger"
        title="Anular gasto"
        message={
          <>
            <p className="zh-confirm-message">
              Se reversará el asiento contable generado al confirmar y, si existe, se anulará la
              cuenta por pagar asociada (no permitido si ya tiene pagos aplicados).
            </p>
            <ZHField label="Motivo de anulación" required error={cancelReasonError}>
              <ZhTextarea
                rows={3}
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                maxLength={500}
                aria-required="true"
                aria-label="Motivo de anulación"
              />
            </ZHField>
          </>
        }
        confirmLabel={cancelling ? "Anulando..." : "Sí, anular"}
        cancelLabel="Cancelar"
        onCancel={closeCancelModal}
        onConfirm={handleCancel}
      />
    </PageShell>
  );
}

function mapBackendErrors(
  error: unknown,
  setHeaderErrors: Dispatch<SetStateAction<ExpenseDocumentHeaderErrors>>,
) {
  const validation = parseValidationErrors(error);
  if (!validation) return;
  const next: ExpenseDocumentHeaderErrors = {};
  const map: Record<string, keyof ExpenseDocumentHeaderState> = {
    supplierId: "supplierId",
    SupplierId: "supplierId",
    issueDate: "issueDate",
    IssueDate: "issueDate",
    accountingDate: "accountingDate",
    AccountingDate: "accountingDate",
    documentType: "documentType",
    DocumentType: "documentType",
    documentNumber: "documentNumber",
    DocumentNumber: "documentNumber",
    paymentTermId: "paymentTermId",
    PaymentTermId: "paymentTermId",
    dueDate: "dueDate",
    DueDate: "dueDate",
    taxSupportCode: "taxSupportCode",
    TaxSupportCode: "taxSupportCode",
  };
  Object.entries(validation).forEach(([field, messages]) => {
    const target = map[field];
    const first = messages[0];
    if (target && first) next[target] = first;
  });
  setHeaderErrors(next);
}

export default ExpenseDocumentFormPage;
