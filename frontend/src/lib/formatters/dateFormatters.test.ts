import {
  describe,
  it,
  expect,
  vi,
  beforeAll,
  afterAll,
  afterEach,
} from "vitest";
import {
  todayIso,
  toLocalIsoDate,
  toDateTimeLocalInputValue,
} from "./dateFormatters";

// Regresión: SRI [65] FECHA EMISIÓN EXTEMPORÁNEA. Causa raíz confirmada con evidencia real
// (facturas 001-500-000000012 y 001-500-000000016): todayIso() usaba toISOString(), que
// siempre convierte a UTC — entre las 19:00 y 23:59 hora Ecuador (UTC-5) UTC ya cruzó a
// mañana, así que el formulario de Nueva Venta precargaba la fecha equivocada.
describe("todayIso", () => {
  const originalTz = process.env.TZ;

  beforeAll(() => {
    process.env.TZ = "America/Guayaquil"; // UTC-5, sin horario de verano
  });

  afterAll(() => {
    process.env.TZ = originalTz;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("devuelve la fecha calendario local (Ecuador), no la fecha UTC, cuando UTC ya cruzó a mañana", () => {
    // 2026-07-13 22:00 hora Ecuador == 2026-07-14 03:00 UTC.
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-07-14T03:00:00.000Z"));
    expect(todayIso()).toBe("2026-07-13");
  });

  it("devuelve la fecha en formato yyyy-MM-dd con ceros a la izquierda", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-05T17:00:00.000Z")); // 12:00 hora Ecuador
    expect(todayIso()).toBe("2026-01-05");
  });

  it("formatea fechas de negocio con el calendario local del dispositivo", () => {
    const localBusinessDate = new Date("2026-07-13T22:00:00-05:00");
    expect(toLocalIsoDate(localBusinessDate)).toBe("2026-07-13");
  });
});

// Regresión: "Fecha y hora autorización" en Compras aparecía vacía aunque el dato sí
// venía del XML/API — <input type="datetime-local"> exige exactamente yyyy-MM-ddTHH:mm
// y el valor crudo (ISO UTC, ISO sin offset, o dd/MM/yyyy HH:mm[:ss] del SRI/XML) nunca
// coincide con ese formato, así que el navegador lo descarta silenciosamente.
describe("toDateTimeLocalInputValue", () => {
  const originalTz = process.env.TZ;

  beforeAll(() => {
    process.env.TZ = "America/Guayaquil"; // UTC-5, sin horario de verano
  });

  afterAll(() => {
    process.env.TZ = originalTz;
  });

  it("normaliza un ISO UTC con Z", () => {
    expect(toDateTimeLocalInputValue("2026-08-01T06:27:48Z")).toBe(
      "2026-08-01T06:27",
    );
  });

  it("normaliza el formato SRI/XML dd/MM/yyyy HH:mm:ss", () => {
    expect(toDateTimeLocalInputValue("01/08/2026 06:27:48")).toBe(
      "2026-08-01T06:27",
    );
  });

  it("normaliza el formato SRI/XML dd/MM/yyyy HH:mm (sin segundos)", () => {
    expect(toDateTimeLocalInputValue("01/08/2026 06:27")).toBe(
      "2026-08-01T06:27",
    );
  });

  it("devuelve vacío cuando no hay dato, sin inventar una fecha", () => {
    expect(toDateTimeLocalInputValue(null)).toBe("");
    expect(toDateTimeLocalInputValue(undefined)).toBe("");
    expect(toDateTimeLocalInputValue("")).toBe("");
  });

  it("devuelve vacío en vez de romper cuando el valor no se puede interpretar", () => {
    expect(toDateTimeLocalInputValue("no-es-una-fecha")).toBe("");
  });

  // Mismo comportamiento que parseIso() en formatDate/formatDateTime: un ISO SIN
  // offset ("...T06:27", sin "Z") se interpreta como hora LOCAL del entorno antes de
  // leerlo con getUTC*(), así que el resultado se desplaza según la zona horaria del
  // proceso (aquí America/Guayaquil, UTC-5) — no es una fecha nueva, es la misma
  // ambigüedad ya existente en el resto del formateo de fechas de la app.
  it("interpreta un ISO sin offset como hora local del entorno, igual que formatDate/formatDateTime", () => {
    expect(toDateTimeLocalInputValue("2026-08-01T06:27:00")).toBe(
      "2026-08-01T11:27",
    );
  });
});
