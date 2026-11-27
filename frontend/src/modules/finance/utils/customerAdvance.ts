import type { BadgeVariant } from "../../../components/PageShell";
import type {
  CustomerAdvanceDto,
  CustomerAdvanceMovementType,
  CustomerAdvanceStatus,
} from "../api/customerAdvanceService";

export const CUSTOMER_ADVANCES_ROUTE = "/finance/customer-advances";

export const CUSTOMER_ADVANCE_PERMISSIONS = {
  create: "finance.customer-advances.create",
  apply: "finance.customer-advances.apply",
  reverse: "finance.customer-advances.reverse",
  refund: "finance.customer-advances.refund",
} as const;

/** Permisos que exigen las API de cada selector; Anticipos no los concede ni los sustituye. */
export const CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS = {
  customers: "masterdata.businesspartners.view",
  bankAccounts: "treasury.banks.accounts.view",
  cashRegisters: "caja.view",
  receivables: "sales.view",
} as const;

export const advanceStatusKey = (status: CustomerAdvanceStatus) =>
  `finance.customerAdvances.status.${status}`;

export const advanceStatusVariant = (status: CustomerAdvanceStatus): BadgeVariant =>
  status === "Confirmed" ? "green" : "gray";

export const movementTypeKey = (type: CustomerAdvanceMovementType) =>
  `finance.customerAdvances.movement.${type}`;

const MOVEMENT_VARIANT: Record<CustomerAdvanceMovementType, BadgeVariant> = {
  Received: "green",
  Application: "blue",
  ReversalOfApplication: "orange",
  Refund: "red",
  ReversalOfRefund: "orange",
  IncomeReversed: "gray",
  SourceCancelled: "gray",
};

export const movementTypeVariant = (type: CustomerAdvanceMovementType) => MOVEMENT_VARIANT[type];

const BALANCE_INCREASES: readonly CustomerAdvanceMovementType[] = [
  "Received",
  "ReversalOfApplication",
  "ReversalOfRefund",
];

/** Sentido del movimiento sobre el saldo del anticipo (mismo criterio que el dominio). */
export const movementBalanceSign = (type: CustomerAdvanceMovementType): 1 | -1 =>
  BALANCE_INCREASES.includes(type) ? 1 : -1;

/** Sin aplicaciones ni devoluciones vigentes: condición del servidor para anular o revertir el ingreso. */
export const isIntactAdvance = (advance: CustomerAdvanceDto) =>
  advance.status === "Confirmed" && advance.availableAmount === advance.originalAmount;

/** Separa el destino codificado en el formulario ("bank:<id>" | "cash:<id>"). */
export function splitDestination(destination: string) {
  return {
    companyBankAccountId: destination.startsWith("bank:") ? destination.slice(5) : null,
    cashRegisterId: destination.startsWith("cash:") ? destination.slice(5) : null,
  };
}

export const joinDestination = (bankAccountId: string | null, cashRegisterId: string | null) =>
  cashRegisterId ? `cash:${cashRegisterId}` : bankAccountId ? `bank:${bankAccountId}` : "";
