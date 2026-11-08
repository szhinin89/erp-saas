# Cola de trabajo abierto autorizado

Autoridad de entrega y gates: [STATUS](../../STATUS.md). Reglas y responsables:
[flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
No incluye entregas terminadas ni autoriza desarrollos por mencionarlos.

- SEC-A01 (requisitos de despliegue; implementación CLOSED): antes de producción, verificar IP
  real tras el proxy y proxies confiables, Redis e instancias API, medir BCrypt y fijar límites,
  concurrencia y cola, y evaluar el bloqueo temporal dirigido. Producción NO-GO. Estado y gates en
  [STATUS](../../STATUS.md).
- Aceptación ampliada de ProductPicker (PENDING; separado de PURCHASES-PRODUCT-PICKER-RACE-01,
  CLOSED con CI #1126 SUCCESS): ejecución ampliada de 63 archivos por el propietario sin la
  intermitencia previa; aún no acreditada. Cierre y evidencia en [STATUS](../../STATUS.md).
- ITEMS-LOOKUP-PICKER-FOCUS-RACE-01 (PENDING; Items FROZEN, no reabierto): `ItemLookupPicker`
  reinicia `focusIdx` con un efecto pasivo sobre `results`, mismo patrón de carrera que
  PURCHASES-PRODUCT-PICKER-RACE-01; requiere reapertura expresa de Items, reproducción determinista
  y corrección en el componente. Registro en [STATUS](../../STATUS.md).
- PURCHASES-RECEPTION-SRI-TXT-VALIDATION-01 (PENDING de aceptación funcional): prueba de extremo a
  extremo de `/purchases/reception` con un TXT auténtico autorizado del SRI, en entorno de pruebas,
  sin fabricar datos ni escribir en producción. Bloqueada hasta cerrar PURCHASES-RECEPTION-SECURITY-01 y
  PURCHASES-RECEPTION-SRI-QUERY-ENV-01 ([STATUS](../../STATUS.md)); hasta entonces no se importan TXT
  auténticos ni se consulta el SRI real.
- PURCHASES-RECEPTION-SECURITY-01 (IMPLEMENTED / PENDING CI; reapertura de Compras solo para H1–H4):
  RECEIVER-RUC-01 (receptor del TXT), CROSS-COMPANY-KEY-01 (conflicto de clave entre empresas),
  WRITE-PERMISSIONS-01 (permisos de escritura y UI) y SUPPLIER-RULE-01 (regla única del proveedor).
  Pendiente: revisión técnica, publicación, CI y revisión de perfiles antes de desplegar. Evidencia
  y riesgos en [STATUS](../../STATUS.md).
- PURCHASES-MANUAL-SUPPLIER-ROLE-ACTIVE-01 (PENDING; Compras CLOSED, requiere reapertura): guardar
  una compra (alta y actualización del borrador, también manual) no exige rol Proveedor activo.
  Registro en [STATUS](../../STATUS.md); sin trabajo abierto.
- PURCHASES-RECEPTION-SRI-QUERY-ENV-01 (PENDING; ADR-023 congelado): evaluación de la opción A registrada
  en [STATUS](../../STATUS.md); ninguna consulta real hasta demostrar aislamiento de emisión,
  certificados, permisos y red.
- PURCHASES-RECEPTION-XML-ATTEMPT-01 (PENDING; requiere backend y migración): historial persistente
  de intentos y resultados de consulta XML. Antes de intervenir el handler congelado, revisar
  ADR-028/ADR-046, migraciones, privacidad y aislamiento; esta cola no autoriza alterar contratos
  fiscales protegidos.
- CI-SELECTIVE-ARCHITECTURE-01 (PENDING): retirar una excepción de
  `tools/architecture/architecture-grandfather.json` clasifica hoy como perfil completo (ruta fuera
  de las listas de `ci_changes.mjs`; p. ej. CI #1121 sobre `bd816be40`). Evitar el FULL innecesario
  conservando fail-closed y probando los perfiles frontend, backend, mixed, full y documentation.
  Relacionado con la evidencia pendiente de CI-SELECTIVE-01 en [STATUS](../../STATUS.md).
