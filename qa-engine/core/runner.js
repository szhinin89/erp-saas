import { createTestCorrelationId } from './correlation.js';

/** @typedef {'infra'|'auth'|'tenant'|'permissions'|'cache'|'security'|'endpoints'} QaSuite */

/**
 * @typedef {Object} QaStepTrace
 * @property {string} step
 * @property {string} at
 * @property {Record<string, unknown>} [detail]
 */

/**
 * @typedef {Object} QaTestResult
 * @property {string} id
 * @property {string} name
 * @property {QaSuite} suite
 * @property {boolean} critical
 * @property {'PASS'|'FAIL'} status
 * @property {string} [error]
 * @property {number} durationMs
 * @property {string} correlation_id
 * @property {string} start_time
 * @property {string} end_time
 * @property {QaStepTrace[]} step_trace
 * @property {Record<string, unknown>} [behavior]
 * @property {number} [attempts]
 */

/**
 * @typedef {Object} QaTestContext
 * @property {QaStepTrace[]} trace
 * @property {(snapshot: Record<string, unknown>) => void} recordBehavior
 */

export class QaRunner {
  /**
   * @param {object} opts
   * @param {string} opts.runCorrelationId
   * @param {import('./logger.js').QaLogger} opts.logger
   * @param {import('./behavior.js').BehaviorCollector} opts.behavior
   * @param {number} [opts.infraRetryCount]
   * @param {number} [opts.testTimeoutMs]
   */
  constructor(opts) {
    this.runCorrelationId = opts.runCorrelationId;
    this.logger = opts.logger;
    this.behavior = opts.behavior;
    this.infraRetryCount = opts.infraRetryCount ?? 3;
    this.testTimeoutMs = opts.testTimeoutMs ?? 60_000;
    /** @type {QaTestResult[]} */
    this.results = [];
    /** @type {QaTestResult[]} */
    this.executionTrace = [];
  }

  /**
   * @param {string} id
   * @param {string} name
   * @param {QaSuite} suite
   * @param {boolean} critical
   * @param {(ctx: QaTestContext) => Promise<void|Record<string, unknown>>} fn
   * @param {{ retry?: boolean; timeoutMs?: number }} [opts]
   */
  async run(id, name, suite, critical, fn, opts = {}) {
    const correlationId = createTestCorrelationId(this.runCorrelationId, id);
    const suiteLogger = this.logger.child(suite);
    const maxAttempts = opts.retry && suite === 'infra' ? this.infraRetryCount : 1;
    const timeoutMs = opts.timeoutMs ?? this.testTimeoutMs;

    /** @type {QaTestResult|null} */
    let lastResult = null;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      const trace = /** @type {QaStepTrace[]} */ ([]);
      const startTime = new Date().toISOString();
      const started = Date.now();

      trace.push({ step: 'start', at: startTime, detail: { attempt, maxAttempts } });

      try {
        /** @type {Record<string, unknown>} */
        let capturedBehavior = {};

        const recordBehavior = (snapshot) => {
          capturedBehavior = { ...capturedBehavior, ...snapshot };
          this.behavior.record(id, snapshot);
        };

        await this.withTimeout(
          async () => {
            const maybeBehavior = await fn({ trace, recordBehavior });
            if (maybeBehavior && typeof maybeBehavior === 'object') {
              recordBehavior(maybeBehavior);
            }
          },
          timeoutMs,
          `${id} timed out after ${timeoutMs}ms`,
        );

        trace.push({ step: 'complete', at: new Date().toISOString() });

        lastResult = {
          id,
          name,
          suite,
          critical,
          status: 'PASS',
          durationMs: Date.now() - started,
          correlation_id: correlationId,
          start_time: startTime,
          end_time: new Date().toISOString(),
          step_trace: trace,
          behavior: Object.keys(capturedBehavior).length ? capturedBehavior : undefined,
          attempts: attempt,
        };

        this.results.push(lastResult);
        this.executionTrace.push(lastResult);
        suiteLogger.info('test passed', { test_id: id, duration_ms: lastResult.durationMs, attempts: attempt });
        return;
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        trace.push({ step: 'error', at: new Date().toISOString(), detail: { message, attempt } });

        lastResult = {
          id,
          name,
          suite,
          critical,
          status: 'FAIL',
          error: message,
          durationMs: Date.now() - started,
          correlation_id: correlationId,
          start_time: startTime,
          end_time: new Date().toISOString(),
          step_trace: trace,
          attempts: attempt,
        };

        if (attempt < maxAttempts) {
          suiteLogger.warn('test failed — retrying', { test_id: id, attempt, error: message });
          await sleep(Math.min(500 * attempt, 2000));
          continue;
        }

        this.results.push(lastResult);
        this.executionTrace.push(lastResult);
        suiteLogger.error('test failed', { test_id: id, error: message, duration_ms: lastResult.durationMs });
      }
    }
  }

  /**
   * @param {() => Promise<void>} fn
   * @param {number} timeoutMs
   * @param {string} message
   */
  async withTimeout(fn, timeoutMs, message) {
    let timer;
    try {
      await Promise.race([
        fn(),
        new Promise((_, reject) => {
          timer = setTimeout(() => reject(new Error(message)), timeoutMs);
        }),
      ]);
    } finally {
      if (timer) clearTimeout(timer);
    }
  }

  get criticalFailures() {
    return this.results.filter((r) => r.critical && r.status === 'FAIL');
  }

  get failed() {
    return this.results.filter((r) => r.status === 'FAIL');
  }
}

/** @param {number} ms */
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
