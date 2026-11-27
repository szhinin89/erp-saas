import { useEffect } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZhDateInput,
  ZhDecimalInput,
  ZhTextInput,
  ZhTextarea,
} from "../../../components/zh/inputs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { formatMoney, roundToDecimals } from "../../../lib/sanitizers";
import { customerAdvanceService, type CustomerAdvanceDto } from "../api/customerAdvanceService";
import { useCustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";
import { useCustomerAdvanceCommand } from "../hooks/useCustomerAdvanceCommand";
import {
  buildRefundCustomerAdvanceSchema,
  type RefundCustomerAdvanceFormValues,
} from "../schemas/customerAdvanceSchema";
import { splitDestination } from "../utils/customerAdvance";
import { CustomerAdvanceMoneyFields } from "./CustomerAdvanceMoneyFields";
import { CustomerAdvanceModalFooter } from "./CustomerAdvanceModalFooter";

const FIELDS = [
  "amount",
  "effectiveDate",
  "paymentMethodId",
  "destination",
  "reference",
  "reason",
] as const;
const ALIASES = { companyBankAccountId: "destination", cashRegisterId: "destination" };

const emptyValues = (available: number): RefundCustomerAdvanceFormValues => ({
  amount: available,
  effectiveDate: todayIso(),
  paymentMethodId: "",
  destination: "",
  reference: "",
  reason: "",
});

/**
 * Devolución de dinero del saldo disponible. El ERP registra la salida y su evidencia declarada;
 * no ejecuta transferencias bancarias.
 */
export function RefundCustomerAdvanceModal({
  open,
  advance,
  onClose,
  onRefunded,
}: {
  open: boolean;
  advance: CustomerAdvanceDto;
  onClose: () => void;
  onRefunded: (advance: CustomerAdvanceDto) => void;
}) {
  const { t } = useI18n();
  const decimals = usePrecisionDecimals("money");
  const catalogs = useCustomerAdvanceCatalogs(open);
  const form = useForm<RefundCustomerAdvanceFormValues>({
    resolver: (values, context, options) =>
      zodResolver(
        buildRefundCustomerAdvanceSchema(t, {
          today: todayIso(),
          advanceDate: advance.advanceDate,
          available: advance.availableAmount,
          decimals,
        }),
      )(values, context, options),
    defaultValues: emptyValues(advance.availableAmount),
  });
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setError,
    formState: { errors },
  } = form;
  const command = useCustomerAdvanceCommand<RefundCustomerAdvanceFormValues>(
    setError,
    FIELDS,
    ALIASES,
  );
  const { clearError } = command;

  useEffect(() => {
    if (!open) return;
    reset(emptyValues(advance.availableAmount));
    clearError();
    // Solo al abrir o cambiar de anticipo: clearError cambia en cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, advance.id, advance.availableAmount, reset]);

  const amount = Number(watch("amount")) || 0;
  const resulting = roundToDecimals(advance.availableAmount - amount, 2);

  const close = () => {
    if (!command.saving) onClose();
  };

  const submit = handleSubmit(async ({ destination, ...values }) => {
    const payload = { ...values, ...splitDestination(destination) };
    await command.run({
      intent: { advanceId: advance.id, ...payload },
      confirm: {
        title: t("finance.customerAdvances.refund.title"),
        message: t("finance.customerAdvances.refund.confirm", {
          amount: formatMoney(payload.amount, decimals),
          date: formatDate(payload.effectiveDate),
          available: formatMoney(advance.availableAmount, decimals),
          resulting: formatMoney(
            roundToDecimals(advance.availableAmount - payload.amount, 2),
            decimals,
          ),
          destination: catalogs.destination(payload.companyBankAccountId, payload.cashRegisterId)
            .name,
        }),
        confirmLabel: t("finance.customerAdvances.refund.action"),
        variant: "danger",
      },
      send: (clientRequestId) =>
        customerAdvanceService.refund(advance.id, { ...payload, clientRequestId }),
      successMessage: t("finance.customerAdvances.refund.success"),
      errorMessage: t("finance.customerAdvances.refund.error"),
      onDone: (updated) => {
        onRefunded(updated);
        onClose();
      },
    });
  });

  return (
    <ZHModal
      open={open}
      onClose={close}
      size="md"
      title={t("finance.customerAdvances.refund.title")}
      subtitle={advance.customerName ?? undefined}
      footer={
        <CustomerAdvanceModalFooter
          saving={command.saving}
          actionLabel={t("finance.customerAdvances.refund.action")}
          onCancel={close}
          onSubmit={() => void submit()}
        />
      }
    >
      <FormProvider {...form}>
        <ZHGrid cols={2}>
          <p className="zh-col-span-2">{t("finance.customerAdvances.refund.explanation")}</p>
          <div className="zh-col-span-2">
            <ZHInfoRow
              label={t("finance.customerAdvances.field.available")}
              value={<ZHMoneyValue precision="money" value={advance.availableAmount} />}
            />
            <ZHInfoRow
              label={t("finance.customerAdvances.field.resulting")}
              value={<ZHMoneyValue precision="money" value={resulting} />}
            />
          </div>

          <ZHField
            label={t("finance.customerAdvances.field.amountToRefund")}
            required
            fieldError={errors.amount?.message}
          >
            <ZhDecimalInput
              precision="money"
              positiveOnly
              disabled={command.saving}
              {...register("amount")}
            />
          </ZHField>

          <ZHField
            label={t("finance.customerAdvances.field.effectiveDate")}
            required
            fieldError={errors.effectiveDate?.message}
          >
            <ZhDateInput
              min={advance.advanceDate}
              max={todayIso()}
              disabled={command.saving}
              {...register("effectiveDate")}
            />
          </ZHField>

          <CustomerAdvanceMoneyFields catalogs={catalogs} disabled={command.saving} />

          <ZHField
            className="zh-col-span-2"
            label={t("finance.customerAdvances.field.executionReference")}
            required
            fieldError={errors.reference?.message}
          >
            <ZhTextInput
              className="zh-input"
              maxLength={100}
              disabled={command.saving}
              {...register("reference")}
            />
          </ZHField>

          <ZHField
            className="zh-col-span-2"
            label={t("finance.customerAdvances.field.reason")}
            required
            fieldError={errors.reason?.message}
          >
            <ZhTextarea maxLength={500} disabled={command.saving} {...register("reason")} />
          </ZHField>

          {command.submitError ? (
            <div className="zh-col-span-2">
              <ZHPageNotice
                variant="error"
                message={t("common.error")}
                detail={command.submitError}
              />
            </div>
          ) : null}
        </ZHGrid>
      </FormProvider>
    </ZHModal>
  );
}
