import { useEffect, useMemo, useState } from "react";
import { FormProvider, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDateInput, ZhDecimalInput, ZhSelect } from "../../../components/zh/inputs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { formatMoney, roundToDecimals } from "../../../lib/sanitizers";
import { formatApiRequestError } from "../../lib/apiError";
import {
  customerAdvanceService,
  type CustomerAdvanceDto,
  type CustomerAdvanceListItemDto,
} from "../api/customerAdvanceService";
import type { SalesReceivableDto } from "../api/receivableService";
import { useCustomerAdvanceCommand } from "../hooks/useCustomerAdvanceCommand";
import { useCustomerReceivables } from "../hooks/useCustomerReceivables";
import {
  buildApplyCustomerAdvanceSchema,
  type ApplicationTarget,
  type ApplyCustomerAdvanceFormValues,
} from "../schemas/customerAdvanceSchema";
import { CustomerAdvanceModalFooter } from "./CustomerAdvanceModalFooter";
import { CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS } from "../utils/customerAdvance";

type AdvanceOption = Pick<
  CustomerAdvanceListItemDto,
  "id" | "advanceDate" | "availableAmount" | "externalReference"
>;

const FIELDS = ["advanceId", "effectiveDate", "lines"] as const;

const pendingOf = (r: SalesReceivableDto | undefined, installmentId: string) => {
  const installment = r?.installments.find((i) => i.id === installmentId);
  return installment
    ? roundToDecimals(installment.amount - installment.paidAmount, 2)
    : (r?.balanceDue ?? 0);
};

/**
 * Aplica un anticipo a una o varias CxC del mismo cliente, en una sola operación. Se abre desde el
 * anticipo (`advance`) o desde una CxC (`receivable`). No mueve dinero ni crea cobros.
 */
export function ApplyCustomerAdvanceModal({
  open,
  advance,
  receivable,
  onClose,
  onApplied,
}: {
  open: boolean;
  advance?: CustomerAdvanceDto;
  receivable?: SalesReceivableDto;
  onClose: () => void;
  onApplied: (advance: CustomerAdvanceDto) => void;
}) {
  const { t } = useI18n();
  const decimals = usePrecisionDecimals("money");
  const customerId = advance?.customerId ?? receivable?.customerId ?? null;
  const { canShow } = usePermissionsUi();
  const canListReceivables = canShow(CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS.receivables);
  const {
    receivables,
    total: receivableTotal,
    loading,
    failed,
    hasMore,
    loadMore,
    reload: reloadReceivables,
  } = useCustomerReceivables(open && canListReceivables ? customerId : null, "pending");
  const [advances, setAdvances] = useState<AdvanceOption[]>([]);
  const [advancesError, setAdvancesError] = useState("");

  const options = useMemo<AdvanceOption[]>(
    () => (advance ? [advance] : advances),
    [advance, advances],
  );
  const choices = useMemo(() => {
    const pending = receivables.filter((r) => r.balanceDue > 0 && r.status !== "cancelled");
    return receivable && !pending.some((r) => r.id === receivable.id)
      ? [receivable, ...pending]
      : pending;
  }, [receivables, receivable]);
  const targets = useMemo(
    () =>
      new Map<string, ApplicationTarget>(
        choices.map((r) => [
          r.id,
          {
            balanceDue: r.balanceDue,
            originDate: r.invoiceId ? r.invoiceIssueDate.slice(0, 10) : null,
            installments: r.installments.map((i) => ({
              id: i.id,
              pending: roundToDecimals(i.amount - i.paidAmount, 2),
            })),
          },
        ]),
      ),
    [choices],
  );

  const emptyLine = () => ({
    receivableId: "",
    installmentId: "",
    amount: null as unknown as number,
  });
  const initialValues = (): ApplyCustomerAdvanceFormValues => ({
    advanceId: advance?.id ?? "",
    effectiveDate: todayIso(),
    lines: [receivable ? { ...emptyLine(), receivableId: receivable.id } : emptyLine()],
  });

  const form = useForm<ApplyCustomerAdvanceFormValues>({
    resolver: (values, context, resolverOptions) => {
      const chosen = options.find((a) => a.id === values.advanceId);
      return zodResolver(
        buildApplyCustomerAdvanceSchema(t, {
          today: todayIso(),
          advanceDate: chosen?.advanceDate ?? null,
          available: chosen?.availableAmount ?? 0,
          decimals,
          targets,
        }),
      )(values, context, resolverOptions);
    },
    defaultValues: initialValues(),
  });
  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    setError,
    formState: { errors },
  } = form;
  const lines = useFieldArray({ control, name: "lines" });
  const command = useCustomerAdvanceCommand<ApplyCustomerAdvanceFormValues>(setError, FIELDS);
  const { clearError } = command;

  useEffect(() => {
    if (!open) return;
    reset(initialValues());
    clearError();
    // Solo al abrir o cambiar de origen.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, advance?.id, receivable?.id, reset]);

  useEffect(() => {
    if (!open || advance || !customerId) return;
    let active = true;
    setAdvancesError("");
    customerAdvanceService
      .list(1, 100, { customerId, status: "Confirmed" })
      .then((r) => active && setAdvances(r.items.filter((a) => a.availableAmount > 0)))
      .catch((err: unknown) => {
        if (active)
          setAdvancesError(
            formatApiRequestError(err, {
              generic: t("finance.customerAdvances.apply.loadError"),
            }),
          );
      });
    return () => {
      active = false;
    };
  }, [open, advance, customerId, t]);

  const watched = watch("lines");
  const chosen = options.find((a) => a.id === watch("advanceId"));
  const total = roundToDecimals(
    watched.reduce((sum, line) => sum + (Number(line.amount) || 0), 0),
    2,
  );
  const available = chosen?.availableAmount ?? 0;

  const close = () => {
    if (!command.saving) onClose();
  };

  const submit = handleSubmit(async (values) => {
    const payload = {
      effectiveDate: values.effectiveDate,
      lines: values.lines.map((line) => ({
        receivableId: line.receivableId,
        installmentId: line.installmentId || null,
        amount: line.amount,
      })),
    };
    await command.run({
      intent: { advanceId: values.advanceId, ...payload },
      confirm: {
        title: t("finance.customerAdvances.apply.title"),
        message: t("finance.customerAdvances.apply.confirm", {
          amount: formatMoney(total, decimals),
          count: payload.lines.length,
          date: formatDate(payload.effectiveDate),
          available: formatMoney(available, decimals),
          resulting: formatMoney(roundToDecimals(available - total, 2), decimals),
        }),
        confirmLabel: t("finance.customerAdvances.apply.action"),
        variant: "warning",
      },
      send: (clientRequestId) =>
        customerAdvanceService.apply(values.advanceId, { ...payload, clientRequestId }),
      successMessage: t("finance.customerAdvances.apply.success"),
      errorMessage: t("finance.customerAdvances.apply.error"),
      onDone: (updated) => {
        reloadReceivables();
        onApplied(updated);
        onClose();
      },
    });
  });

  const label = (r: SalesReceivableDto) =>
    `${r.invoiceNumber} — ${t("finance.customerAdvances.field.pending")} ${formatMoney(r.balanceDue, decimals)}`;

  return (
    <ZHModal
      open={open}
      onClose={close}
      size="lg"
      title={t("finance.customerAdvances.apply.title")}
      subtitle={advance?.customerName ?? receivable?.customerName}
      footer={
        <CustomerAdvanceModalFooter
          saving={command.saving}
          actionLabel={t("finance.customerAdvances.apply.action")}
          onCancel={close}
          onSubmit={() => void submit()}
        />
      }
    >
      <FormProvider {...form}>
        <ZHGrid cols={1}>
          <p>{t("finance.customerAdvances.apply.explanation")}</p>
          {!canListReceivables ? (
            <ZHPageNotice
              variant="warning"
              message={t("finance.customerAdvances.access.receivables")}
            />
          ) : null}
          {canListReceivables && !loading && !failed && choices.length === 0 ? (
            <ZHPageNotice
              variant="info"
              message={t("finance.customerAdvances.apply.noReceivables")}
            />
          ) : null}
          {failed ? (
            <ZHPageNotice
              variant="error"
              message={t("finance.customerAdvances.apply.receivablesError")}
            />
          ) : null}
          {advancesError ? <ZHPageNotice variant="error" message={advancesError} /> : null}

          <ZHGrid cols={2}>
            <ZHField
              label={t("finance.customerAdvances.field.advance")}
              required
              fieldError={errors.advanceId?.message}
              hint={
                !advance && options.length === 0
                  ? t("finance.customerAdvances.apply.noAdvances")
                  : null
              }
            >
              <ZhSelect
                className="zh-input"
                disabled={command.saving || !!advance}
                {...register("advanceId")}
              >
                {advance ? null : (
                  <option value="">{t("finance.customerAdvances.select.advance")}</option>
                )}
                {options.map((a) => (
                  <option key={a.id} value={a.id}>
                    {formatDate(a.advanceDate)} — {formatMoney(a.availableAmount, decimals)}
                    {a.externalReference ? ` — ${a.externalReference}` : ""}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField
              label={t("finance.customerAdvances.field.effectiveDate")}
              required
              fieldError={errors.effectiveDate?.message}
            >
              <ZhDateInput
                min={chosen?.advanceDate}
                max={todayIso()}
                disabled={command.saving}
                {...register("effectiveDate")}
              />
            </ZHField>
          </ZHGrid>

          {lines.fields.map((field, index) => {
            const selected = choices.find((r) => r.id === watched[index]?.receivableId);
            const lineErrors = errors.lines?.[index];
            return (
              <ZHGrid cols={3} key={field.id}>
                <ZHField
                  label={t("finance.customerAdvances.field.receivable")}
                  required
                  fieldError={lineErrors?.receivableId?.message}
                  hint={
                    selected
                      ? `${t("finance.customerAdvances.field.pending")} ${formatMoney(selected.balanceDue, decimals)}`
                      : null
                  }
                >
                  <ZhSelect
                    className="zh-input"
                    disabled={command.saving || loading}
                    {...register(`lines.${index}.receivableId`, {
                      onChange: () => setValue(`lines.${index}.installmentId`, ""),
                    })}
                  >
                    <option value="">
                      {loading
                        ? t("common.loading")
                        : t("finance.customerAdvances.select.receivable")}
                    </option>
                    {choices.map((r) => (
                      <option key={r.id} value={r.id}>
                        {label(r)}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>

                <ZHField label={t("finance.customerAdvances.field.installment")}>
                  <ZhSelect
                    className="zh-input"
                    disabled={command.saving || !selected}
                    {...register(`lines.${index}.installmentId`)}
                  >
                    <option value="">{t("finance.customerAdvances.select.anyInstallment")}</option>
                    {selected?.installments
                      .filter((i) => i.paidAmount < i.amount)
                      .map((i) => (
                        <option key={i.id} value={i.id}>
                          #{i.installmentNumber} —{" "}
                          {formatMoney(pendingOf(selected, i.id), decimals)}
                        </option>
                      ))}
                  </ZhSelect>
                </ZHField>

                <ZHField
                  label={t("finance.customerAdvances.field.amountToApply")}
                  required
                  fieldError={lineErrors?.amount?.message}
                >
                  <ZhDecimalInput
                    precision="money"
                    positiveOnly
                    disabled={command.saving}
                    {...register(`lines.${index}.amount`)}
                  />
                  {lines.fields.length > 1 ? (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={command.saving}
                      onClick={() => lines.remove(index)}
                    >
                      {t("finance.customerAdvances.apply.removeLine")}
                    </ZHBtn>
                  ) : null}
                </ZHField>
              </ZHGrid>
            );
          })}

          <div>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              disabled={command.saving}
              onClick={() => lines.append(emptyLine())}
            >
              {t("finance.customerAdvances.apply.addLine")}
            </ZHBtn>
            {hasMore ? (
              <ZHBtn type="button" variant="ghost" size="sm" disabled={loading} onClick={loadMore}>
                {t("finance.customerAdvances.apply.loadMore", {
                  loaded: receivables.length,
                  total: receivableTotal,
                })}
              </ZHBtn>
            ) : null}
          </div>

          <div>
            <ZHInfoRow
              label={t("finance.customerAdvances.field.available")}
              value={<ZHMoneyValue precision="money" value={available} />}
            />
            <ZHInfoRow
              label={t("finance.customerAdvances.field.totalToApply")}
              value={<ZHMoneyValue precision="money" value={total} />}
            />
            <ZHInfoRow
              label={t("finance.customerAdvances.field.resulting")}
              value={
                <ZHMoneyValue precision="money" value={roundToDecimals(available - total, 2)} />
              }
            />
          </div>

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
