import path from 'node:path';
import { ARCH_DIR, REPO_ROOT, loadConfig, loadGrandfather, walkFiles, toRepoRel } from './shared/fs-utils.mjs';
import { moduleFromPath } from './shared/rule-utils.mjs';

/** @param {import('./shared/report-utils.mjs').CheckResult[]} results */
export function calculateArchitectureScore(results) {
  const scoring = loadConfig('scoring-rules.json');
  const rules = loadConfig('architecture-rules.json');
  const grandfather = loadGrandfather();

  let score = scoring.baseScore;
  let violations = 0;
  let warnings = 0;

  for (const r of results) {
    violations += r.violations.length;
    warnings += r.warnings.length;
    score -= r.violations.length * scoring.violationPenalty;
    score -= r.warnings.length * scoring.warningPenalty;
  }

  const gfCount = countGrandfatherEntries(grandfather);
  score -= gfCount * scoring.grandfatherEntryPenalty;

  score = Math.max(0, Math.min(100, Math.round(score)));

  const status =
    score >= scoring.statusThresholds.healthy
      ? 'healthy'
      : score >= scoring.statusThresholds.warning
        ? 'warning'
        : 'critical';

  const driftRisk = computeDriftRisk(warnings, gfCount, scoring);

  const modules = buildModuleBreakdown(results, scoring);

  return {
    architectureScore: score,
    status,
    driftRisk,
    violations,
    warnings,
    checksPassed: results.filter((r) => r.violations.length === 0).length,
    checksFailed: results.filter((r) => r.violations.length > 0).length,
    modules,
    adrs: rules.adr?.index ?? [],
  };
}

/** @param {object} grandfather */
function countGrandfatherEntries(grandfather) {
  let n = 0;
  for (const [key, val] of Object.entries(grandfather)) {
    if (key.startsWith('$')) continue;
    if (typeof val === 'number') continue;
    if (Array.isArray(val)) n += val.length;
  }
  return n;
}

/** @param {number} warnings @param {number} gfCount @param {object} scoring */
function computeDriftRisk(warnings, gfCount, scoring) {
  const { driftRisk } = scoring;
  if (gfCount >= driftRisk.grandfatherHighCount || warnings > driftRisk.mediumMaxWarnings) return 'high';
  if (warnings > driftRisk.lowMaxWarnings) return 'medium';
  return 'low';
}

/** @param {import('./shared/report-utils.mjs').CheckResult[]} results @param {object} scoring */
function buildModuleBreakdown(results, scoring) {
  /** @type {Record<string, { violations: number, warnings: number }>} */
  const modules = {};

  const bump = (mod, v = 0, w = 0) => {
    if (!modules[mod]) modules[mod] = { violations: 0, warnings: 0 };
    modules[mod].violations += v;
    modules[mod].warnings += w;
  };

  for (const r of results) {
    const warnSet = new Set(r.warnings.map((w) => `${w.file}|${w.rule}|${w.message}`));
    for (const item of r.violations) {
      const fe = moduleFromPath(item.file);
      if (fe) {
        bump(fe, 1, 0);
        continue;
      }
      const be = item.file.match(/Modules\/([^/]+)/i);
      if (be && item.file.includes('ERP.Domain')) {
        bump(be[1].toLowerCase(), 1, 0);
        continue;
      }
      if (item.file.includes('ERP.API/Controllers')) bump('api-controllers', 1, 0);
    }
    for (const item of r.warnings) {
      if (!warnSet.has(`${item.file}|${item.rule}|${item.message}`)) continue;
      const fe = moduleFromPath(item.file);
      if (fe) {
        bump(fe, 0, 1);
        continue;
      }
      if (item.file.includes('ERP.API/Controllers')) bump('api-controllers', 0, 1);
      else if (item.file.includes('ERP.Domain')) bump('domain', 0, 1);
    }
  }

  return modules;
}

if (process.argv[1]?.endsWith('calculate-score.mjs')) {
  console.log(JSON.stringify(calculateArchitectureScore([]), null, 2));
}
