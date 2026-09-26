import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions, ZHFormAlert } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { formatMoney } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import {
  paymentMethodLookupFacade,
  type PaymentMethodDto,
} from "../../sales/facades/paymentMethodLookupFacade";
import { bankAccountService, type CompanyBankAccountDto } from "../../finance/api/bankAccountService";
import { cajaService, type CashRegisterDto } from "../../caja/api/cajaService";
import {
  pendingPayablesFacade,
  type PendingInstallmentOption,
} from "../api/pendingPayablesFacade";
import { supplierPaymentService } from "../api/supplierPaymentService";
import { SupplierPaymentHeader } from "../components/SupplierPaymentHeader";
import { SupplierPayablesPortfolio } from "../components/SupplierPayablesPortfolio";
import { SupplierPaymentMethodLinesEditor } from "../components/SupplierPaymentMethodLinesEditor";
import { SupplierPaymentAllocationPreview } from "../components/SupplierPaymentAllocationPreview";
import { SupplierPaymentConfirmModal } from "../components/SupplierPaymentConfirmModal";
import { computeAutomaticAllocations, computePaymentSplit } from "../utils/allocation";
import {
  buildRegisterSupplierPaymentSchema,
  type RegisterSupplierPaymentFormValues,
} from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";
import "../styles/supplier-payments.css";
import { todayIso } from "../../../lib/formatters/dateFormatters";

const PERMISSIONS = { create: "supplier-payments.create" } as const;

const EMPTY_METHOD_LINE = {
  paymentMethodId: "",
  destination: "",
  amount: 0,
  referenceNumber: "",
  checkNumber: "",
  checkDate: "",
  transactionDate: "",
  notes: "",
};

/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — formulario de registro. Sin Draft: "Registrar pago" valida el
 * formulario y abre el modal de confirmación; solo al confirmar ese modal se llama al backend, que
 * confirma el pago en una única operación (aplica saldos + genera asiento, todo o nada).
 */
export function SupplierPaymentFormPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // representación del mensaje (04G)
  const { has } = usePermissionsUi();
  const canCreate = has(PERMISSIONS.create);
  const navigate = useNavigate();

  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [bankAccounts, setBankAccounts] = useState<CompanyBankAccountDto[]>([]);
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const [installments, setInstallments] = useState<PendingInstallmentOption[]>([]);
  const [installmentsLoading, setInstallmentsLoading] = useState(false);
  const [supplierName, setSupplierName] = useState("");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pendingValues, setPendingValues] = useState<RegisterSupplierPaymentFormValues | null>(
    null,
  );
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);
  // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — política de empresa "pago sin CxP". Fail-closed:
  // si no puede leerse queda en false (el backend la aplica igual; esto es solo UX).
  const [allowWithoutPayable, setAllowWithoutPayable] = useState(false);
  const allowWithoutPayableRef = useRef(false);

  const form = useForm<RegisterSupplierPaymentFormValues>({
    resolver: zodResolver(
      buildRegisterSupplierPaymentSchema(moneyDecimals, {
        allowWithoutPayable: () => allowWithoutPayableRef.current,
      }),
    ),
    defaultValues: {
      supplierId: "",
      paymentDate: todayIso(),
      receiptNumber: "",
      methodLines: [EMPTY_METHOD_LINE],
      applicationLines: [],
    },
  });
  const { handleSubmit, watch, setValue, setError } = form;
  const supplierId = watch("supplierId");
  const watchedMethodLines = watch("methodLines") ?? [];
  const watchedApplicationLines = watch("applicationLines") ?? [];
  const split = computePaymentSplit(watchedMethodLines, watchedApplicationLines);

  useEffect(() => {
    supplierPaymentService
      .getPolicy()
      .then((policy) => {
        allowWithoutPayableRef.current = policy.allowWithoutPayable;
        setAllowWithoutPayable(policy.allowWithoutPayable);
      })
      .catch(() => {
        allowWithoutPayableRef.current = false;
        setAllowWithoutPayable(false);
      });
    paymentMethodLookupFacade.list(true).then(setMethods).catch(() => setMethods([]));
    bankAccountService.list(true).then(setBankAccounts).catch(() => setBankAccounts([]));
    cajaService.getCashRegisters(true).then(setCashRegisters).catch(() => setCashRegisters([]));
  }, []);

  useEffect(() => {
    if (!supplierId) {
      setInstallments([]);
      setInstallmentsLoading(false);
      setSupplierName("");
      return;
    }
    setInstallmentsLoading(true);
    pendingPayablesFacade
      .listPendingInstallments(supplierId)
      .then(setInstallments)
      .catch(() => setInstallments([]))
      .finally(() => setInstallmentsLoading(false));
    setValue("applicationLines", []);
    businessPartnerFacade
      .getBusinessPartner(supplierId)
      .then((bp) => setSupplierName(bp.tradeName?.trim() || bp.legalName))
      .catch(() => setSupplierName(""));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [supplierId]);

  const installmentsById = useMemo(
    () => new Map(installments.map((i) => [i.installmentId, i])),
    [installments],
  );
  const methodsById = useMemo(() => new Map(methods.map((m) => [m.id, m])), [methods]);

  const validateRuntimeRules = useCallback(
    (values: RegisterSupplierPaymentFormValues): boolean => {
      let ok = true;

      values.methodLines.forEach((line, idx) => {
        const method = line.paymentMethodId ? methodsById.get(line.paymentMethodId) : undefined;
        // 02A — mismo contrato que el backend (PaymentMethod SSOT): crédito prohibido; efectivo
        // físico ⇒ caja; cualquier otro ⇒ cuenta bancaria con número de operación si lo exige.
        if (method?.isCreditAllowed) {
          setError(`methodLines.${idx}.paymentMethodId`, {
            type: "manual",
            message: "Un medio de crédito no puede usarse para pagar a un proveedor.",
          });
          ok = false;
        }
        if (method && line.destination) {
          const wantsCash = method.affectsPhysicalCash;
          if (wantsCash !== line.destination.startsWith("cash:")) {
            setError(`methodLines.${idx}.destination`, {
              type: "manual",
              message: wantsCash
                ? "Este medio mueve efectivo: seleccione una caja."
                : "Este medio es bancario: seleccione una cuenta bancaria.",
            });
            ok = false;
          }
        }
        if (line.destination.startsWith("bank:") && !line.transactionDate?.trim()) {
          setError(`methodLines.${idx}.transactionDate`, {
            type: "manual",
            message: "La fecha de la transacción bancaria es obligatoria.",
          });
          ok = false;
        }
        if (
          method &&
          !method.affectsPhysicalCash &&
          method.detailType !== "Check" &&
          method.requiresReference &&
          !line.referenceNumber?.trim()
        ) {
          setError(`methodLines.${idx}.referenceNumber`, {
            type: "manual",
            message: "El número de operación bancaria es obligatorio para este medio de pago.",
          });
          ok = false;
        }
        if (method?.detailType === "Check") {
          if (!line.checkNumber?.trim()) {
            setError(`methodLines.${idx}.checkNumber`, {
              type: "manual",
              message: "El número de cheque es obligatorio para este medio de pago.",
            });
            ok = false;
          }
          if (!line.checkDate?.trim()) {
            setError(`methodLines.${idx}.checkDate`, {
              type: "manual",
              message: "La fecha del cheque es obligatoria para este medio de pago.",
            });
            ok = false;
          }
        }
      });

      values.applicationLines.forEach((line, idx) => {
        const installment = line.accountsPayableInstallmentId
          ? installmentsById.get(line.accountsPayableInstallmentId)
          : undefined;
        if (installment && line.amountApplied > installment.outstandingAmount + 0.005) {
          setError(`applicationLines.${idx}.amountApplied`, {
            type: "manual",
            message: `El monto no puede superar el saldo pendiente de la cuota (${formatMoney(installment.outstandingAmount, moneyDecimals)}).`,
          });
          ok = false;
        }
      });

      return ok;
    },
    [methodsById, installmentsById, setError, moneyDecimals],
  );

  const onValid = handleSubmit((values) => {
    setPageError(null);
    if (!validateRuntimeRules(values)) return;
    setModalError(null);
    setPendingValues(values);
    setConfirmOpen(true);
  });

  const handleConfirm = async () => {
    if (!pendingValues || saving) return;
    setSaving(true);
    setModalError(null);
    try {
      // SUPPLIER-PAYMENT-REMOVE-DUPLICATED-APPLICATION-LINES-FLOW-01 — la cartera pendiente
      // (SupplierPayablesPortfolio) ya elimina/omite las cuotas sin monto al construir
      // applicationLines, pero el submit filtra de nuevo por defensa en profundidad: nunca debe
      // viajar al backend una línea con amountApplied <= 0.
      const validApplicationLines = pendingValues.applicationLines.filter(
        (l) => l.amountApplied > 0,
      );
      const allocations = computeAutomaticAllocations(
        pendingValues.methodLines,
        validApplicationLines,
      );
      const { total: totalAmount, unapplied } = computePaymentSplit(
        pendingValues.methodLines,
        validApplicationLines,
      );

      const dto = await supplierPaymentService.register({
        supplierId: pendingValues.supplierId,
        paymentDate: pendingValues.paymentDate,
        totalAmount,
        receiptNumber: pendingValues.receiptNumber?.trim() || null,
        methodLines: pendingValues.methodLines.map((l) => {
          const isBank = l.destination.startsWith("bank:");
          return {
            paymentMethodId: l.paymentMethodId,
            companyBankAccountId: isBank ? l.destination.slice(5) : null,
            cashRegisterId: l.destination.startsWith("cash:") ? l.destination.slice(5) : null,
            amount: l.amount,
            referenceNumber: l.referenceNumber?.trim() || null,
            checkNumber: l.checkNumber?.trim() || null,
            checkDate: l.checkDate?.trim() || null,
            notes: l.notes?.trim() || null,
            // 02A-FINAL — cada fuente bancaria envía SIEMPRE su fecha explícita (precargada en el
            // formulario, validada arriba); nunca se completa aquí ni en el backend.
            transactionDate: isBank ? l.transactionDate?.trim() || null : null,
          };
        }),
        applicationLines: validApplicationLines.map((l) => ({
          accountsPayableInstallmentId: l.accountsPayableInstallmentId,
          amountApplied: l.amountApplied,
        })),
        allocations,
        // 02C — el usuario acaba de aceptar el modal que le mostró el remanente como anticipo:
        // esa es la confirmación explícita (el backend la exige y la revalida).
        confirmUnappliedAmount: unapplied > 0,
      });

      message.success(`Pago ${dto.displayNumber} registrado correctamente.`);
      setConfirmOpen(false);
      navigate(`/supplier-payments/${dto.id}`);
    } catch (err) {
      const applied = applyServerErrors(err, setError, (msg) => setModalError(msg));
      if (!applied) {
        const fromApi = readApiErrorMessage(err);
        setModalError(
          fromApi || formatApiRequestError(err, { generic: "No se pudo registrar el pago." }),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  if (!canCreate) return <NoAccessPage title="Registrar pago a proveedor" />;

  return (
    <PageShell
      kicker="Finanzas"
      title="Registrar pago a proveedor"
      subtitle="El pago se confirma de inmediato al aceptar el modal de confirmación — no hay borrador ni edición posterior."
      action={
        <ZHBtn type="button" variant="ghost" onClick={() => navigate("/supplier-payments")}>
          Cancelar
        </ZHBtn>
      }
    >
      <FormProvider {...form}>
        <ZHCard title="Datos del pago">
          <SupplierPaymentHeader disabled={saving} />
        </ZHCard>

        {supplierId && (
          <ZHCard title="Cartera pendiente del proveedor">
            <SupplierPayablesPortfolio
              installments={installments}
              loading={installmentsLoading}
              disabled={saving}
            />
          </ZHCard>
        )}

        <ZHCard title="Medios de pago">
          <SupplierPaymentMethodLinesEditor
            methods={methods}
            bankAccounts={bankAccounts}
            cashRegisters={cashRegisters}
            disabled={saving}
          />
        </ZHCard>

        <ZHCard title="Distribución medio ↔ cuota (automática)">
          <SupplierPaymentAllocationPreview methods={methods} installments={installments} />
        </ZHCard>

        <ZHCard title="Resumen del pago">
          <div className="sp-detail-summary">
            <ZHField label="Total pago" readOnly>
              <ZHMoneyValue value={split.total} precision="money" emphasis="strong" />
            </ZHField>
            <ZHField label="Aplicado a CxP" readOnly>
              <ZHMoneyValue value={split.applied} precision="money" />
            </ZHField>
            <ZHField label="Anticipo generado" readOnly>
              <ZHMoneyValue value={split.unapplied > 0 ? split.unapplied : 0} precision="money" />
            </ZHField>
          </div>
          {split.total > 0 && split.applied === 0 && allowWithoutPayable && (
            <ZHPageNotice variant="info" message="Este pago quedará pendiente de aplicar." />
          )}
          {split.applied > 0 && split.unapplied > 0 && (
            <ZHPageNotice
              variant="warning"
              message="El excedente sobre las cuotas seleccionadas quedará como anticipo a favor del proveedor."
            />
          )}
        </ZHCard>

        {pageError && <ZHFormAlert type="error" message="No se pudo continuar" detail={pageError} />}

        <ZHFormActions
          onCancel={() => navigate("/supplier-payments")}
          onSave={() => void onValid()}
          hideDraft
          disableSave={saving}
          labels={{ cancel: "Cancelar", save: "Registrar pago" }}
        />
      </FormProvider>

      <SupplierPaymentConfirmModal
        open={confirmOpen}
        values={pendingValues}
        supplierName={supplierName}
        methods={methods}
        installments={installments}
        saving={saving}
        submitError={modalError}
        onCancel={() => {
          if (saving) return;
          setConfirmOpen(false);
        }}
        onConfirm={() => void handleConfirm()}
      />
    </PageShell>
  );
}

export default SupplierPaymentFormPage;
