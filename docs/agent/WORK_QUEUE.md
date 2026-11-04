# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- Caso #32: manejo de respuestas inciertas del SRI implementado; pendiente E2E integrada junto con
  #20, #30 y #31. No declarar CLOSED. Estado y gates en [STATUS](../../STATUS.md).
- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Producción NO-GO. Estado y gates en
  [STATUS](../../STATUS.md).
