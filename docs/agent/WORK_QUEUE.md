# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Producción NO-GO. Estado y gates en
  [STATUS](../../STATUS.md).
- ACCOUNTING-CHART-INTEGRITY-01 (PENDING: diagnóstico presentado al propietario el 2026-11-08;
  reglas nuevas pendientes de su decisión): `UpdateAccountHandler` permite quitar `AllowsPosting` a
  una cuenta referenciada por una Regla de Contabilización activa (desactivarla sí se bloquea) y no
  impide que una cuenta con hijas admita movimiento ni desactivar una agrupadora con hijas activas.
  `DisableAccountHandler` solo comprueba Reglas de Contabilización, no las demás configuraciones que
  apuntan a la cuenta. El fallo aparece después, al contabilizar (`PostingAccountGuard`). El aviso
  informativo del modal del Plan de cuentas (ACCOUNTING-CHART-UX-01) no resuelve el riesgo ni
  sustituye validaciones del servidor.
- PURCHASES-PRODUCT-PICKER-RACE-01 (PENDING; Compras CLOSED, reabierto solo para este defecto):
  corregir en `ProductPicker` la carrera del reinicio de `focusIdx` con la navegación por teclado y
  demostrarlo con pruebas deterministas, sin parches artificiales del test. Evidencia en
  [STATUS](../../STATUS.md).
- PURCHASES-RECEPTION-SRI-TXT-VALIDATION-01 (PENDING de aceptación funcional): prueba de extremo a
  extremo de `/purchases/reception` con un TXT auténtico autorizado del SRI, en entorno de pruebas,
  sin fabricar datos ni escribir en producción.
- PURCHASES-RECEPTION-XML-ATTEMPT-01 (PENDING; requiere backend y migración): historial persistente
  de intentos y resultados de consulta XML. Antes de intervenir el handler congelado, revisar
  ADR-028/ADR-046, migraciones, privacidad y aislamiento; esta cola no autoriza alterar contratos
  fiscales protegidos.
- CI-SELECTIVE-ARCHITECTURE-01 (PENDING): retirar una excepción de
  `tools/architecture/architecture-grandfather.json` clasifica hoy como perfil completo (ruta fuera
  de las listas de `ci_changes.mjs`; p. ej. CI #1121 sobre `bd816be40`). Evitar el FULL innecesario
  conservando fail-closed y probando los perfiles frontend, backend, mixed, full y documentation.
  Relacionado con la evidencia pendiente de CI-SELECTIVE-01 en [STATUS](../../STATUS.md).
