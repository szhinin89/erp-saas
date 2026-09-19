// @vitest-environment node
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import ts from "typescript";
import { describe, expect, it } from "vitest";
import { dictionaries } from "../../i18n/dictionaries";
import { openCashSessionSchema, recordMovementSchema } from "./schemas/cajaSchema";
import { manualCashMovementTypeOptions, cashMovementTypeLabel } from "./constants/cashMovementTypes";
import { formatApiRequestError } from "../lib/apiError";

describe("cash locale integrity", () => {
  it("selects manual types by internal values even when translated labels collide", () => {
    expect(manualCashMovementTypeOptions(() => "same label")).toEqual([
      { value: "ManualIncome", label: "same label" },
      { value: "ManualExpense", label: "same label" },
      { value: "Withdrawal", label: "same label" },
    ]);
    expect(cashMovementTypeLabel(() => "refund", "SaleRefund")).toBe("refund");
    expect(cashMovementTypeLabel(() => "translated", "FutureType")).toBe("FutureType");
  });

  it.each(["en", "qu"] as const)("localizes the 401 fallback and preserves API messages in %s", locale => {
    const labels = { generic: dictionaries[locale]["caja.session.openError"], unauthorized: dictionaries[locale]["caja.session.unauthorized"] };
    expect(formatApiRequestError({ isAxiosError: true, response: { status: 401, data: null } }, labels)).toBe(labels.unauthorized);
    expect(formatApiRequestError({ isAxiosError: true, response: { status: 401, data: { message: { user: "server message" } } } }, labels)).toBe("server message");
  });

  it("keeps caja.*/common.* keys in parity across locales and detects duplicate JSON properties", () => {
    // TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04A — el alcance de esta auditoría es Caja: la
    // paridad se exige solo para las keys que Caja realmente usa (caja.* propias + common.*
    // compartidas). Exigir paridad de TODO el catálogo (miles de keys de módulos no
    // relacionados) queda fuera de alcance de este ticket y no debe bloquear su entrega por
    // huérfanas preexistentes de otros módulos.
    const scoped = (dict: Record<string, string>) =>
      Object.keys(dict).filter((k) => k.startsWith("caja.") || k.startsWith("common."));
    for (const locale of ["es", "en", "qu"] as const) {
      expect(scoped(dictionaries[locale]).sort()).toEqual(scoped(dictionaries.es).sort());
      const source = readFileSync(resolve(`src/i18n/locales/${locale}.json`), "utf8");
      const ast = ts.parseJsonText(`${locale}.json`, source);
      const keys: string[] = [];
      function visit(node: ts.Node) {
        if (ts.isPropertyAssignment(node) && ts.isStringLiteral(node.name)) keys.push(node.name.text);
        ts.forEachChild(node, visit);
      }
      visit(ast);
      expect(keys.length).toBe(new Set(keys).size);
    }
  });

  it("resolves every literal cash translation key in all three locales with matching interpolation", () => {
    for (const file of ["pages/CajaPage.tsx", "hooks/useCajaPage.tsx", "schemas/cajaSchema.ts", "constants/cashMovementTypes.ts", "components/ManualCashMovementModal.tsx"]) {
      const source = readFileSync(resolve(`src/modules/caja/${file}`), "utf8");
      // canShow(...) toma keys de PERMISOS (p. ej. "caja.record"), un namespace plano y distinto
      // del de i18n (siempre anidado, "caja.<grupo>.<campo>") — se excluyen de este chequeo de
      // traducciones porque nunca deben existir como key de diccionario.
      for (const match of source.matchAll(/canShow\(\s*"[^"]+"\s*\)|"((?:caja|common)\.[\w.]+)"/g)) {
        if (match[1] === undefined) continue;
        const key = match[1];
        const params = (text: string) => [...text.matchAll(/\{\{(\w+)\}\}/g)].map(m => m[1]).sort();
        for (const locale of ["es", "en", "qu"] as const) {
          expect(dictionaries[locale][key], `${locale}: ${key}`).toBeTruthy();
          expect(params(dictionaries[locale][key])).toEqual(params(dictionaries.es[key]));
        }
      }
    }
  });

  it("has no untranslated JSX text or visible string props in the cash page and confirmations", () => {
    for (const file of ["pages/CajaPage.tsx", "hooks/useCajaPage.tsx", "components/ManualCashMovementModal.tsx"]) {
      const source = readFileSync(resolve(`src/modules/caja/${file}`), "utf8");
      const ast = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
      const hardcodes: string[] = [];
      function visit(node: ts.Node) {
        if (ts.isJsxText(node)) {
          const text = node.text.trim();
          // Material Symbols ligatures and punctuation have no language content.
          if (/[\p{L}]/u.test(text) && !["add", "refresh", "arrow_back"].includes(text)) hardcodes.push(text);
        }
        if (ts.isJsxAttribute(node) && /^(label|title|ariaLabel|placeholder|emptyMessage|message|closeLabel)$/.test(node.name.getText(ast)) && node.initializer && ts.isStringLiteral(node.initializer)) {
          hardcodes.push(node.initializer.text);
        }
        if (ts.isPropertyAssignment(node) && /^(header|title|confirmLabel|cancelLabel|generic)$/.test(node.name.getText(ast)) && ts.isStringLiteral(node.initializer)) {
          hardcodes.push(node.initializer.text);
        }
        ts.forEachChild(node, visit);
      }
      visit(ast);
      expect(hardcodes, file).toEqual([]);
    }
  });

  it.each(["en", "qu"] as const)("localizes validation while preserving constraints in %s", locale => {
    const t = (key: string, params?: Record<string, string | number>) => {
      let text = dictionaries[locale][key];
      for (const [key, value] of Object.entries(params ?? {})) text = text.replaceAll(`{{${key}}}`, String(value));
      return text;
    };
    const opening = openCashSessionSchema(t).safeParse({ cashRegisterId: "", openingAmount: -1 });
    expect(opening.success).toBe(false);
    if (!opening.success) expect(opening.error.issues.map(issue => issue.message)).toEqual([
      t("caja.validation.register"), t("caja.validation.openingAmount"),
    ]);
    const movement = recordMovementSchema(t).safeParse({ movementType: "ManualIncome", reasonId: "reason-1", amount: 0, description: "x".repeat(301) });
    expect(movement.success).toBe(false);
    if (!movement.success) expect(movement.error.issues.map(issue => issue.message)).toEqual([
      t("caja.validation.amount"), t("caja.validation.maxLength", { max: 300 }),
    ]);
  });
});
