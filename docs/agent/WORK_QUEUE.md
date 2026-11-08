# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Producción NO-GO. Estado y gates en
  [STATUS](../../STATUS.md).
- ACCOUNTING-CHART-INTEGRITY-01 (PENDING: diagnóstico específico pendiente; sin reglas nuevas
  autorizadas): `UpdateAccountHandler` permite quitar `AllowsPosting` a una cuenta referenciada por
  una Regla de Contabilización activa (desactivarla sí se bloquea) y no impide que una cuenta con
  hijas admita movimiento ni desactivar una agrupadora con hijas activas. El fallo aparece después,
  al contabilizar (`PostingAccountGuard`). El aviso informativo del modal del Plan de cuentas
  (ACCOUNTING-CHART-UX-01) no resuelve el riesgo ni sustituye validaciones del servidor.
