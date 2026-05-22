import { loadConfig } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation } from './shared/report-utils.mjs';
import { scanCsUsings } from './shared/backend-utils.mjs';

export const CHECK_NAME = 'backend-clean-architecture';

export function runCheckBackendCleanArchitecture() {
  const rules = loadConfig('architecture-rules.json');
  const cfg = rules.backend.cleanArchitecture;
  const result = createCheckResult(CHECK_NAME);

  for (const [layer, spec] of Object.entries(cfg.layers)) {
    const hits = scanCsUsings(spec.path, { forbiddenPatterns: spec.forbiddenUsings });
    for (const hit of hits) {
      addViolation(result, {
        rule: `B-${layer}`,
        file: hit.file,
        line: hit.line,
        message: `forbidden using in ${layer}: ${hit.using}`,
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-backend-clean-architecture.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckBackendCleanArchitecture()) ? 0 : 1);
}
