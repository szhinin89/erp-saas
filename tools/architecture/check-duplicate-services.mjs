/**
 * check-duplicate-services.mjs
 * Detecta registros duplicados de interfaces en DependencyInjection.cs
 * Regla: una interfaz de servicio no debe registrarse más de una vez con
 * el mismo lifetime (AddScoped / AddSingleton / AddTransient).
 */
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel, readText } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

export const CHECK_NAME = 'duplicate-services';

const REGISTER_RE =
  /\.(?:AddScoped|AddSingleton|AddTransient)\s*<\s*(I[A-Za-z]\w+)\s*(?:,\s*[A-Za-z]\w*)?\s*>/g;

export function runCheckDuplicateServices() {
  const result = createCheckResult(CHECK_NAME);
  const diRoot = path.join(REPO_ROOT, 'backend/src');

  for (const abs of walkFiles(diRoot, { extensions: ['.cs'] })) {
    const rel = toRepoRel(abs);
    if (!rel.includes('DependencyInjection') && !rel.includes('ServiceRegistration')) continue;

    const text = readText(rel);
    /** @type {Map<string, number>} interface → count */
    const seen = new Map();

    let m;
    REGISTER_RE.lastIndex = 0;
    while ((m = REGISTER_RE.exec(text)) !== null) {
      const iface = m[1];
      seen.set(iface, (seen.get(iface) ?? 0) + 1);
    }

    for (const [iface, count] of seen) {
      if (count > 1) {
        addViolation(result, {
          rule: 'B-duplicate-di',
          file: rel,
          message: `${iface} registrado ${count} veces en DI — puede causar comportamiento inesperado`,
        });
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-duplicate-services.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckDuplicateServices()) ? 0 : 1);
}
