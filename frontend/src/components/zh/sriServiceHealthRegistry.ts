import type { BadgeVariant } from "../PageShell";
import type {
  SriServiceHealthReason,
  SriServiceHealthState,
} from "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

/**
 * Único lugar que traduce el estado técnico de un servicio SRI (calculado por el backend, monitor
 * central de salud SRI) a su representación visual. `ZHSriServiceHealth` y cualquier otra UI leen de
 * aquí — nunca recalculan el estado. Es disponibilidad técnica, no autorización fiscal.
 */
export const SRI_SERVICE_HEALTH_STATE_REGISTRY: Record<
  SriServiceHealthState,
  { variant: BadgeVariant; labelKey: string }
> = {
  Unverified: { variant: "neutral", labelKey: "common.sriHealth.state.unverified" },
  Available: { variant: "success", labelKey: "common.sriHealth.state.available" },
  Slow: { variant: "warning", labelKey: "common.sriHealth.state.slow" },
  Intermittent: { variant: "warning", labelKey: "common.sriHealth.state.intermittent" },
  Unavailable: { variant: "error", labelKey: "common.sriHealth.state.unavailable" },
};

export const SRI_SERVICE_HEALTH_REASON_KEYS: Record<SriServiceHealthReason, string> = {
  NotConfigured: "common.sriHealth.reason.notConfigured",
  NoRecentEvidence: "common.sriHealth.reason.noRecentEvidence",
  RecentResponses: "common.sriHealth.reason.recentResponses",
  RespondedWithoutDecision: "common.sriHealth.reason.respondedWithoutDecision",
  SlowResponse: "common.sriHealth.reason.slowResponse",
  RecentTransportFailure: "common.sriHealth.reason.recentTransportFailure",
  RecentInvalidResponse: "common.sriHealth.reason.recentInvalidResponse",
  RecentUndeterminedFailure: "common.sriHealth.reason.recentUndeterminedFailure",
  RecoveredAfterFailures: "common.sriHealth.reason.recoveredAfterFailures",
  ConsecutiveTransportFailures: "common.sriHealth.reason.consecutiveTransportFailures",
  ProbeReachable: "common.sriHealth.reason.probeReachable",
  ProbeUnreachable: "common.sriHealth.reason.probeUnreachable",
  ProbeReachableAfterFailures: "common.sriHealth.reason.probeReachableAfterFailures",
};
