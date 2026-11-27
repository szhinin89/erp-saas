import { useEffect } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhCheckbox, ZhDateInput, ZhTextInput, ZhTextarea } from "../../../components/zh/inputs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { formatMoney, roundToDecimals } from "../../../lib/sanitizers";
import { customerAdvanceService, type CustomerAdvanceDto } from "../api/customerAdvanceService";
import { useCustomerAdvanceCommand } from "../hooks/useCustomerAdvanceCommand";
import {
  buildCustomerAdvanceCompensationSchema,
  requiresEvidenceDate,
  type CompensationKind,
  type CustomerAdvanceCompensationFormValues,
} from "../schemas/customerAdvanceSchema";
import { CustomerAdvanceModalFooter } from "./CustomerAdvanceModalFooter";

/** Operación a compensar. Importe, destino y líneas salen siempre del registro original. */
export interface CompensationTarget {
  kind: CompensationKind;
  /** `operationId` de la aplicación o `id` de la devolución; vacío para el ingreso completo. */
  targetId: string;
  amount: number;
  /** Fecha de la operación original: límite inferior de la evidencia. */
  originalDate: string;
  /** Destino histórico ya legible (no editable). */
  destination?: string;
  /** CxC afectadas por una aplicación. */
  affected?: { label: string; amount: number }[];
}

const FIELDS = ["reason", "reference", "evidenceDate", "moneyNotReceived"] as const;

const emptyValues = (): CustomerAdvanceCompensationFormValues => ({
  reason: "",
  reference: "",
  evidenceDate: "",
  moneyNotReceived: false,
});

/**
 * Reversión de aplicación, de devolución, del ingreso y anulación administrativa: nunca piden
 * importe ni destino y cada una explica qué hace con el dinero.
 */
export function CustomerAdvanceCompensationModal({
  advance,
  target,
  onClose,
  onCompensated,
}: {
  advance: CustomerAdvanceDto;
  target: CompensationTarget | null;
  onClose: () => void;
  onCompensated: (advance: CustomerAdvanceDto) => void;
}) {
  const { t } = useI18n();
  const decimals = usePrecisionDecimals("money");
  const kind = target?.kind ?? "cancel";
  const form = useForm<CustomerAdvanceCompensationFormValues>({
    resolver: (values, context, options) =>
      zodResolver(
        buildCustomerAdvanceCompensationSchema(t, {
          kind,
          today: todayIso(),
          originalDate: target?.originalDate ?? advance.advanceDate,
        }),
      )(values, context, options),
    defaultValues: emptyValues(),
  });
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = form;
  const command = useCustomerAdvanceCommand<CustomerAdvanceCompensationFormValues>(
    setError,
    FIELDS,
  );
  const { clearError } = command;

  useEffect(() => {
    if (!target) return;
    reset(emptyValues());
    clearError();
    // Solo al cambiar de operación: clearError cambia en cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [target?.kind, target?.targetId, reset]);

  if (!target) return null;

  const text = (key: string, params?: Record<string, string>) =>
    t(`finance.customerAdvances.${target.kind}.${key}`, params);
  const resulting =
    target.kind === "reverseApplication" || target.kind === "reverseRefund"
      ? roundToDecimals(advance.availableAmount + target.amount, 2)
      : 0;

  const close = () => {
    if (!command.saving) onClose();
  };

  const send = (values: CustomerAdvanceCompensationFormValues, clientRequestId: string) => {
    const base = { reason: values.reason, reference: values.reference, clientRequestId };
    const evidenced = { ...base, evidenceDate: values.evidenceDate };
    switch (target.kind) {
      case "reverseApplication":
        return customerAdvanceService.reverseApplication(advance.id, target.targetId, base);
      case "reverseRefund":
        return customerAdvanceService.reverseRefund(advance.id, target.targetId, evidenced);
      case "reverseIncome":
        return customerAdvanceService.reverseIncome(advance.id, evidenced);
      default:
        return customerAdvanceService.cancel(advance.id, {
          ...base,
          moneyNotReceived: values.moneyNotReceived,
        });
    }
  };

  const submit = handleSubmit(async (values) => {
    await command.run({
      intent: { advanceId: advance.id, kind: target.kind, targetId: target.targetId, ...values },
      confirm: {
        title: text("title"),
        message: text("confirm", { amount: formatMoney(target.amount, decimals) }),
        confirmLabel: text("action"),
        variant: "danger",
      },
      send: (clientRequestId) => send(values, clientRequestId),
      successMessage: text("success"),
      errorMessage: text("error"),
      onDone: (updated) => {
        onCompensated(updated);
        onClose();
      },
    });
  });

  return (
    <ZHModal
      open
      onClose={close}
      size="md"
      title={text("title")}
      subtitle={advance.customerName ?? undefined}
      footer={
        <CustomerAdvanceModalFooter
          saving={command.saving}
          actionLabel={text("action")}
          onCancel={close}
          onSubmit={() => void submit()}
        />
      }
    >
      <FormProvider {...form}>
        <ZHGrid cols={1}>
          <ZHPageNotice variant="warning" message={text("explanation")} />
          <div>
            <ZHInfoRow
              label={t("finance.customerAdvances.field.operationAmount")}
              value={<ZHMoneyValue precision="money" value={target.amount} />}
            />
            {target.destination ? (
              <ZHInfoRow
                label={t("finance.customerAdvances.field.originalDestination")}
                value={target.destination}
              />
            ) : null}
            {target.affected?.map((line, index) => (
              <ZHInfoRow
                key={`${line.label}-${index}`}
                label={line.label}
                value={<ZHMoneyValue precision="money" value={line.amount} />}
              />
            ))}
            <ZHInfoRow
              label={t("finance.customerAdvances.field.resulting")}
              value={<ZHMoneyValue precision="money" value={resulting} />}
            />
          </div>

          <ZHField
            label={t("finance.customerAdvances.field.reason")}
            required
            fieldError={errors.reason?.message}
          >
            <ZhTextarea maxLength={500} disabled={command.saving} {...register("reason")} />
          </ZHField>

          <ZHField label={text("reference")} required fieldError={errors.reference?.message}>
            <ZhTextInput
              className="zh-input"
              maxLength={100}
              disabled={command.saving}
              {...register("reference")}
            />
          </ZHField>

          {requiresEvidenceDate(target.kind) ? (
            <ZHField
              label={t("finance.customerAdvances.field.evidenceDate")}
              required
              fieldError={errors.evidenceDate?.message}
              hint={t("finance.customerAdvances.hint.evidenceDate")}
            >
              <ZhDateInput
                min={target.originalDate}
                max={todayIso()}
                disabled={command.saving}
                {...register("evidenceDate")}
              />
            </ZHField>
          ) : null}

          {target.kind === "cancel" ? (
            <ZHField fieldError={errors.moneyNotReceived?.message}>
              <ZhCheckbox
                label={t("finance.customerAdvances.cancel.declaration")}
                disabled={command.saving}
                {...register("moneyNotReceived")}
              />
            </ZHField>
          ) : null}

          {command.submitError ? (
            <ZHPageNotice
              variant="error"
              message={t("common.error")}
              detail={command.submitError}
            />
          ) : null}
        </ZHGrid>
      </FormProvider>
    </ZHModal>
  );
}
