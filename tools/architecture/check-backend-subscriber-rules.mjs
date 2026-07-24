import path from 'node:path';
import { REPO_ROOT, loadConfig, matchGlob, toRepoRel, walkFiles } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { readBackendText, scanIgnoreQueryFilters } from './shared/backend-utils.mjs';

export const CHECK_NAME = 'backend-subscriber-rules';

/** @param {string} rel @param {string[]} globs */
function isExempt(rel, globs) {
  const norm = rel.replace(/\\/g, '/');
  return globs.some((g) => matchGlob(g, norm) || matchGlob(`**/${g}`, norm));
}

export function runCheckBackendSubscriberRules() {
  const rules = loadConfig('architecture-rules.json');
  const cfg = rules.backend.subscriber;
  const result = createCheckResult(CHECK_NAME);

  for (const hit of scanIgnoreQueryFilters(cfg.scanPath, cfg.ignoreQueryFiltersAllowlist)) {
    addViolation(result, {
      rule: 'B-subscriber',
      file: hit.file,
      line: hit.line,
      message: 'IgnoreQueryFilters() outside allowlist; use IPlatformQueryAccessor',
    });
  }

  const domainRoot = path.join(REPO_ROOT, cfg.entityScanPath);
  for (const file of walkFiles(domainRoot, { extensions: ['.cs'] })) {
    const rel = toRepoRel(file);
    if (isExempt(rel, cfg.exemptEntityGlobs)) continue;
    const text = readBackendText(rel);
    if (!/class\s+\w+/.test(text)) continue;
    if (!/: (MasterEntity|DocumentEntity|AuditableEntity)/.test(text)) continue;
    if (text.includes(cfg.scopedMarker)) continue;
    if (/\bTenantId\b/.test(text) || /\bSubscriberId\b/.test(text)) continue;

    addWarning(result, {
      rule: 'B-subscriber-warn',
      file: rel,
      message: `business entity without ${cfg.scopedMarker} or TenantId/SubscriberId; verify global filter`,
    });
  }

  return result;
}

if (process.argv[1]?.endsWith('check-backend-subscriber-rules.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckBackendSubscriberRules()) ? 0 : 1);
}
