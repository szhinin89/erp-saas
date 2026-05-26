export const decimalConfig = {
  sales: 2,
  purchases: 4,
  inventory: 6,
  accounting: 2,
  percentage: 2,
  taxRate: 4,
  exchangeRate: 6,
} as const;

export type DecimalConfigKey = keyof typeof decimalConfig;
