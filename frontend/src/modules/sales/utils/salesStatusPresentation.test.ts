import { describe, expect, it } from "vitest";
import es from "../../../i18n/locales/es.json";
import en from "../../../i18n/locales/en.json";
import {
  salesCommercialStatusPresentation,
  salesElectronicStatusPresentation,
} from "./salesStatusPresentation";

/**
 * SALES-ELECTRONIC-STATUS-CLARITY-01 — la autorización comercial (venta confirmada en el ERP) y la
 * autorización tributaria (SRI) nunca comparten etiqueta. Usa los diccionarios reales es/en.
 */
const tFor = (dict: Record<string, string>) => (key: string) => {
  const value = dict[key];
  if (value === undefined) throw new Error(`missing i18n key: ${key}`);
  return value;
};
const tEs = tFor(es as Record<string, string>);
const tEn = tFor(en as Record<string, string>);

const ELECTRONIC_STATES = [
  "None",
  "Draft",
  "XmlGenerated",
  "Signed",
  "Dispatching",
  "Sent",
  "Received",
  "Authorized",
  "Rejected",
  "Failed",
  "DeadLetter",
  "AnnulmentPending",
  "Cancelled",
  "Discarded",
];

describe("estado comercial de la venta", () => {
  it("Authorized se presenta como «Confirmada», nunca como autorización", () => {
    const p = salesCommercialStatusPresentation("Authorized", tEs);
    expect(p.label).toBe("Confirmada");
    expect(p.label).not.toMatch(/autoriz/i);
    expect(p.hint).toMatch(/No equivale a la autorización del SRI/);
  });

  it("Draft y Cancelled conservan sus etiquetas", () => {
    expect(salesCommercialStatusPresentation("Draft", tEs).label).toBe("Borrador");
    expect(salesCommercialStatusPresentation("Cancelled", tEs).label).toBe("Anulada");
  });

  it("inglés: Authorized → Confirmed", () => {
    expect(salesCommercialStatusPresentation("Authorized", tEn).label).toBe("Confirmed");
  });
});

describe("estado electrónico SRI (dato real del backend)", () => {
  it("Draft → «Sin emitir», aclarando que no hay XML firmado ni autorización SRI", () => {
    const p = salesElectronicStatusPresentation("Electronic", "Draft", tEs);
    expect(p.label).toBe("Sin emitir");
    expect(p.hint).toBe("Aún no tiene XML firmado ni autorización del SRI.");
  });

  it("«Autorizada por SRI» SOLO para el estado electrónico Authorized", () => {
    for (const state of ELECTRONIC_STATES) {
      const label = salesElectronicStatusPresentation("Electronic", state, tEs).label;
      if (state === "Authorized") expect(label).toBe("Autorizada por SRI");
      else expect(label).not.toBe("Autorizada por SRI");
    }
  });

  it("Signed no se presenta como autorizada", () => {
    const p = salesElectronicStatusPresentation("Electronic", "Signed", tEs);
    expect(p.label).toBe("Firmada, sin autorizar");
    expect(p.hint).toMatch(/aún no lo ha autorizado/);
  });

  it("nunca se infiere del estado comercial: sin dato → «No disponible»", () => {
    expect(salesElectronicStatusPresentation("Electronic", null, tEs).label).toBe("No disponible");
    expect(salesElectronicStatusPresentation("Electronic", undefined, tEs).label).toBe(
      "No disponible",
    );
  });

  it("None (sin documento registrado) no se confunde con Draft", () => {
    const p = salesElectronicStatusPresentation("Electronic", "None", tEs);
    expect(p.label).toBe("Sin documento electrónico");
  });

  it("venta física → «No aplica», aunque llegue un estado", () => {
    expect(salesElectronicStatusPresentation("Physical", "None", tEs).label).toBe("No aplica");
  });

  it("estado desconocido: se muestra el valor real, sin reinterpretarlo", () => {
    expect(salesElectronicStatusPresentation("Electronic", "FutureState", tEs).label).toBe(
      "FutureState",
    );
  });

  it("todas las claves existen en es y en (sin texto faltante)", () => {
    for (const state of ELECTRONIC_STATES) {
      expect(() => salesElectronicStatusPresentation("Electronic", state, tEs)).not.toThrow();
      expect(() => salesElectronicStatusPresentation("Electronic", state, tEn)).not.toThrow();
    }
    expect(salesElectronicStatusPresentation("Electronic", "Draft", tEn).label).toBe("Not issued");
    expect(salesElectronicStatusPresentation("Electronic", "Authorized", tEn).label).toBe(
      "Authorized by SRI",
    );
  });
});
