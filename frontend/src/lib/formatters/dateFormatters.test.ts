import {
  describe,
  it,
  expect,
  vi,
  beforeAll,
  afterAll,
  afterEach,
} from "vitest";
import { todayIso, toLocalIsoDate } from "./dateFormatters";

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
