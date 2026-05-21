import type { CommercialPlanAdmin } from '../../modules/superadmin/api/superAdminService';

export const POLL_MS = 20_000;

export type PlanVisualTier = 'starter' | 'business' | 'professional' | 'enterprise' | 'default';

/** Tema visual por código de plan (alineado a tarjetas comerciales tipo pricing). */
export function planVisualTier(code: string): PlanVisualTier {
  const c = (code ?? '').trim().toLowerCase();
  if (c === 'starter') return 'starter';
  if (c === 'business') return 'business';
  if (c === 'professional') return 'professional';
  if (c === 'enterprise') return 'enterprise';
  return 'default';
}

/** Precio mensual equivalente para estimar MRR (planes con ciclo distinto a mensual). */
export function monthlyEquivalentPrice(plan: CommercialPlanAdmin): number {
  const c = (plan.billingCycle ?? 'monthly').toLowerCase();
  if (c === 'yearly') return plan.priceAmount / 12;
  if (c === 'quarterly') return plan.priceAmount / 3;
  if (c === 'one_time') return 0;
  return plan.priceAmount;
}

export function csvEscape(cell: string): string {
  const s = String(cell ?? '');
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export type PlanFormState = {
  code: string;
  name: string;
  shortLabel: string;
  isActive: boolean;
  priceAmount: string;
  currency: string;
  billingCycle: string;
  isPubliclyVisible: boolean;
  isRecommended: boolean;
  sortOrder: string;
  externalBillingRef: string;
};

export function emptyPlanForm(): PlanFormState {
  return {
    code: '',
    name: '',
    shortLabel: '',
    isActive: true,
    priceAmount: '0',
    currency: 'USD',
    billingCycle: 'monthly',
    isPubliclyVisible: true,
    isRecommended: false,
    sortOrder: '0',
    externalBillingRef: '',
  };
}

export function planToForm(p: CommercialPlanAdmin): PlanFormState {
  return {
    code: p.code,
    name: p.name,
    shortLabel: p.shortLabel ?? '',
    isActive: p.isActive,
    priceAmount: String(p.priceAmount),
    currency: p.currency,
    billingCycle: p.billingCycle,
    isPubliclyVisible: p.isPubliclyVisible,
    isRecommended: p.isRecommended,
    sortOrder: String(p.sortOrder),
    externalBillingRef: p.externalBillingRef ?? '',
  };
}

export function formatPlanMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency || 'USD',
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}
