import { loadConfig, loadGrandfather, isGrandfathered } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';
import { parseCsproj } from './shared/backend-utils.mjs';

export const CHECK_NAME = 'backend-layering';

export function runCheckBackendLayering() {
  const rules = loadConfig('architecture-rules.json');
  const cfg = rules.backend.layering;
  const result = createCheckResult(CHECK_NAME);

  for (const [key, project] of Object.entries(cfg.projects)) {
    const { projectRefs, packages } = parseCsproj(project.csproj);
    const allowedRefs = new Set(project.allowedProjectRefs.map((r) => r.toLowerCase()));
    for (const ref of projectRefs) {
      const norm = ref.toLowerCase();
      const ok = [...allowedRefs].some((a) => norm.endsWith(a.replace(/\\/g, '/').toLowerCase()));
      if (!ok) {
        addViolation(result, {
          rule: 'B-layering',
          file: project.csproj,
          message: `${key} must not reference ${ref}`,
        });
      }
    }
    if (project.allowedPackages) {
      const allowedPkg = new Set(project.allowedPackages.map((p) => p.toLowerCase()));
      for (const pkg of packages) {
        const pkgLower = pkg.toLowerCase();
        const prefixOk = (project.allowedPackagePrefixes ?? []).some((p) => pkgLower.startsWith(p.toLowerCase()));
        if (!allowedPkg.has(pkgLower) && !prefixOk) {
          addViolation(result, {
            rule: 'B-layering',
            file: project.csproj,
            message: `${key} package not allowlisted: ${pkg}`,
          });
        }
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-backend-layering.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckBackendLayering()) ? 0 : 1);
}
