# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Producción NO-GO. Estado y gates en
  [STATUS](../../STATUS.md).
- ACCOUNTING-CHART-INTEGRITY-01 (IMPLEMENTED / PENDING CI): invariantes I1–I4 aprobadas por el
  propietario (2026-11-08) e implementadas en el servidor; pendiente la publicación y su CI.
  Riesgos residuales en [STATUS](../../STATUS.md): concurrencia con otros módulos, vínculo de forma
  de pago sin desvinculación directa y diagnóstico de datos antes de desplegar en otras bases.
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
