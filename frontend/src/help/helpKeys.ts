/** SSOT de claves de ayuda contextual. Las pantallas consumen ayuda solo por clave, nunca con texto hardcodeado. */
export const HELP_KEYS = {
  SALES_CUSTOMER_CONSUMER_FINAL: "sales.customer.consumerFinal",
  SALES_EMISSION_SECTION: "sales.emission.section",
  SALES_EMISSION_TYPE: "sales.emission.type",
  SALES_CASH_SESSION: "sales.cashSession",
  SALES_PAYMENTS_SECTION: "sales.payments.section",
  SALES_PAYMENTS_CASH_RECEIVED: "sales.payments.cashReceived",
  SALES_PAYMENTS_CHANGE: "sales.payments.change",
  SALES_CHECKLIST: "sales.checklist",
  SALES_PRODUCT_SEARCH: "sales.productSearch",
  SALES_STOCK: "sales.stock",
  SALES_PRICING: "sales.pricing",
} as const;

export type HelpKeyId = (typeof HELP_KEYS)[keyof typeof HELP_KEYS];
