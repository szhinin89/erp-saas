import fs from 'node:fs';
import path from 'node:path';
import { ARCH_DIR } from './shared/fs-utils.mjs';

export const BASELINE_FILE = path.join(ARCH_DIR, 'architecture-baseline.json');
export const BASELINE_REPO_PATH = 'tools/architecture/architecture-baseline.json';

export class ArchitectureBaselineError extends Error {
  constructor(message) {
    super(message);
    this.name = 'ArchitectureBaselineError';
  }
}

export function violationIdentity(check, violation) {
  return JSON.stringify([
    check,
    violation.rule,
    violation.file.replaceAll('\\', '/'),
    violation.message,
  ]);
}

function isRecord(value) {
  return value != null && typeof value === 'object' && !Array.isArray(value);
}

function validateIdentity(item, label) {
  if (!isRecord(item)) throw new ArchitectureBaselineError(`${label} must be an object.`);
  for (const key of ['check', 'rule', 'file', 'message']) {
    if (typeof item[key] !== 'string' || item[key].trim().length === 0)
      throw new ArchitectureBaselineError(`${label}.${key} must be a non-empty string.`);
  }
  if (item.file.includes('\\') || item.file.startsWith('/') || /^[A-Za-z]:/.test(item.file)
      || item.file.split('/').some((part) => part === '..' || part === '.'))
    throw new ArchitectureBaselineError(`${label}.file must be a normalized repository-relative path.`);
}

export function parseArchitectureBaseline(text) {
  let baseline;
  try {
    baseline = JSON.parse(text);
  } catch (error) {
    throw new ArchitectureBaselineError(`invalid JSON (${error.message}).`);
  }

  if (!isRecord(baseline) || baseline.schemaVersion !== 1)
    throw new ArchitectureBaselineError('schemaVersion must be 1.');
  if (!isRecord(baseline.identity)
      || JSON.stringify(baseline.identity.fields) !== JSON.stringify(['check', 'rule', 'file', 'message'])
      || baseline.identity.lineNumber !== 'excluded to keep identity stable across unrelated edits')
    throw new ArchitectureBaselineError('identity must declare the stable fields and excluded line number.');
  if (!isRecord(baseline.summary) || !Number.isInteger(baseline.summary.violations)
      || baseline.summary.violations < 0 || !isRecord(baseline.summary.byCheck))
    throw new ArchitectureBaselineError('summary must contain a non-negative violations count and byCheck object.');
  if (!Array.isArray(baseline.violations))
    throw new ArchitectureBaselineError('violations must be an array.');

  const identities = new Set();
  const actualByCheck = {};
  let total = 0;
  for (const [index, item] of baseline.violations.entries()) {
    const label = `violations[${index}]`;
    validateIdentity(item, label);
    if (!Number.isInteger(item.count) || item.count < 1)
      throw new ArchitectureBaselineError(`${label}.count must be a positive integer.`);
    const identity = violationIdentity(item.check, item);
    if (identities.has(identity)) throw new ArchitectureBaselineError(`${label} duplicates an existing identity.`);
    identities.add(identity);
    total += item.count;
    actualByCheck[item.check] = (actualByCheck[item.check] ?? 0) + item.count;
  }

  if (baseline.summary.violations !== total)
    throw new ArchitectureBaselineError(`summary.violations is ${baseline.summary.violations}, but identities contain ${total}.`);

  const declaredChecks = Object.keys(baseline.summary.byCheck).sort((a, b) => a.localeCompare(b));
  const actualChecks = Object.keys(actualByCheck).sort((a, b) => a.localeCompare(b));
  if (JSON.stringify(declaredChecks) !== JSON.stringify(actualChecks)
      || actualChecks.some((check) => baseline.summary.byCheck[check] !== actualByCheck[check]))
    throw new ArchitectureBaselineError('summary.byCheck does not match violation identities.');

  return baseline;
}

export function loadArchitectureBaseline(filePath = BASELINE_FILE) {
  if (!fs.existsSync(filePath))
    throw new ArchitectureBaselineError(`baseline file not found: ${filePath}`);
  return parseArchitectureBaseline(fs.readFileSync(filePath, 'utf8'));
}

function collectFindings(results) {
  const findings = new Map();
  for (const result of results) {
    for (const violation of result.violations) {
      const identity = violationIdentity(result.name, violation);
      const finding = findings.get(identity) ?? {
        check: result.name,
        rule: violation.rule,
        file: violation.file.replaceAll('\\', '/'),
        message: violation.message,
        count: 0,
        lines: [],
      };
      finding.count++;
      if (Number.isInteger(violation.line)) finding.lines.push(violation.line);
      findings.set(identity, finding);
    }
  }
  return findings;
}

function sorted(items) {
  return items.sort((a, b) =>
    a.check.localeCompare(b.check)
    || a.rule.localeCompare(b.rule)
    || a.file.localeCompare(b.file)
    || a.message.localeCompare(b.message),
  );
}

export function compareArchitectureBaseline(results, baseline) {
  const current = collectFindings(results);
  const historical = new Map(baseline.violations.map((item) => [violationIdentity(item.check, item), item]));
  const newViolations = [];
  const resolvedViolations = [];
  const historicalViolations = [];
  const checks = {};
  let historicalCount = 0;
  let currentCount = 0;

  for (const [identity, item] of current) {
    currentCount += item.count;
    const previous = historical.get(identity);
    const oldCount = previous?.count ?? 0;
    const retained = Math.min(oldCount, item.count);
    const added = item.count - retained;
    historicalCount += retained;
    if (retained > 0) historicalViolations.push({ ...item, count: retained });
    if (added > 0) newViolations.push({ ...item, count: added });
    const entry = checks[item.check] ?? { baseline: 0, current: 0, historical: 0, new: 0, resolved: 0 };
    entry.current += item.count;
    entry.historical += retained;
    entry.new += added;
    checks[item.check] = entry;
  }

  for (const [identity, item] of historical) {
    const currentItem = current.get(identity);
    const currentIdentityCount = currentItem?.count ?? 0;
    const removed = item.count - Math.min(item.count, currentIdentityCount);
    const entry = checks[item.check] ?? { baseline: 0, current: 0, historical: 0, new: 0, resolved: 0 };
    entry.baseline += item.count;
    entry.resolved += removed;
    checks[item.check] = entry;
    if (removed > 0) resolvedViolations.push({
      check: item.check,
      rule: item.rule,
      file: item.file,
      message: item.message,
      count: removed,
    });
  }

  return {
    passed: newViolations.length === 0,
    baselineViolations: baseline.summary.violations,
    currentViolations: currentCount,
    historicalViolations: historicalCount,
    newViolationCount: newViolations.reduce((count, item) => count + item.count, 0),
    resolvedViolationCount: resolvedViolations.reduce((count, item) => count + item.count, 0),
    checks: Object.fromEntries(Object.entries(checks).sort(([a], [b]) => a.localeCompare(b))),
    historicalFindings: sorted(historicalViolations),
    newViolations: sorted(newViolations),
    resolvedViolations: sorted(resolvedViolations),
  };
}