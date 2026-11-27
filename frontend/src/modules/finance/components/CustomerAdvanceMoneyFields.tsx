import { useFormContext } from "react-hook-form";
import { ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type { CustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";

/**
 * Medio de pago y destino, compartidos por registro y devolución: el medio elegido decide si el
 * destino es una caja (efectivo) o una cuenta bancaria (transferencia), así que son excluyentes.
 */
export function CustomerAdvanceMoneyFields({
  catalogs,
  disabled,
}: {
  catalogs: CustomerAdvanceCatalogs;
  disabled: boolean;
}) {
  const { t } = useI18n();
  const {
    register,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<{ paymentMethodId: string; destination: string }>();
  const method = catalogs.advanceMethods.find((m) => m.id === watch("paymentMethodId"));
  const catalog = method?.affectsPhysicalCash ? "cashRegisters" : "bankAccounts";
  const access = !method
    ? "ok"
    : method.affectsPhysicalCash
      ? catalogs.registerAccess
      : catalogs.bankAccess;

  return (
    <>
      <ZHField
        label={t("finance.customerAdvances.field.paymentMethod")}
        required
        fieldError={errors.paymentMethodId?.message}
      >
        <ZhSelect
          className="zh-input"
          disabled={disabled}
          {...register("paymentMethodId", { onChange: () => setValue("destination", "") })}
        >
          <option value="">{t("finance.customerAdvances.select.paymentMethod")}</option>
          {catalogs.advanceMethods.map((m) => (
            <option key={m.id} value={m.id}>
              {m.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>

      <ZHField
        label={t(
          !method
            ? "finance.customerAdvances.field.destination"
            : method.affectsPhysicalCash
              ? "finance.customerAdvances.field.cashRegister"
              : "finance.customerAdvances.field.bankAccount",
        )}
        required
        fieldError={errors.destination?.message}
        hint={method ? null : t("finance.customerAdvances.hint.destinationNeedsMethod")}
      >
        <ZhSelect
          className="zh-input"
          disabled={disabled || !method || access !== "ok"}
          {...register("destination")}
        >
          <option value="">{t("finance.customerAdvances.select.destination")}</option>
          {method?.affectsPhysicalCash
            ? catalogs.cashRegisters
                .filter((c) => c.isActive)
                .map((c) => (
                  <option key={c.id} value={`cash:${c.id}`}>
                    {c.name}
                  </option>
                ))
            : method &&
              catalogs.bankAccounts
                .filter((b) => b.isActive)
                .map((b) => (
                  <option key={b.id} value={`bank:${b.id}`}>
                    {b.displayName}
                  </option>
                ))}
        </ZhSelect>
      </ZHField>

      {access !== "ok" ? (
        <div className="zh-col-span-2">
          <ZHPageNotice
            variant="warning"
            message={t(
              access === "forbidden"
                ? `finance.customerAdvances.access.${catalog}`
                : `finance.customerAdvances.loadError.${catalog}`,
            )}
          />
        </div>
      ) : null}
    </>
  );
}
