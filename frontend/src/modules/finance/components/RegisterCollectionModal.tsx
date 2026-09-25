import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZhDecimalInput,
  ZhTextInput,
  ZhSelect,
} from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import { formatMoney } from "../../../lib/sanitizers";
import type { SalesReceivableDto } from "../api/receivableService";
import { paymentService } from "../api/paymentService";
import {
  paymentMethodLookupFacade,
  type PaymentMethodDto,
} from "../../sales/facades/paymentMethodLookupFacade";
import { bankAccountService, type CompanyBankAccountDto } from "../api/bankAccountService";
import { cajaService, type CashRegisterDto } from "../../caja/api/cajaService";
import {
  buildRegisterCollectionSchema,
  type RegisterCollectionFormValues,
} from "../../../schemas/finance/registerCollectionSchema";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

interface Props {
  open: boolean;
  receivable: SalesReceivableDto | null;
  onClose: () => void;
  onRegistered: () => void;
}

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — registra un cobro contra una CxC seleccionada.
 * Aplicación de un único documento por operación (con cuota opcional) — mantiene la UI mínima;
 * el backend (RegisterCollectionCommand) soporta múltiples líneas, sin usarlas desde aquí.
 */
export function RegisterCollectionModal({
  open,
  receivable,
  onClose,
  onRegistered,
}: Props) {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [bankAccounts, setBankAccounts] = useState<CompanyBankAccountDto[]>([]);
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const submittingRef = useRef(false);

  const maxAmount = receivable?.balanceDue ?? 0;
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<RegisterCollectionFormValues>({
    resolver: zodResolver(buildRegisterCollectionSchema(maxAmount, moneyDecimals)),
    defaultValues: {
      amount: maxAmount,
      installmentId: "",
      paymentMethodId: "",
      destination: "",
      reference: "",
    },
  });

  useEffect(() => {
    if (!open || !receivable) return;
    reset({
      amount: receivable.balanceDue,
      installmentId: "",
      paymentMethodId: "",
      destination: "",
      reference: "",
    });
    setSubmitError("");
    paymentMethodLookupFacade
      .list(true)
      .then(setMethods)
      .catch(() => setMethods([]));
    bankAccountService
      .list(true)
      .then(setBankAccounts)
      .catch(() => setBankAccounts([]));
    cajaService
      .getCashRegisters(true)
      .then(setCashRegisters)
      .catch(() => setCashRegisters([]));
  }, [open, receivable, reset]);

  const handleClose = () => {
    if (saving) return;
    setSubmitError("");
    onClose();
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current || !receivable) return;
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    const destination = values.destination || "";
    const companyBankAccountId = destination.startsWith("bank:")
      ? destination.slice(5)
      : null;
    const cashRegisterId = destination.startsWith("cash:") ? destination.slice(5) : null;
    try {
      await paymentService.registerCollection({
        customerId: receivable.customerId,
        amount: values.amount,
        paymentDate: new Date().toISOString().slice(0, 10),
        paymentMethodId: values.paymentMethodId || null,
        companyBankAccountId,
        cashRegisterId,
        reference: values.reference || null,
        lines: [
          {
            documentId: receivable.id,
            installmentId: values.installmentId || null,
            appliedAmount: values.amount,
          },
        ],
      });
      message.success("Cobro registrado correctamente.");
      onRegistered();
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => setSubmitError(msg));
      if (!applied) {
        const fromApi = readApiErrorMessage(err);
        setSubmitError(
          fromApi ||
            formatApiRequestError(err, {
              generic: "No se pudo registrar el cobro.",
            }),
        );
      }
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  if (!receivable) return null;

  const pendingInstallments = receivable.installments.filter(
    (i) => i.status !== "paid" && i.paidAmount < i.amount,
  );

  return (
    <ZHModal
      open={open}
      onClose={handleClose}
      size="md"
      title="Registrar cobro"
      subtitle={`Factura ${receivable.invoiceNumber} — Cliente: ${receivable.customerName} — Saldo pendiente: ${formatMoney(receivable.balanceDue, moneyDecimals)}`}
    >
      <div>
        <ZHField label="Monto a cobrar" error={errors.amount?.message} required>
          <ZhDecimalInput
            precision="money"
            positiveOnly
            disabled={saving}
            {...register("amount", {
              valueAsNumber: true,
              setValueAs: (v) => (v === "" ? null : Number(v)),
            })}
          />
        </ZHField>

        {pendingInstallments.length > 0 && (
          <ZHField label="Cuota (opcional)" error={errors.installmentId?.message}>
            <ZhSelect className="zh-input" disabled={saving} {...register("installmentId")}>
              <option value="">Sin cuota específica</option>
              {pendingInstallments.map((i) => (
                <option key={i.id} value={i.id}>
                  Cuota #{i.installmentNumber} — {formatMoney(i.amount - i.paidAmount, moneyDecimals)} pendiente
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        )}

        <ZHField label="Forma de pago (opcional)" error={errors.paymentMethodId?.message}>
          <ZhSelect className="zh-input" disabled={saving} {...register("paymentMethodId")}>
            <option value="">Sin especificar</option>
            {methods.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField
          label="Cuenta bancaria o caja (opcional)"
          error={errors.destination?.message}
          hint="Cuenta contable usada para generar asientos automáticos cuando esta cuenta bancaria/caja se use en cobros."
        >
          <ZhSelect className="zh-input" disabled={saving} {...register("destination")}>
            <option value="">Sin especificar</option>
            {bankAccounts.map((b) => (
              <option key={b.id} value={`bank:${b.id}`}>
                Banco: {b.displayName}
              </option>
            ))}
            {cashRegisters.map((c) => (
              <option key={c.id} value={`cash:${c.id}`}>
                Caja: {c.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField label="Referencia (opcional)" error={errors.reference?.message}>
          <ZhTextInput
            className="zh-input"
            maxLength={200}
            disabled={saving}
            placeholder="N.º de transferencia, cheque, etc."
            {...register("reference")}
          />
        </ZHField>

        {submitError ? (
          <ZHPageNotice variant="error" message="Error" detail={submitError} />
        ) : null}

        <ZHFormActions
          onCancel={handleClose}
          onSave={() => {
            void onValid();
          }}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: "Cancelar",
            save: saving ? "Guardando..." : "Registrar cobro",
          }}
        />
      </div>
    </ZHModal>
  );
}
