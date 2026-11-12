import { loadConfig, loadGrandfather, isGrandfathered } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { scanControllers, findBackendPatternLines } from './shared/backend-utils.mjs';

export const CHECK_NAME = 'backend-controller-thin';

/**
 * Evalúa un controller medido (`measureController`). El umbral cuenta líneas EFECTIVAS: se excluyen
 * solo los metadatos de documentación de la API (`///` y `[ProducesResponseType(...)]`), de modo que
 * documentar Swagger no penaliza al controller.
 * @param {{ rel: string, lines: number, xmlDocLines: number, attributeLines: number, effectiveLines: number, text: string }} ctrl
 * @param {{ maxLinesError: number, maxLinesWarning: number, forbiddenPatterns: (string|RegExp)[] }} cfg
 */
export function evaluateController(ctrl, cfg) {
  /** @type {{ rule: string, file: string, line?: number, message: string }[]} */
  const violations = [];
  /** @type {{ rule: string, file: string, message: string }[]} */
  const warnings = [];
  const detail = `${ctrl.effectiveLines} effective lines (${ctrl.lines} total, ${ctrl.xmlDocLines} XML doc, ${ctrl.attributeLines} ProducesResponseType)`;

  if (ctrl.effectiveLines > cfg.maxLinesError) {
    violations.push({
      rule: 'B-controller',
      file: ctrl.rel,
      message: `controller has ${detail} (max ${cfg.maxLinesError}); delegate to MediatR handlers`,
    });
  } else if (ctrl.effectiveLines > cfg.maxLinesWarning) {
    warnings.push({
      rule: 'B-controller-warn',
      file: ctrl.rel,
      message: `controller has ${detail} (warning threshold ${cfg.maxLinesWarning})`,
    });
  }

  for (const hit of findBackendPatternLines(ctrl.text, cfg.forbiddenPatterns)) {
    violations.push({
      rule: 'B-controller',
      file: ctrl.rel,
      line: hit.line,
      message: `forbidden pattern in controller: ${hit.snippet}`,
    });
  }
  return { violations, warnings };
}

export function runCheckBackendControllerThin() {
  const rules = loadConfig('architecture-rules.json');
  const grandfather = loadGrandfather();
  const cfg = rules.backend.controller;
  const result = createCheckResult(CHECK_NAME);

  for (const ctrl of scanControllers(cfg.path)) {
    if (isGrandfathered(grandfather, 'backendControllerMaxLines', ctrl.rel)) continue;
    const { violations, warnings } = evaluateController(ctrl, cfg);
    for (const v of violations) addViolation(result, v);
    for (const w of warnings) addWarning(result, w);
  }

  return result;
}

if (process.argv[1]?.endsWith('check-backend-controller-thin.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckBackendControllerThin()) ? 0 : 1);
}
