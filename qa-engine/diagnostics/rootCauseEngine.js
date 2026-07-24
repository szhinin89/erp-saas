/** @typedef {import('./types.js').FailureType} FailureType */

/** @type {Record<FailureType, Array<{ pattern: RegExp; cause: string; component: string; evidence: string[]; confidence: number }>>} */
const INFERENCE_RULES = {
  AUTH_BYPASS: [
    {
      pattern: /tampered token|tampered subscriber/i,
      cause: 'JWT validation or signature verification accepts malformed or tampered bearer tokens',
      component: 'auth-jwt',
      evidence: ['Tampered bearer token returned non-401/403', 'Token integrity check may be bypassed'],
      confidence: 0.92,
    },
    {
      pattern: /unauth|unauthorized|bootstrap token reached|platform/i,
      cause: 'Protected endpoint reachable without a valid session token or with wrong token scope',
      component: 'authorization-pipeline',
      evidence: ['Business or platform endpoint responded 2xx with invalid token scope'],
      confidence: 0.88,
    },
    {
      pattern: /refresh token still accepted|revoked refresh/i,
      cause: 'Refresh token revocation is not enforced — logout does not invalidate refresh chain',
      component: 'auth-refresh',
      evidence: ['Revoked refresh token still exchanges for access token'],
      confidence: 0.9,
    },
  ],
  TENANT_ISOLATION_LEAK: [
    {
      pattern: /foreign switch|switch allowed/i,
      cause: 'Company switch endpoint does not validate membership — cross-company context injection possible',
      component: 'tenant-switch-company',
      evidence: ['switch-company succeeded for foreign companyId'],
      confidence: 0.93,
    },
    {
      pattern: /foreign read|cross-tenant/i,
      cause: 'Company read API lacks subscriber-scoped authorization — cross-tenant data leak',
      component: 'tenant-data-access',
      evidence: ['GET /api/companies/{id} returned 2xx for foreign tenant resource'],
      confidence: 0.95,
    },
    {
      pattern: /operational settings|password reset mode/i,
      cause: 'Subscriber mutation endpoints missing ownership check (CanAccessSubscriber)',
      component: 'subscribers-controller',
      evidence: ['PATCH on foreign subscriber succeeded from tenant A session'],
      confidence: 0.91,
    },
  ],
  PERMISSION_DRIFT: [
    {
      pattern: /empty permissions|wildcard|permission snapshot/i,
      cause: 'EffectivePermissionKeysProvider or UI snapshot returns incorrect permission set for session role',
      component: 'permissions-runtime',
      evidence: ['Permission snapshot empty or missing admin wildcard'],
      confidence: 0.84,
    },
    {
      pattern: /bootstrap token reached|activity allowed/i,
      cause: 'Bootstrap token reaches business endpoints — session permission policies not applied to token type',
      component: 'permissions-policy',
      evidence: ['Bootstrap-scoped token passed PermissionHandler for business route'],
      confidence: 0.87,
    },
    {
      pattern: /drift|permissions\.count/i,
      cause: 'EffectivePermissionKeysProvider cache hit returns stale profile snapshot after permission mutation',
      component: 'permissions-cache',
      evidence: ['Permission count changed between reads without invalidation', 'Cache HIT without version bump'],
      confidence: 0.86,
    },
  ],
  CACHE_INCONSISTENCY: [
    {
      pattern: /drift between reads|snapshot drift/i,
      cause: 'Permissions cache serves inconsistent snapshots across consecutive reads in same session',
      component: 'permissions-cache',
      evidence: ['Identical session token yielded different permission keys'],
      confidence: 0.85,
    },
    {
      pattern: /redis/i,
      cause: 'Redis distributed cache unavailable or write/read probe failed',
      component: 'redis-cache',
      evidence: ['Redis health probe writeReadOk=false'],
      confidence: 0.78,
    },
  ],
  API_BREAKING_CHANGE: [
    {
      pattern: /server error|status=5/i,
      cause: 'Critical API endpoint returning 5xx — possible breaking change or unhandled exception',
      component: 'api-handlers',
      evidence: ['Endpoint returned 5xx during smoke test'],
      confidence: 0.75,
    },
    {
      pattern: /protected endpoint rejected|permissions failed/i,
      cause: 'Previously valid session rejected — auth contract or token claims may have changed',
      component: 'api-auth-contract',
      evidence: ['Session token rejected on formerly accessible route'],
      confidence: 0.72,
    },
  ],
  VALIDATION_ERROR: [
    {
      pattern: /422|validation|unprocessable/i,
      cause: 'Request validation rejected unexpectedly — payload contract may have changed',
      component: 'api-validation',
      evidence: ['422 Unprocessable Entity on QA fixture payload'],
      confidence: 0.7,
    },
  ],
  INFRA_FAILURE: [
    {
      pattern: /timed out|not ready after/i,
      cause: 'Service readiness timeout — API or frontend failed to become healthy within polling window',
      component: 'infra-readiness',
      evidence: ['Exponential backoff polling exhausted'],
      confidence: 0.82,
    },
    {
      pattern: /api down|frontend down|not ready/i,
      cause: 'Infrastructure health check failed — service unreachable',
      component: 'infra-health',
      evidence: ['/health/live or /health/ready returned non-2xx'],
      confidence: 0.8,
    },
  ],
  UNKNOWN: [
    {
      pattern: /.*/,
      cause: 'Failure pattern not matched to known diagnostic rule — manual triage required',
      component: 'unknown',
      evidence: ['Inspect execution-trace.json step_trace for this test'],
      confidence: 0.4,
    },
  ],
};

/**
 * @param {import('../core/runner.js').QaTestResult} test
 * @param {FailureType} failureType
 * @param {{ diff?: object }} [context]
 */
export function inferRootCause(test, failureType, context = {}) {
  const error = test.error ?? '';
  const haystack = `${error} ${test.id} ${test.name}`;
  const rules = INFERENCE_RULES[failureType] ?? INFERENCE_RULES.UNKNOWN;

  for (const rule of rules) {
    if (rule.pattern.test(haystack)) {
      const evidence = [...rule.evidence];
      if (test.step_trace?.length) {
        const lastStep = test.step_trace[test.step_trace.length - 1];
        evidence.push(`Last step: ${lastStep.step}`);
      }
      for (const bc of (context.diff?.behaviorChanges ?? []).filter((c) => c.test_id === test.id).slice(0, 2)) {
        evidence.push(`Behavior: ${bc.field} ${JSON.stringify(bc.old)} → ${JSON.stringify(bc.new)}`);
      }
      return {
        root_cause: rule.cause,
        confidence: rule.confidence,
        related_component: rule.component,
        evidence,
      };
    }
  }

  const fallback = INFERENCE_RULES.UNKNOWN[0];
  return {
    root_cause: fallback.cause,
    confidence: fallback.confidence,
    related_component: fallback.component,
    evidence: [...fallback.evidence, `Error: ${error}`],
  };
}
