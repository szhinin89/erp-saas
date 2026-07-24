import { loadConfig, loadGrandfather, isGrandfathered } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { scanControllers, findBackendPatternLines } from './shared/backend-utils.mjs';

export const CHECK_NAME = 'backend-controller-thin';

export function runCheckBackendControllerThin() {
  const rules = loadConfig('architecture-rules.json');
  const grandfather = loadGrandfather();
  const cfg = rules.backend.controller;
  const result = createCheckResult(CHECK_NAME);

  for (const ctrl of scanControllers(cfg.path)) {
    if (isGrandfathered(grandfather, 'backendControllerMaxLines', ctrl.rel)) continue;

    if (ctrl.lines > cfg.maxLinesError) {
      addViolation(result, {
        rule: 'B-controller',
        file: ctrl.rel,
        message: `controller has ${ctrl.lines} lines (max ${cfg.maxLinesError}); delegate to MediatR handlers`,
      });
    } else if (ctrl.lines > cfg.maxLinesWarning) {
      addWarning(result, {
        rule: 'B-controller-warn',
        file: ctrl.rel,
        message: `controller has ${ctrl.lines} lines (warning threshold ${cfg.maxLinesWarning})`,
      });
    }

    for (const hit of findBackendPatternLines(ctrl.text, cfg.forbiddenPatterns)) {
      addViolation(result, {
        rule: 'B-controller',
        file: ctrl.rel,
        line: hit.line,
        message: `forbidden pattern in controller: ${hit.snippet}`,
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-backend-controller-thin.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckBackendControllerThin()) ? 0 : 1);
}
