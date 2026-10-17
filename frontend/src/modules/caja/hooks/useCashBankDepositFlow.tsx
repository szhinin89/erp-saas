import { useCallback, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { isAxiosError } from "axios";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import {
  bankDepositSchema,
  emptyBankDepositForm,
  type BankDepositFormValues,
} from "../schemas/cajaSchema";
import {
  bankAccountLookupFacade,
  type CompanyBankAccountDto,
} from "../../finance/facades/bankAccountLookupFacade";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useClientRequestId } from "../../../lib/idempotency/useClientRequestId";

export type UseCashBankDepositFlowArgs = {
  /** Sesión que se está viendo — la resuelve el llamador, nunca este hook. */
  cashSessionId: string | null | undefined;
  /** true solo si esa sesión está abierta (decisión del llamador). */
  isSessionOpen: boolean;
  /** Refresco propio de cada pantalla tras un depósito exitoso. */
  onRecorded?: () => void | Promise<void>;
};

/**
 * ACCOUNTING-12C — depósito del efectivo de la caja en una cuenta bancaria de la empresa. Mismo
 * patrón que `useManualCashMovementFlow` (confirmación previa, ClientRequestId estable en
 * reintentos, applyServerErrors + toast con el mensaje real del backend), pero con su propia
 * operación (`BankDeposit`): la cuenta bancaria destino se elige explícitamente, nunca se deduce de
 * un motivo de Retiro. Permiso: `caja.record` (el mismo del backend).
 */
export function useCashBankDepositFlow({ cashSessionId, isSessionOpen, onRecorded }: UseCashBankDepositFlowArgs) {
  const moneyDecimals = usePrecisionDecimals("money");
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canRecordBankDeposit = canShow("caja.record");
  // La lista de cuentas destino exige además `treasury.banks.accounts.view` (mismo endpoint que
  // Tesorería): sin él se explica la causa en vez de mostrar un 403 genérico.
  const canViewBankAccounts = canShow("treasury.banks.accounts.view");

  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [bankAccounts, setBankAccounts] = useState<CompanyBankAccountDto[]>([]);
  const [bankAccountsLoading, setBankAccountsLoading] = useState(false);
  const submittingRef = useRef(false);
  const clientRequest = useClientRequestId();

  const form = useForm<BankDepositFormValues>({
    resolver: zodResolver(bankDepositSchema(t)),
    defaultValues: emptyBankDepositForm(),
    mode: "onBlur",
  });

  const openModal = useCallback(() => {
    if (!canRecordBankDeposit || !isSessionOpen || !cashSessionId) return;
    form.reset(emptyBankDepositForm(t("caja.bankDeposit.defaultDescription")));
    setSaveError("");
    setOpen(true);
    if (!canViewBankAccounts) {
      setBankAccounts([]);
      setSaveError(t("caja.bankDeposit.bankAccountsForbidden"));
      return;
    }
    setBankAccountsLoading(true);
    bankAccountLookupFacade
      .list(true)
      .then((rows) => setBankAccounts(rows ?? []))
      .catch((err: unknown) => {
        setBankAccounts([]);
        setSaveError(
          isAxiosError(err) && err.response?.status === 403
            ? t("caja.bankDeposit.bankAccountsForbidden")
            : formatApiRequestError(err, { unauthorized: t("caja.session.unauthorized"), generic: t("caja.bankDeposit.bankAccountsError") }),
        );
      })
      .finally(() => setBankAccountsLoading(false));
  }, [canRecordBankDeposit, canViewBankAccounts, isSessionOpen, cashSessionId, form, t]);

  const closeModal = useCallback(() => {
    if (saving) return;
    setSaveError("");
    setOpen(false);
  }, [saving]);

  const confirmAndRecord = async (sessionId: string, data: BankDepositFormValues) => {
    const bankLabel = bankAccounts.find((b) => b.id === data.companyBankAccountId)?.displayName ?? "";
    const confirmed = await message.confirm({
      title: t("caja.bankDeposit.confirmTitle"),
      message: (
        <p className="zh-confirm-message">
          {t("caja.bankDeposit.bankAccount")}: <strong>{bankLabel}</strong>
          <br />
          {t("caja.bankDeposit.amount")}: <strong>{formatMoneyWithSymbol(data.amount, moneyDecimals)}</strong>
        </p>
      ),
      variant: "warning",
      confirmLabel: t("caja.bankDeposit.submit"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      const deposit = {
        companyBankAccountId: data.companyBankAccountId,
        amount: data.amount,
        description: data.description,
        referenceNumber: data.referenceNumber.trim() || undefined,
      };
      await cajaService.recordBankDeposit(sessionId, {
        ...deposit,
        clientRequestId: clientRequest.idFor({ sessionId, ...deposit }),
      });
      clientRequest.complete();
      setOpen(false);
      await onRecorded?.();
      message.success(t("caja.bankDeposit.success"));
    } catch (err: unknown) {
      applyServerErrors(err, form.setError, () => {});
      const errorMessage = formatApiRequestError(err, {
        unauthorized: t("caja.session.unauthorized"),
        generic: t("caja.bankDeposit.error"),
      });
      setSaveError(errorMessage);
      message.error(errorMessage);
    }
    setSaving(false);
  };

  const handleSubmit = form.handleSubmit(async (data) => {
    if (!cashSessionId || saving || submittingRef.current) return;
    submittingRef.current = true;
    try {
      await confirmAndRecord(cashSessionId, data);
    } finally {
      submittingRef.current = false;
    }
  });

  return {
    canRecordBankDeposit,
    open,
    saving,
    saveError,
    form,
    bankAccounts,
    bankAccountsLoading,
    openModal,
    closeModal,
    handleSubmit,
  };
}
