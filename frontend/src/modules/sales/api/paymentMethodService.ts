import { apiGet, apiPost, apiPut } from "../../lib/apiEnvelope";

/** Esquema de detalle que la UI debe capturar al registrar un pago — viene del catálogo, nunca se infiere del código. */
export type PaymentMethodDetailType = "None" | "Card" | "Transfer" | "Check";

/**
 * SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — de dónde sale la cuenta contable de este método.
 * Fuente única por método: nunca hay una segunda configuración posible para el mismo método.
 */
export type PaymentMethodAccountSource =
  | "CashRegister" // Efectivo — según la caja registradora de la venta.
  | "CompanyBankAccount" // Transferencia — según la cuenta bancaria elegida.
  | "PaymentMethodAccount" // Tarjeta/Cheque — única configuración manual posible (accountingAccountId).
  | "AccountingRule"; // Crédito — regla de Cuentas por Cobrar, resuelta al momento del cobro.

export type PaymentMethodDto = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  requiresReference: boolean;
  isCreditAllowed: boolean;
  sortOrder: number;
  detailType: PaymentMethodDetailType;
  /** ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — SSOT backend de "mueve efectivo físico":
   * true ⇒ el destino de un pago es una caja; false ⇒ una cuenta bancaria. Nunca inferir por code. */
  affectsPhysicalCash: boolean;
  /** SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: código del catálogo sri_payment_method mapeado a
   * esta forma de cobro (null = sin mapeo propio, la emisión cae al default de empresa). */
  sriPaymentMethodCode: string | null;
  /** SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — fuente de la cuenta contable de este método. */
  accountSource: PaymentMethodAccountSource;
  /** Cuenta contable configurada — solo tiene valor cuando accountSource es
   * "PaymentMethodAccount" (Tarjeta/Cheque). Para Efectivo/Transferencia/Crédito siempre es
   * null: su cuenta no se configura aquí. */
  accountingAccountId: string | null;
};

export type CreatePaymentMethodPayload = {
  code: string;
  name: string;
  requiresReference?: boolean;
  isCreditAllowed?: boolean;
  sortOrder?: number;
  detailType?: PaymentMethodDetailType;
  sriPaymentMethodCode?: string | null;
};

export type UpdatePaymentMethodPayload = {
  id: string;
  name: string;
  requiresReference: boolean;
  isCreditAllowed: boolean;
  sortOrder: number;
  detailType: PaymentMethodDetailType;
  sriPaymentMethodCode?: string | null;
};

const BASE = "/api/v1/payment-methods";

export const paymentMethodService = {
  list: (onlyActive = false) =>
    apiGet<PaymentMethodDto[]>(`${BASE}?onlyActive=${onlyActive}`),

  getById: (id: string) => apiGet<PaymentMethodDto>(`${BASE}/${id}`),

  create: (body: CreatePaymentMethodPayload) =>
    apiPost<PaymentMethodDto>(BASE, body),

  update: (id: string, body: UpdatePaymentMethodPayload) =>
    apiPut<PaymentMethodDto>(`${BASE}/${id}`, body),

  toggle: (id: string) => apiPost<PaymentMethodDto>(`${BASE}/${id}/toggle`, {}),

  /** SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 */
  setAccount: (id: string, accountingAccountId: string) =>
    apiPut<PaymentMethodDto>(`${BASE}/${id}/account`, { accountingAccountId }),
};
