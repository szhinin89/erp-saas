import { useEffect } from "react";
import { Controller, FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
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
import { formatMoney } from "../../../lib/sanitizers";
import { customerAdvanceService, type CustomerAdvanceDto } from "../api/customerAdvanceService";
import { useCustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";
import { useCustomerAdvanceCommand } from "../hooks/useCustomerAdvanceCommand";
import {
  buildRegisterCustomerAdvanceSchema,
  type RegisterCustomerAdvanceFormValues,
} from "../schemas/customerAdvanceSchema";
import { splitDestination } from "../utils/customerAdvance";
import { CustomerAdvanceMoneyFields } from "./CustomerAdvanceMoneyFields";
import { CustomerAdvanceModalFooter } from "./CustomerAdvanceModalFooter";
import { CustomerAdvanceCustomerPicker } from "./CustomerAdvanceCustomerPicker";

const FIELDS = [
  "customerId",
  "amount",
  "advanceDate",
  "paymentMethodId",
  "destination",
  "reference",
  "reason",
] as const;
const ALIASES = { companyBankAccountId: "destination", cashRegisterId: "destination" };

const emptyValues = (): RegisterCustomerAdvanceFormValues => ({
  customerId: "",
  amount: null as unknown as number,
  advanceDate: todayIso(),
  paymentMethodId: "",
  destination: "",
  reference: "",
  reason: "",
});

/** Registro de un anticipo recibido, sin factura ni cobro: confirmación directa y atómica. */
export function RegisterCustomerAdvanceModal({
  open,
  onClose,
  onRegistered,
}: {
  open: boolean;
  onClose: () => void;
  onRegistered: (advance: CustomerAdvanceDto) => void;
}) {
  const { t } = useI18n();
  const decimals = usePrecisionDecimals("money");
  const catalogs = useCustomerAdvanceCatalogs(open);
  const form = useForm<RegisterCustomerAdvanceFormValues>({
    resolver: (values, context, options) =>
      zodResolver(
        buildRegisterCustomerAdvanceSchema(
          t,
          todayIso(),
          catalogs.methods.find((m) => m.id === values.paymentMethodId)?.requiresReference ?? false,
        ),
      )(values, context, options),
    defaultValues: emptyValues(),
  });
  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = form;
  const command = useCustomerAdvanceCommand<RegisterCustomerAdvanceFormValues>(
    setError,
    FIELDS,
    ALIASES,
  );
  const { clearError } = command;

  useEffect(() => {
    if (!open) return;
    reset(emptyValues());
    clearError();
    // Solo al abrir: clearError cambia en cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, reset]);

  const close = () => {
    if (!command.saving) onClose();
  };

  const submit = handleSubmit(async (values) => {
    const { destination, reference, reason, ...data } = values;
    const payload = {
      ...data,
      ...splitDestination(destination),
      reference: reference || null,
      reason: reason || null,
    };
    await command.run({
      intent: payload,
      confirm: {
        title: t("finance.customerAdvances.register.title"),
        message: t("finance.customerAdvances.register.confirm", {
          amount: formatMoney(payload.amount, decimals),
          date: formatDate(payload.advanceDate),
          destination: catalogs.destination(payload.companyBankAccountId, payload.cashRegisterId)
            .name,
        }),
        confirmLabel: t("finance.customerAdvances.register.action"),
        variant: "warning",
      },
      send: (clientRequestId) => customerAdvanceService.register({ ...payload, clientRequestId }),
      successMessage: t("finance.customerAdvances.register.success"),
      errorMessage: t("finance.customerAdvances.register.error"),
      onDone: (advance) => {
        onRegistered(advance);
        onClose();
      },
    });
  });

  return (
    <ZHModal
      open={open}
      onClose={close}
      size="md"
      title={t("finance.customerAdvances.register.title")}
      subtitle={t("finance.customerAdvances.register.subtitle")}
      footer={
        <CustomerAdvanceModalFooter
          saving={command.saving}
          actionLabel={t("finance.customerAdvances.register.action")}
          onCancel={close}
          onSubmit={() => void submit()}
        />
      }
    >
      <FormProvider {...form}>
        <ZHGrid cols={2}>
          <ZHField
            className="zh-col-span-2"
            label={t("finance.customerAdvances.field.customer")}
            required
            fieldError={errors.customerId?.message}
          >
            <Controller
              name="customerId"
              control={control}
              render={({ field }) => (
                <CustomerAdvanceCustomerPicker
                  value={field.value || null}
                  disabled={command.saving}
                  onChange={(id) => field.onChange(id ?? "")}
                />
              )}
            />
          </ZHField>

          <ZHField
            label={t("finance.customerAdvances.field.amountReceived")}
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
            label={t("finance.customerAdvances.field.advanceDate")}
            required
            fieldError={errors.advanceDate?.message}
          >
            <ZhDateInput max={todayIso()} disabled={command.saving} {...register("advanceDate")} />
          </ZHField>

          <CustomerAdvanceMoneyFields catalogs={catalogs} disabled={command.saving} />

          <ZHField
            className="zh-col-span-2"
            label={t("finance.customerAdvances.field.reference")}
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
