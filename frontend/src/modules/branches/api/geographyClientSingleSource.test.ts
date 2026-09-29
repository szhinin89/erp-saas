import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

/**
 * ZH-FRONTEND-GEOGRAPHY-SSOT-01 — guard: el endpoint de geografía solo puede aparecer en el
 * cliente HTTP único (branches/api/branchService.ts). Cualquier otro módulo consume
 * branches/facades/geographyLookupFacade; un segundo service de geografía hace fallar este test.
 */
const SRC_ROOT = fileURLToPath(new URL("../../../", import.meta.url));
const SINGLE_CLIENT = "modules/branches/api/branchService.ts";
const GEOGRAPHY_ENDPOINT = /api\/v1\/settings\/geography/;

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (/\.(ts|tsx)$/.test(name) && !/\.test\.tsx?$/.test(name)) out.push(full);
  }
  return out;
}

describe("geografía — un solo cliente HTTP", () => {
  it(`solo ${SINGLE_CLIENT} referencia /api/v1/settings/geography`, () => {
    const hits = walk(SRC_ROOT)
      .filter((file) => GEOGRAPHY_ENDPOINT.test(readFileSync(file, "utf8")))
      .map((file) => relative(SRC_ROOT, file).split(sep).join("/"));

    expect(hits).toEqual([SINGLE_CLIENT]);
  });

  // ZH-GEOGRAPHY-COUNTRY-CONTEXT-01 — el país viene del contexto (selección del usuario o la
  // fuente central del owner, p. ej. masterData PHYSICAL_ADDRESS_GEO_COUNTRY_ID), nunca de un
  // literal disperso en un consumidor.
  it("ningún consumidor pasa un país literal a provinces()", () => {
    const literalCountry = /\.provinces\(\s*["'`]/;
    const hits = walk(SRC_ROOT)
      .filter((file) => literalCountry.test(readFileSync(file, "utf8")))
      .map((file) => relative(SRC_ROOT, file).split(sep).join("/"));

    expect(hits).toEqual([]);
  });
});
