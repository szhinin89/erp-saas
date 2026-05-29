/**
 * Subscription status constants — single source of truth for the frontend.
 * These must match the backend enums exactly:
 *   - BillingAccountStatus (BillingEnums.cs)
 *   - SubscriberLifecycleStatus (Subscriber.cs)
 *   - SaasBillingInvoiceStatus (BillingEnums.cs)
 *   - SubscriptionAccessMode (SubscriptionAccessMode.cs)
 */

// ── Billing Account Status (BillingAccountStatus enum) ───────────────────────
export const BillingAccountStatus = {
  Trialing:    'Trialing',
  Active:      'Active',
  PastDue:     'PastDue',
  GracePeriod: 'GracePeriod',
  Suspended:   'Suspended',
  Cancelled:   'Cancelled',
} as const;
export type BillingAccountStatus = typeof BillingAccountStatus[keyof typeof BillingAccountStatus];

// ── Lifecycle Status (SubscriberLifecycleStatus enum) ────────────────────────
export const LifecycleStatus = {
  Active:      'Active',
  Trial:       'Trial',
  GracePeriod: 'GracePeriod',
  Suspended:   'Suspended',
  Inactive:    'Inactive',
} as const;
export type LifecycleStatus = typeof LifecycleStatus[keyof typeof LifecycleStatus];

// ── Invoice Status (SaasBillingInvoiceStatus enum) ───────────────────────────
export const InvoiceStatus = {
  Draft:         'Draft',
  Open:          'Open',
  Paid:          'Paid',
  Void:          'Void',
  Uncollectible: 'Uncollectible',
} as const;
export type InvoiceStatus = typeof InvoiceStatus[keyof typeof InvoiceStatus];

// ── Access Mode (SubscriptionAccessMode enum) ─────────────────────────────────
export const AccessMode = {
  FullAccess: 'FullAccess',
  ReadOnly:   'ReadOnly',
  Blocked:    'Blocked',
} as const;
export type AccessMode = typeof AccessMode[keyof typeof AccessMode];

// ── Denial Codes (SubscriptionAccessDenialReason enum) ───────────────────────
export const DenialCode = {
  Suspended:          'subscription_suspended',
  Inactive:           'subscription_inactive',
  Cancelled:          'subscription_cancelled',
  TrialExpired:       'subscription_trial_expired',
  GracePeriodExpired: 'subscription_grace_period_expired',
  PastDue:            'subscription_past_due',
  ReadOnly:           'readonly_mode',
  AccessDenied:       'subscription_access_denied',
} as const;

// ── Helper predicates ─────────────────────────────────────────────────────────

/** True when the billing status means writes are blocked (ReadOnly mode). */
export const isReadOnlyBillingStatus = (status: string): boolean =>
  status === BillingAccountStatus.GracePeriod || status === BillingAccountStatus.PastDue;

/** True when the billing status means ALL access is blocked. */
export const isBlockedBillingStatus = (status: string): boolean =>
  status === BillingAccountStatus.Suspended || status === BillingAccountStatus.Cancelled;

/** True when the invoice is in an actionable (unpaid) state. */
export const isOpenInvoice = (status: string): boolean =>
  status === InvoiceStatus.Open;

/** 403 denial codes that mean the tenant is fully blocked → logout. */
export const BLOCKED_DENIAL_CODES = new Set([
  DenialCode.Suspended,
  DenialCode.Inactive,
  DenialCode.Cancelled,
  DenialCode.TrialExpired,
  DenialCode.GracePeriodExpired,
  DenialCode.AccessDenied,
]);

/** 403 denial codes that mean ReadOnly mode → no logout, show banner. */
export const READONLY_DENIAL_CODES = new Set([
  DenialCode.PastDue,
  DenialCode.ReadOnly,
]);
