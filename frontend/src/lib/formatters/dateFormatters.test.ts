import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  DEFAULT_COMPANY_TIME_ZONE,
  addDaysIso,
  companyDayUtcRange,
  firstDayOfMonthIso,
  formatDate,
  formatDateTime,
  fromDateTimeLocalInputValue,
  getCompanyTimeZone,
  isValidIsoDate,
  setCompanyTimeZone,
  toDateTimeLocalInputValue,
  todayIso,
} from "./dateFormatters";

/**
 * ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — contrato temporal del frontend.
 * `process.env.TZ` simula la zona del NAVEGADOR; `setCompanyTimeZone` la de la EMPRESA.
 * Todo resultado debe depender solo de la empresa (o de ninguna zona, para fechas de negocio).
 */
const ZONES = [
  "America/Guayaquil",
  "UTC",
  "America/New_York",
  "Europe/Madrid",
] as const;

const originalTz = process.env.TZ;

function useZones(browserTz: string, companyTz: string) {
  process.env.TZ = browserTz;
  setCompanyTimeZone(companyTz);
}

afterEach(() => {
  vi.useRealTimers();
  process.env.TZ = originalTz;
  setCompanyTimeZone(DEFAULT_COMPANY_TIME_ZONE);
});

describe("BUSINESS DATE invariance — '2026-09-25' nunca se corre de día", () => {
  for (const browserTz of ZONES) {
    for (const companyTz of ZONES) {
      it(`navegador ${browserTz} / empresa ${companyTz}`, () => {
        useZones(browserTz, companyTz);
        expect(formatDate("2026-09-25")).toBe("25/09/2026");
        expect(formatDateTime("2026-09-25")).toBe("25/09/2026");
        expect(addDaysIso("2026-09-25", 0)).toBe("2026-09-25");
        expect(addDaysIso("2026-09-25", 30)).toBe("2026-10-25");
        expect(isValidIsoDate("2026-09-25")).toBe(true);
      });
    }
  }

  it("aritmética de calendario pura: fin de mes, bisiesto y día de cambio DST del navegador", () => {
    useZones("America/New_York", "America/Guayaquil");
    expect(addDaysIso("2026-12-31", 1)).toBe("2027-01-01");
    expect(addDaysIso("2028-02-28", 1)).toBe("2028-02-29");
    expect(addDaysIso("2026-03-07", 1)).toBe("2026-03-08");
    expect(addDaysIso("2026-03-08", 1)).toBe("2026-03-09");
    expect(addDaysIso("no-es-fecha", 1)).toBe("");
    expect(isValidIsoDate("2026-02-30")).toBe(false);
  });
});

// Regresión SRI [65] FECHA EMISIÓN EXTEMPORÁNEA: el "hoy" de negocio nunca es el día UTC.
describe("todayIso — hoy de la empresa (Ecuador 18:59 / 19:00 / 23:59 del día 25)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  for (const browserTz of ZONES) {
    it.each([
      ["18:59", "2026-09-25T23:59:00Z"],
      ["19:00", "2026-09-26T00:00:00Z"],
      ["23:59", "2026-09-26T04:59:00Z"],
    ])(`navegador ${browserTz}: %s hora Ecuador → 2026-09-25`, (_local, utcNow) => {
      useZones(browserTz, "America/Guayaquil");
      vi.setSystemTime(new Date(utcNow));
      expect(todayIso()).toBe("2026-09-25");
    });
  }

  it("firstDayOfMonthIso usa el mes de la empresa, no el UTC (30/09 23:59 Ecuador)", () => {
    useZones("UTC", "America/Guayaquil");
    vi.setSystemTime(new Date("2026-10-01T04:59:00Z"));
    expect(todayIso()).toBe("2026-09-30");
    expect(firstDayOfMonthIso()).toBe("2026-09-01");
  });
});

describe("UTC INSTANT round-trip — cero drift", () => {
  for (const browserTz of ZONES) {
    it(`empresa America/Guayaquil, navegador ${browserTz}: 25/09/2026 14:38 ↔ 19:38Z`, () => {
      useZones(browserTz, "America/Guayaquil");
      const utc = fromDateTimeLocalInputValue("2026-09-25T14:38");
      expect(utc).toBe("2026-09-25T19:38:00Z");
      expect(toDateTimeLocalInputValue(utc)).toBe("2026-09-25T14:38");
      expect(formatDateTime(utc)).toBe("25/09/2026 14:38:00");
    });
  }

  it("editar/guardar 100 veces sin tocar el campo conserva exactamente el mismo instante", () => {
    useZones("Europe/Madrid", "America/Guayaquil");
    let stored = "2026-09-25T19:38:00Z";
    for (let i = 0; i < 100; i += 1) {
      const formValue = toDateTimeLocalInputValue(stored);
      stored = fromDateTimeLocalInputValue(formValue)!;
    }
    expect(stored).toBe("2026-09-25T19:38:00Z");
  });

  it("formatDateTime presenta en Company.Timezone, no UTC crudo", () => {
    useZones("UTC", "America/Guayaquil");
    expect(formatDateTime("2026-09-25T19:38:00.1234567Z")).toBe("25/09/2026 14:38:00");
  });

  it("formatDate(instante) toma el día de la empresa, no los 10 primeros caracteres (día UTC)", () => {
    useZones("UTC", "America/Guayaquil");
    // 26/09 02:00Z == 25/09 21:00 hora Ecuador.
    expect(formatDate("2026-09-26T02:00:00Z")).toBe("25/09/2026");
  });

  it("valores sin dato / inválidos no inventan fechas", () => {
    expect(formatDate(null)).toBe("—");
    expect(formatDateTime("no-es-fecha")).toBe("—");
    expect(toDateTimeLocalInputValue(null)).toBe("");
    expect(toDateTimeLocalInputValue("")).toBe("");
    expect(toDateTimeLocalInputValue("no-es-una-fecha")).toBe("");
    expect(fromDateTimeLocalInputValue("")).toBeNull();
    expect(fromDateTimeLocalInputValue("no-es-fecha")).toBeNull();
  });

  it("hora de pared ya local (SRI dd/MM/yyyy HH:mm[:ss] o ISO sin zona) no se reinterpreta", () => {
    useZones("UTC", "America/Guayaquil");
    expect(toDateTimeLocalInputValue("01/08/2026 06:27:48")).toBe("2026-08-01T06:27:48");
    expect(toDateTimeLocalInputValue("01/08/2026 06:27")).toBe("2026-08-01T06:27");
    expect(toDateTimeLocalInputValue("2026-08-01T06:27:00")).toBe("2026-08-01T06:27");
    expect(fromDateTimeLocalInputValue("01/08/2026 06:27:48")).toBe("2026-08-01T11:27:48Z");
  });
});

describe("DST — Company.Timezone con horario de verano", () => {
  it("America/New_York: verano (UTC-4) e invierno (UTC-5)", () => {
    useZones("America/Guayaquil", "America/New_York");
    expect(fromDateTimeLocalInputValue("2026-07-01T10:00")).toBe("2026-07-01T14:00:00Z");
    expect(fromDateTimeLocalInputValue("2026-12-01T10:00")).toBe("2026-12-01T15:00:00Z");
    expect(formatDateTime("2026-07-01T14:00:00Z")).toBe("01/07/2026 10:00:00");
    expect(formatDateTime("2026-12-01T15:00:00Z")).toBe("01/12/2026 10:00:00");
  });

  it("hora inexistente (salto 08/03/2026 02:30) → null; ambigua (01/11/2026 01:30) → horario estándar", () => {
    useZones("UTC", "America/New_York");
    expect(fromDateTimeLocalInputValue("2026-03-08T02:30")).toBeNull();
    expect(fromDateTimeLocalInputValue("2026-11-01T01:30")).toBe("2026-11-01T06:30:00Z");
  });

  it("Europe/Madrid: verano (UTC+2)", () => {
    useZones("America/Guayaquil", "Europe/Madrid");
    expect(fromDateTimeLocalInputValue("2026-07-01T10:00")).toBe("2026-07-01T08:00:00Z");
    expect(toDateTimeLocalInputValue("2026-07-01T08:00:00Z")).toBe("2026-07-01T10:00");
  });
});

describe("companyDayUtcRange — día de empresa → [inicio, fin) UTC", () => {
  it("America/Guayaquil", () => {
    useZones("Europe/Madrid", "America/Guayaquil");
    expect(companyDayUtcRange("2026-09-25")).toEqual({
      startUtc: "2026-09-25T05:00:00Z",
      endUtcExclusive: "2026-09-26T05:00:00Z",
    });
  });

  it("día de cambio DST dura 23h (America/New_York 08/03/2026)", () => {
    useZones("UTC", "America/New_York");
    expect(companyDayUtcRange("2026-03-08")).toEqual({
      startUtc: "2026-03-08T05:00:00Z",
      endUtcExclusive: "2026-03-09T04:00:00Z",
    });
  });
});

describe("setCompanyTimeZone", () => {
  it("zona vacía o inválida cae al default nacional", () => {
    setCompanyTimeZone("Zona/Inexistente");
    expect(getCompanyTimeZone()).toBe(DEFAULT_COMPANY_TIME_ZONE);
    setCompanyTimeZone("");
    expect(getCompanyTimeZone()).toBe(DEFAULT_COMPANY_TIME_ZONE);
    setCompanyTimeZone("Europe/Madrid");
    expect(getCompanyTimeZone()).toBe("Europe/Madrid");
  });
});

// ZH-TEMPORAL-DATETIME-SECONDS-02I — única salida visual de instantes: dd/MM/yyyy HH:mm:ss.
describe("formatDateTime — dd/MM/yyyy HH:mm:ss en Company.Timezone", () => {
  for (const browserTz of ZONES) {
    it(`navegador ${browserTz}`, () => {
      useZones(browserTz, "America/Guayaquil");
      expect(formatDateTime("2026-09-25T19:38:27Z")).toBe("25/09/2026 14:38:27");
      expect(formatDateTime("2026-09-25T19:38:00Z")).toBe("25/09/2026 14:38:00");
      // Cambio de día: 26/09 02:30:15Z == 25/09 21:30:15 hora Ecuador.
      expect(formatDateTime("2026-09-26T02:30:15Z")).toBe("25/09/2026 21:30:15");
      // Fecha de negocio: sin hora, sin zona, sin desplazamiento.
      expect(formatDate("2026-09-25")).toBe("25/09/2026");
      expect(formatDateTime("2026-09-25")).toBe("25/09/2026");
    });
  }

  it("mismo instante en Europe/Madrid (UTC+2 en verano)", () => {
    useZones("America/Guayaquil", "Europe/Madrid");
    expect(formatDateTime("2026-09-25T19:38:27Z")).toBe("25/09/2026 21:38:27");
    expect(formatDateTime("2026-09-26T02:30:15Z")).toBe("26/09/2026 04:30:15");
  });

  it("datetime-local conserva los segundos reales y no inventa :00 en el dato persistido", () => {
    useZones("UTC", "America/Guayaquil");
    expect(toDateTimeLocalInputValue("2026-09-25T19:38:27Z")).toBe("2026-09-25T14:38:27");
    expect(fromDateTimeLocalInputValue(toDateTimeLocalInputValue("2026-09-25T19:38:27Z"))).toBe(
      "2026-09-25T19:38:27Z",
    );
    expect(toDateTimeLocalInputValue("2026-09-25T19:38:00Z")).toBe("2026-09-25T14:38");
  });
});
