/**
 * check-duplicate-services.mjs
 * Detecta registros duplicados de interfaces en DependencyInjection.cs
 * Regla: una interfaz de servicio no debe registrarse más de una vez con
 * el mismo lifetime (AddScoped / AddSingleton / AddTransient).
 *
 * Excepción: `backend.duplicateServices.allowedMultiRegistration` en
 * architecture-rules.json — lista cerrada y explícita de interfaces que se
 * registran múltiples veces a propósito porque implementan un patrón
 * strategy/plugin consumido vía `IEnumerable<TInterface>` (una implementación
 * por variante de negocio, resuelta en conjunto por un orquestador). No es
 * un allow-list genérico: cada entrada debe nombrarse explícitamente aquí y
 * en el JSON, nunca un patrón/wildcard.
 */
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel, readText, loadConfig } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

export const CHECK_NAME = 'duplicate-services';

// La interfaz puede venir con namespace completo (`AddScoped<ERP.Application.X.IFoo, ...>`):
// grupo 1 = prefijo `Namespace.` (vacío si no está calificada), grupo 2 = nombre simple (`IFoo`).
export const REGISTER_RE =
  /\.(?:AddScoped|AddSingleton|AddTransient)\s*<\s*((?:\w+\.)*)(I[A-Za-z]\w+)\s*(?:,\s*[\w.]+\s*)?>/g;

/**
 * Identidad de cada registro: `fullName` (namespace completo si existe) + `simpleName`.
 * - 0 o 1 `fullName` distinto para un `simpleName`: registros simples y calificados son la
 *   misma interfaz y se cuentan juntos.
 * - Más de 1 `fullName` distinto: interfaces distintas, cada `fullName` se cuenta por separado.
 *   Si además hay registros sin calificar, no se adivina a cuál pertenecen → `ambiguous`.
 * La allowlist se aplica por `simpleName` (lista cerrada, nunca wildcard).
 * @param {string} text
 * @param {Set<string>} allowedMultiRegistration
 * @returns {Array<{ iface: string, count: number, ambiguous?: string[] }>}
 */
export function findDuplicateRegistrations(text, allowedMultiRegistration) {
  return [...groupBySimpleName(text)].flatMap(([simpleName, group]) =>
    evaluateGroup(simpleName, group, allowedMultiRegistration),
  );
}

/** @param {string} text @returns {Map<string, { unqualified: number, qualified: Map<string, number> }>} */
function groupBySimpleName(text) {
  const bySimpleName = new Map();
  for (const [, prefix, simpleName] of text.matchAll(REGISTER_RE)) {
    const group = bySimpleName.get(simpleName) ?? { unqualified: 0, qualified: new Map() };
    if (prefix) {
      const fullName = prefix + simpleName;
      group.qualified.set(fullName, (group.qualified.get(fullName) ?? 0) + 1);
    } else {
      group.unqualified += 1;
    }
    bySimpleName.set(simpleName, group);
  }
  return bySimpleName;
}

/**
 * @param {string} simpleName
 * @param {{ unqualified: number, qualified: Map<string, number> }} group
 * @param {Set<string>} allowedMultiRegistration
 */
function evaluateGroup(simpleName, { unqualified, qualified }, allowedMultiRegistration) {
  if (qualified.size > 1 && unqualified > 0) {
    return [{ iface: simpleName, count: unqualified, ambiguous: [...qualified.keys()] }];
  }
  if (allowedMultiRegistration.has(simpleName)) return [];
  if (qualified.size > 1) {
    return [...qualified]
      .filter(([, count]) => count > 1)
      .map(([fullName, count]) => ({ iface: fullName, count }));
  }
  const count = unqualified + [...qualified.values()].reduce((a, b) => a + b, 0);
  return count > 1 ? [{ iface: simpleName, count }] : [];
}

export function runCheckDuplicateServices() {
  const result = createCheckResult(CHECK_NAME);
  const diRoot = path.join(REPO_ROOT, 'backend/src');
  const rules = loadConfig('architecture-rules.json');
  const allowedMultiRegistration = new Set(
    rules.backend?.duplicateServices?.allowedMultiRegistration ?? [],
  );

  for (const abs of walkFiles(diRoot, { extensions: ['.cs'] })) {
    const rel = toRepoRel(abs);
    if (!rel.includes('DependencyInjection') && !rel.includes('ServiceRegistration')) continue;

    for (const { iface, count, ambiguous } of findDuplicateRegistrations(readText(rel), allowedMultiRegistration)) {
      addViolation(result, {
        rule: 'B-duplicate-di',
        file: rel,
        message: ambiguous
          ? `${iface} registrado ${count} vez/veces sin namespace con ${ambiguous.length} interfaces homónimas (${ambiguous.join(', ')}) — identidad ambigua, calificar el registro`
          : `${iface} registrado ${count} veces en DI — puede causar comportamiento inesperado`,
      });
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-duplicate-services.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckDuplicateServices()) ? 0 : 1);
}
