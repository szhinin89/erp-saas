import type { FormEventHandler } from "react";
import type { FieldErrors, UseFormRegister } from "react-hook-form";
import { useI18n } from "../../../i18n/i18n";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput } from "../../../components/zh/inputs";
import type { CompanyBankAccountDto } from "../../finance/facades/bankAccountLookupFacade";
import type { BankDepositFormValues } from "../schemas/cajaSchema";

type Props = {
  open: boolean;
  saving: boolean;
  saveError: string;
  register: UseFormRegister<BankDepositFormValues>;
  errors: FieldErrors<BankDepositFormValues>;
  bankAccounts: CompanyBankAccountDto[];
  bankAccountsLoading: boolean;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onClose: () => void;
};

/**
 * ACCOUNTING-12C — formulario del depósito bancario de caja. Puramente presentacional, mismo markup y
 * componentes DS que `ManualCashMovementModal` (ZHModal/ZHField/ZhDecimalInput/ZHBtn/ZHPageNotice y
 * las clases `cj-movement-*` ya existentes); toda la lógica vive en `useCashBankDepositFlow`.
 */
export function CashBankDepositModal({
  open,
  saving,
  saveError,
  register,
  errors,
  bankAccounts,
  bankAccountsLoading,
  onSubmit,
  onClose,
}: Props) {
  const { t } = useI18n();

  return (
    <ZHModal
      closeLabel={t("common.close")}
      open={open}
      onClose={onClose}
      size="md"
      title={t("caja.bankDeposit.title")}
      closeOnBackdrop={!saving}
    >
      <form onSubmit={onSubmit} className="cj-movement-form">
        <ZHField
          density="compact"
          className="cj-movement-field--type"
          label={t("caja.bankDeposit.bankAccount")}
          required
          fieldError={errors.companyBankAccountId?.message}
        >
          <select {...register("companyBankAccountId")} disabled={bankAccountsLoading || bankAccounts.length === 0}>
            <option value="">
              {!bankAccountsLoading && bankAccounts.length === 0
                ? t("caja.bankDeposit.bankAccountsEmpty")
                : t("caja.movements.form.selectPlaceholder")}
            </option>
            {bankAccounts.map((b) => (
              <option key={b.id} value={b.id}>
                {b.displayName} — {b.accountNumber}
              </option>
            ))}
          </select>
        </ZHField>
        <ZHField
          density="compact"
          className="cj-movement-field--amount"
          label={t("caja.bankDeposit.amount")}
          required
          fieldError={errors.amount?.message}
        >
          <ZhDecimalInput {...register("amount")} precision="money" positiveOnly />
        </ZHField>
        <ZHField
          density="compact"
          className="cj-movement-field--reason"
          label={t("caja.bankDeposit.reference")}
          fieldError={errors.referenceNumber?.message}
        >
          <input type="text" {...register("referenceNumber")} />
        </ZHField>
        <ZHField
          density="compact"
          className="cj-movement-field--desc"
          label={t("caja.bankDeposit.description")}
          required
          fieldError={errors.description?.message}
        >
          <input type="text" {...register("description")} />
        </ZHField>
        {saveError && (
          <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={saveError} />
        )}
        <div className="cj-actions">
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? t("common.saving") : t("caja.bankDeposit.submit")}
          </ZHBtn>
          <ZHBtn variant="secondary" type="button" disabled={saving} onClick={onClose}>
            {t("common.cancel")}
          </ZHBtn>
        </div>
      </form>
    </ZHModal>
  );
}
