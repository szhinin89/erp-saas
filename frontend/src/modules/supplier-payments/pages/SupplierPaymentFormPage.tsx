import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHFormActions, ZHFormAlert } from "../../../components/zh/ZHForm";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import {
  paymentMethodLookupFacade,
  type PaymentMethodDto,
} from "../../sales/facades/paymentMethodLookupFacade";
import {
  financialDestinationService,
  type CompanyFinancialDestinationDto,
} from "../../finance/api/financialDestinationService";
import {
  pendingPayablesFacade,
  type PendingInstallmentOption,
} from "../api/pendingPayablesFacade";
import { supplierPaymentService } from "../api/supplierPaymentService";
import { SupplierPaymentHeader } from "../components/SupplierPaymentHeader";
import { SupplierPaymentMethodLinesEditor } from "../components/SupplierPaymentMethodLinesEditor";
import { SupplierPaymentApplicationsEditor } from "../components/SupplierPaymentApplicationsEditor";
import { SupplierPaymentAllocationPreview } from "../components/SupplierPaymentAllocationPreview";
import { SupplierPaymentConfirmModal } from "../components/SupplierPaymentConfirmModal";
import { computeAutomaticAllocations } from "../utils/allocation";
import {
  registerSupplierPaymentSchema,
  type RegisterSupplierPaymentFormValues,
} from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";
import "../styles/supplier-payments.css";

const PERMISSIONS = { create: "supplier-payments.create" } as const;

const EMPTY_METHOD_LINE = {
  paymentMethodId: "",
  financialDestinationId: "",
  amount: 0,
  referenceNumber: "",
  checkNumber: "",
  checkDate: "",
  notes: "",
};

const EMPTY_APPLICATION_LINE = { accountsPayableInstallmentId: "", amountApplied: 0 };

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — formulario de registro. Sin Draft: "Registrar pago" valida el
 * formulario y abre el modal de confirmación; solo al confirmar ese modal se llama al backend, que
 * confirma el pago en una única operación (aplica saldos + genera asiento, todo o nada).
 */
export function SupplierPaymentFormPage() {
  const { has } = usePermissionsUi();
  const canCreate = has(PERMISSIONS.create);
  const navigate = useNavigate();

  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [destinations, setDestinations] = useState<CompanyFinancialDestinationDto[]>([]);
  const [installments, setInstallments] = useState<PendingInstallmentOption[]>([]);
  const [supplierName, setSupplierName] = useState("");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pendingValues, setPendingValues] = useState<RegisterSupplierPaymentFormValues | null>(
    null,
  );
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  const form = useForm<RegisterSupplierPaymentFormValues>({
    resolver: zodResolver(registerSupplierPaymentSchema),
    defaultValues: {
      supplierId: "",
      paymentDate: todayIso(),
      receiptNumber: "",
      methodLines: [EMPTY_METHOD_LINE],
      applicationLines: [EMPTY_APPLICATION_LINE],
    },
  });
  const { handleSubmit, watch, setValue, setError } = form;
  const supplierId = watch("supplierId");

  useEffect(() => {
    paymentMethodLookupFacade.list(true).then(setMethods).catch(() => setMethods([]));
    financialDestinationService.list(true).then(setDestinations).catch(() => setDestinations([]));
  }, []);

  useEffect(() => {
    if (!supplierId) {
      setInstallments([]);
      setSupplierName("");
      return;
    }
    pendingPayablesFacade
      .listPendingInstallments(supplierId)
      .then(setInstallments)
      .catch(() => setInstallments([]));
    setValue("applicationLines", [EMPTY_APPLICATION_LINE]);
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
            message: `El monto no puede superar el saldo pendiente de la cuota (${installment.outstandingAmount.toFixed(2)}).`,
          });
          ok = false;
        }
      });

      return ok;
    },
    [methodsById, installmentsById, setError],
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
      const allocations = computeAutomaticAllocations(
        pendingValues.methodLines,
        pendingValues.applicationLines,
      );
      const totalAmount = pendingValues.methodLines.reduce((sum, l) => sum + (l.amount || 0), 0);

      const dto = await supplierPaymentService.register({
        supplierId: pendingValues.supplierId,
        paymentDate: pendingValues.paymentDate,
        totalAmount,
        receiptNumber: pendingValues.receiptNumber?.trim() || null,
        methodLines: pendingValues.methodLines.map((l) => ({
          paymentMethodId: l.paymentMethodId,
          financialDestinationId: l.financialDestinationId,
          amount: l.amount,
          referenceNumber: l.referenceNumber?.trim() || null,
          checkNumber: l.checkNumber?.trim() || null,
          checkDate: l.checkDate?.trim() || null,
          notes: l.notes?.trim() || null,
        })),
        applicationLines: pendingValues.applicationLines.map((l) => ({
          accountsPayableInstallmentId: l.accountsPayableInstallmentId,
          amountApplied: l.amountApplied,
        })),
        allocations,
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

        <ZHCard title="Medios de pago">
          <SupplierPaymentMethodLinesEditor
            methods={methods}
            destinations={destinations}
            disabled={saving}
          />
        </ZHCard>

        <ZHCard title="Cuotas a pagar">
          <SupplierPaymentApplicationsEditor installments={installments} disabled={saving} />
        </ZHCard>

        <ZHCard title="Distribución medio ↔ cuota (automática)">
          <SupplierPaymentAllocationPreview methods={methods} installments={installments} />
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
