import { beforeEach } from "vitest";
import { TEST_PRECISION_POLICY } from "./precisionPolicyFixture";

// Cada test parte con una política cargada (como en la app real tras el bootstrap de sesión).
// Import dinámico: así el módulo se resuelve DESPUÉS de los vi.mock del archivo de test (un import
// estático aquí cargaría antes axios/api reales y rompería los tests que los mockean).
beforeEach(async () => {
  try {
    const config = await import("../lib/config/precisionPolicy.config");
    config.setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY });
  } catch {
    // Tests que sustituyen por completo el módulo de configuración no necesitan la política real.
  }
});
