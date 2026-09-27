import type { BadgeVariant } from "../../../components/PageShell";
import type { CashFundingRequestStatus } from "../api/cashFundingRequestService";

/** ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — etiqueta legible de CashFundingRequestStatus (backend). */
const STATUS_LABEL: Record<CashFundingRequestStatus, string> = {
  Pending: "Pendiente",
  Fulfilled: "Entregada",
  Rejected: "Rechazada",
  Cancelled: "Cancelada",
};

const STATUS_BADGE: Record<CashFundingRequestStatus, BadgeVariant> = {
  Pending: "warning",
  Fulfilled: "success",
  Rejected: "error",
  Cancelled: "neutral",
};

export const CASH_FUNDING_REQUEST_STATUSES: CashFundingRequestStatus[] = [
  "Pending",
  "Fulfilled",
  "Rejected",
  "Cancelled",
];

export const cashFundingRequestStatusLabel = (status: string): string =>
  STATUS_LABEL[status as CashFundingRequestStatus] ?? status;

export const cashFundingRequestStatusBadge = (status: string): BadgeVariant =>
  STATUS_BADGE[status as CashFundingRequestStatus] ?? "neutral";
