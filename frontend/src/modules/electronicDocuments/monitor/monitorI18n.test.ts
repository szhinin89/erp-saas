// @vitest-environment node
import { describe, expect, it } from "vitest";
import es from "../../../i18n/locales/es.json";
import en from "../../../i18n/locales/en.json";
import { HELP_KEYS, resolveHelp } from "../../../help";
import {
  ELECTRONIC_DOCUMENT_STATE_GROUPS,
  ELECTRONIC_DOCUMENT_STATES,
  ELECTRONIC_DOCUMENT_TYPES,
} from "./utils/enumOptions";

/**
 * SRI-MONITOR-UX-01 — paridad es/en del Monitor y del diagnóstico compartido: toda clave literal
 * que usan sus componentes existe en ambos idiomas (antes cinco claves del panel compartido y la
 * sección secundaria dependían de un texto de respaldo en español).
 */

const ES = es as Record<string, string>;
const EN = en as Record<string, string>;

const sources = import.meta.glob(
  [
    "./**/*.{ts,tsx}",
    "!./**/*.test.{ts,tsx}",
    "../../../components/zh/electronicDocuments/*.tsx",
    "../../../components/zh/ZHSriServiceHealth.tsx",
  ],
  { query: "?raw", import: "default", eager: true },
) as Record<string, string>;

function literalKeys(): string[] {
  const keys = new Set<string>();
  for (const code of Object.values(sources))
    for (const match of code.matchAll(/\bt\(\s*"([A-Za-z0-9_.]+)"/g))
      keys.add(match[1]);
  return [...keys];
}

describe("Monitor SRI — paridad i18n", () => {
  it("los componentes del Monitor y del diagnóstico compartido se leen", () => {
    expect(Object.keys(sources).length).toBeGreaterThan(10);
  });

  it("cada clave literal existe en es y en", () => {
    const missing = literalKeys().filter((k) => !(k in ES) || !(k in EN));
    expect(missing).toEqual([]);
  });

  it("estados, tipos, grupos y unidades existen en ambos idiomas", () => {
    const keys = [
      ...ELECTRONIC_DOCUMENT_STATES.map((s) => `electronicDocuments.monitor.state.${s}`),
      ...ELECTRONIC_DOCUMENT_TYPES.map(
        (d) => `electronicDocuments.monitor.documentType.${d}`,
      ),
      ...Object.values(ELECTRONIC_DOCUMENT_STATE_GROUPS).map((g) => g.labelKey),
      "electronicDocuments.monitor.units.minutes",
      "electronicDocuments.monitor.units.hours",
    ];
    expect(keys.filter((k) => !(k in ES) || !(k in EN))).toEqual([]);
  });

  it("DeadLetter se nombra por su única causa: reintentos agotados", () => {
    expect(ES["electronicDocuments.monitor.state.DeadLetter"]).toBe("Reintentos agotados");
    expect(EN["electronicDocuments.monitor.state.DeadLetter"]).toBe("Retries exhausted");
    expect(ES["electronicDocuments.monitor.timelineAction.DeadLetter"]).toBe(
      "Reintentos agotados",
    );
  });

  it("el subtítulo ya no declara una consola de solo lectura y «Registrados hoy» refleja el cálculo", () => {
    expect(ES["electronicDocuments.monitor.subtitle"]).not.toMatch(/solo lectura/i);
    expect(EN["electronicDocuments.monitor.subtitle"]).not.toMatch(/read-only/i);
    expect(ES["electronicDocuments.monitor.dashboard.totalToday"]).toBe("Registrados hoy");
    expect(EN["electronicDocuments.monitor.dashboard.totalToday"]).toBe("Registered today");
    expect("electronicDocuments.monitor.table.retryNumber" in ES).toBe(false);
    expect("electronicDocuments.monitor.table.retryNumber" in EN).toBe(false);
  });

  it("las ayudas del Monitor tienen título, resumen y detalle en es y en, iguales al registro", () => {
    const keys = [
      HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_CURRENT_STATE,
      HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_TODAY,
      HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_FOLLOW_UP,
      HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_ATTENTION,
      HELP_KEYS.ELECTRONIC_DOCUMENTS_MONITOR_SRI_SERVICES,
    ];
    for (const key of keys) {
      const help = resolveHelp(key);
      expect(help?.i18nPrefix).toBeTruthy();
      for (const part of ["title", "short", "long"] as const) {
        const k = `${help!.i18nPrefix}.${part}`;
        expect(ES[k], k).toBe(help![part]);
        expect(EN[k], k).toBeTruthy();
        expect(EN[k], k).not.toBe(ES[k]);
      }
    }
  });
});
