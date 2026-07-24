import { apiGet, apiPut } from '../../modules/lib/apiEnvelope';

export type DecimalConfig = {
  salesUnitPrice: number;
  purchaseUnitPrice: number;
  quantity: number;
  percentage: number;
  totalAmount: number;
};

const DEFAULTS: DecimalConfig = {
  salesUnitPrice: 2,
  purchaseUnitPrice: 4,
  quantity: 4,
  percentage: 2,
  totalAmount: 2,
};

let _cache: DecimalConfig | null = null;

export async function loadDecimalConfig(): Promise<DecimalConfig> {
  try {
    const cfg = await apiGet<DecimalConfig>('/api/v1/config/decimals');
    _cache = cfg;
    return cfg;
  } catch {
    _cache = DEFAULTS;
    return DEFAULTS;
  }
}

export function getDecimalConfig(): DecimalConfig {
  return _cache ?? DEFAULTS;
}

export async function saveDecimalConfig(cfg: DecimalConfig): Promise<DecimalConfig> {
  const result = await apiPut<DecimalConfig>('/api/v1/config/decimals', cfg);
  _cache = result;
  return result;
}

export const decimalDefaults = DEFAULTS;
