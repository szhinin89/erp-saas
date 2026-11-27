import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
import { bankAccountService, type CompanyBankAccountDto } from "../api/bankAccountService";
import {
  cashRegisterLookupFacade,
  type CashRegisterDto,
} from "../../caja/facades/cashRegisterLookupFacade";
import {
  paymentMethodLookupFacade,
  type PaymentMethodDto,
} from "../../sales/facades/paymentMethodLookupFacade";
import {
  sessionBranchFacade,
  type SessionBranchDto,
} from "../../session/facades/sessionBranchFacade";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS } from "../utils/customerAdvance";

/** Estado de un catálogo: sin permiso o con fallo nunca se presenta como «sin datos». */
export type CatalogAccess = "ok" | "forbidden" | "error";

const accessOf = (err: unknown): CatalogAccess =>
  isAxiosError(err) && err.response?.status === 403 ? "forbidden" : "error";

/**
 * Catálogos oficiales que usan las pantallas de Anticipos: medios (incluye inactivos para poder
 * nombrar el histórico), cuentas bancarias, cajas y sucursales autorizadas. Cuentas y cajas solo
 * se consultan con su permiso; el servidor sigue siendo la autoridad.
 */
export function useCustomerAdvanceCatalogs(enabled = true) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canSeeBanks = canShow(CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS.bankAccounts);
  const canSeeRegisters = canShow(CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS.cashRegisters);
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [bankAccounts, setBankAccounts] = useState<CompanyBankAccountDto[]>([]);
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const [branches, setBranches] = useState<SessionBranchDto[]>([]);
  const [bankFailure, setBankFailure] = useState<CatalogAccess>("ok");
  const [registerFailure, setRegisterFailure] = useState<CatalogAccess>("ok");

  useEffect(() => {
    if (!enabled) return;
    let active = true;
    const load = <T>(
      request: Promise<T>,
      apply: (value: T) => void,
      fail?: (access: CatalogAccess) => void,
    ) => {
      request
        .then((value) => active && apply(value))
        .catch((err: unknown) => active && fail?.(accessOf(err)));
    };
    setBankFailure("ok");
    setRegisterFailure("ok");
    load(paymentMethodLookupFacade.list(false), setMethods);
    if (canSeeBanks) load(bankAccountService.list(), setBankAccounts, setBankFailure);
    if (canSeeRegisters)
      load(cashRegisterLookupFacade.getCashRegisters(false), setCashRegisters, setRegisterFailure);
    load(sessionBranchFacade.listAvailable(), (dto) => setBranches(dto.branches));
    return () => {
      active = false;
    };
  }, [enabled, canSeeBanks, canSeeRegisters]);

  const bankAccess: CatalogAccess = canSeeBanks ? bankFailure : "forbidden";
  const registerAccess: CatalogAccess = canSeeRegisters ? registerFailure : "forbidden";

  return {
    methods,
    /** TA-02: solo efectivo y transferencia bancaria. */
    advanceMethods: methods.filter(
      (m) =>
        m.isActive && !m.isCreditAllowed && (m.affectsPhysicalCash || m.detailType === "Transfer"),
    ),
    bankAccounts,
    cashRegisters,
    bankAccess,
    registerAccess,
    branches,
    methodName: (id: string) => methods.find((m) => m.id === id)?.name ?? "—",
    branchName: (id: string) => branches.find((b) => b.id === id)?.name ?? "—",
    destination: (bankAccountId: string | null, cashRegisterId: string | null) => {
      const isCash = cashRegisterId !== null;
      const name = isCash
        ? cashRegisters.find((c) => c.id === cashRegisterId)?.name
        : bankAccounts.find((b) => b.id === bankAccountId)?.displayName;
      const hidden = (isCash ? registerAccess : bankAccess) === "forbidden";
      return {
        isCash,
        name: name ?? (hidden ? t("finance.customerAdvances.access.destinationHidden") : "—"),
      };
    },
  };
}

export type CustomerAdvanceCatalogs = ReturnType<typeof useCustomerAdvanceCatalogs>;
