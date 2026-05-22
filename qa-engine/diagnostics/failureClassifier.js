import { matchesAny } from './types.js';

/** @typedef {import('./types.js').FailureType} FailureType */

/**
 * @param {import('../core/runner.js').QaTestResult} test
 * @param {Array<{ test_id: string; field: string; severity: string }>} [behaviorChanges]
 * @returns {{ failure_type: FailureType; signals: string[] }}
 */
export function classifyFailure(test, behaviorChanges = []) {
  const signals = [];
  const id = test.id ?? '';
  const suite = test.suite ?? '';
  const error = test.error ?? '';
  const combined = `${id} ${suite} ${error} ${JSON.stringify(test.behavior ?? {})}`;

  const testBehaviorChanges = behaviorChanges.filter((c) => c.test_id === id);

  if (
    suite === 'auth' ||
    id.startsWith('auth.') ||
    id.startsWith('security.noauth') ||
    matchesAny(combined, [
      'auth bypass',
      'tampered token accepted',
      'unauth',
      'unauthorized',
      'bootstrap token reached',
      'revoked refresh token still accepted',
      'tenant accessed platform',
      'requires auth',
    ])
  ) {
    if (test.status === 'FAIL') {
      signals.push('auth-boundary');
      return { failure_type: 'AUTH_BYPASS', signals };
    }
  }

  if (
    suite === 'tenant' ||
    id.startsWith('tenant.') ||
    id.startsWith('security.subscribers') ||
    id.startsWith('security.password') ||
    matchesAny(combined, [
      'foreign switch allowed',
      'foreign read allowed',
      'cross-tenant',
      'operational settings mutation allowed',
      'password reset mode allowed',
      'tampered subscriber context accepted',
    ])
  ) {
    if (test.status === 'FAIL') {
      signals.push('tenant-isolation');
      return { failure_type: 'TENANT_ISOLATION_LEAK', signals };
    }
  }

  if (
    suite === 'permissions' ||
    id.startsWith('permissions.') ||
    testBehaviorChanges.some((c) => c.field.includes('permissions')) ||
    matchesAny(combined, ['empty permissions', 'wildcard', 'activity allowed', 'permission snapshot'])
  ) {
    if (test.status === 'FAIL') {
      signals.push('permissions-runtime');
      return { failure_type: 'PERMISSION_DRIFT', signals };
    }
  }

  if (
    suite === 'cache' ||
    id.startsWith('cache.') ||
    matchesAny(combined, ['permission snapshot drift', 'redis write/read failed', 'cache', 'hit metrics'])
  ) {
    if (test.status === 'FAIL') {
      signals.push('cache-layer');
      return { failure_type: 'CACHE_INCONSISTENCY', signals };
    }
  }

  if (
    suite === 'endpoints' ||
    id.startsWith('ep.') ||
    testBehaviorChanges.some((c) => c.field.includes('responseFields')) ||
    matchesAny(combined, ['server error', 'protected endpoint rejected', 'endpoint'])
  ) {
    if (test.status === 'FAIL') {
      signals.push('endpoint-contract');
      return { failure_type: 'API_BREAKING_CHANGE', signals };
    }
  }

  if (matchesAny(combined, ['422', 'validation', 'unprocessable'])) {
    signals.push('validation');
    return { failure_type: 'VALIDATION_ERROR', signals };
  }

  if (
    suite === 'infra' ||
    id.startsWith('infra.') ||
    matchesAny(combined, ['timed out', 'not ready', 'api down', 'frontend down', 'econnrefused'])
  ) {
    if (test.status === 'FAIL') {
      signals.push('infra');
      return { failure_type: 'INFRA_FAILURE', signals };
    }
  }

  if (test.status === 'FAIL') {
    signals.push('unclassified');
    return { failure_type: 'UNKNOWN', signals };
  }

  return { failure_type: 'UNKNOWN', signals: ['passed-or-skipped'] };
}

/**
 * @param {import('../core/runner.js').QaTestResult[]} results
 * @param {object} diff
 */
export function classifyAllFailures(results, diff) {
  const behaviorChanges = diff?.behaviorChanges ?? [];
  return results
    .filter((t) => t.status === 'FAIL')
    .map((test) => {
      const { failure_type, signals } = classifyFailure(test, behaviorChanges);
      return {
        test: test.id,
        name: test.name,
        suite: test.suite,
        status: test.status,
        failure_type,
        signals,
        correlation_id: test.correlation_id,
      };
    });
}
