/**
 * Behavioral fingerprints captured during tests for regression V2.
 */
export class BehaviorCollector {
  constructor() {
    /** @type {Record<string, unknown>} */
    this.byTestId = {};
    /** @type {Record<string, unknown>} */
    this.global = {};
  }

  /** @param {string} testId @param {Record<string, unknown>} snapshot */
  record(testId, snapshot) {
    this.byTestId[testId] = { ...(this.byTestId[testId] ?? {}), ...snapshot, _recordedAt: new Date().toISOString() };
  }

  /** @param {string} key @param {unknown} value */
  recordGlobal(key, value) {
    this.global[key] = value;
  }

  toJSON() {
    return { byTestId: this.byTestId, global: this.global };
  }
}

/**
 * Deep-compare behavioral snapshots and emit change records.
 * @param {Record<string, unknown>|undefined} prev
 * @param {Record<string, unknown>|undefined} cur
 * @param {string} testId
 */
export function diffBehaviorSnapshots(prev, cur, testId) {
  /** @type {Array<{ test_id: string; change_type: string; field: string; old: unknown; new: unknown; severity: string }>} */
  const changes = [];

  if (!prev && cur) return changes;
  if (!cur) return changes;

  const allKeys = new Set([...Object.keys(prev ?? {}), ...Object.keys(cur ?? {})]);

  for (const key of allKeys) {
    if (key.startsWith('_')) continue;
    const oldVal = prev?.[key];
    const newVal = cur?.[key];
    if (JSON.stringify(oldVal) === JSON.stringify(newVal)) continue;

    changes.push({
      test_id: testId,
      change_type: 'BEHAVIOR_CHANGE',
      field: key,
      old: oldVal,
      new: newVal,
      severity: inferSeverity(key, oldVal, newVal),
    });
  }

  return changes;
}

/** @param {string} field @param {unknown} oldVal @param {unknown} newVal */
function inferSeverity(field, oldVal, newVal) {
  if (field.includes('permissions.count') || field.includes('isolation')) return 'CRITICAL';
  if (field.includes('status') || field.includes('auth.')) return 'HIGH';
  if (typeof oldVal === 'number' && typeof newVal === 'number' && oldVal !== newVal) return 'HIGH';
  return 'MEDIUM';
}
