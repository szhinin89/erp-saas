# ADR-046 — Excepción temporal para deuda histórica de tamaño de handlers C-03

## Status

Accepted — aprobado por el propietario para implementación controlada. Fecha: 2026-10-19.
Clasificación: STRUCTURAL DEBT (DEBT en el diagnóstico LOCAL-ERP-READINESS-01A.2).
Decisión aprobada para el guardrail C-03; no autoriza refactors de handlers.

## Context

La regla [C-03](../architecture/pr-rules-catalog.md#c-03--tamaño-máximo-de-handler)
limita a 150 líneas el cuerpo de `Handle`. El
[enforcement vigente](../architecture/enforcement.md#guardrails-automatizados)
delega su comprobación a `tools/quality/check-handler-size.ps1`.

El HEAD de referencia es `9afc6c56e7eaf1f87cbcc426a9e31f6a031ada77`.
El [CI de ese commit](https://github.com/szhinin89/erp-saas/actions/runs/39298026404/job/118215684235)
y la reproducción local identifican exactamente ocho infracciones. Sus conteos
son idénticos en el padre `0dff8efdb`: el arreglo del Stack Allowlist no las introdujo.
No se ha demostrado un defecto funcional ni un falso positivo del contador para
estos casos. Seguridad y módulos CLOSED/FROZEN desaconsejan un refactor incidental.

En el HEAD base, `handlerHandleMaxLines150` estaba vacío y admitía rutas sin
techo individual; incorporarlas directamente permitiría crecimiento ilimitado.
El checker Node de integridad consumía esa lista como cadenas; se adapta junto
con el consumidor PowerShell al formato con techos individuales.

## Decision

### Inventario cerrado y techos individuales

Rutas relativas al repositorio, sin comodines. Los valores usan exactamente el
contador vigente (líneas físicas desde la llave inicial hasta la final, incluidas
líneas vacías y comentarios); no se modifica la métrica en esta iniciativa.

| Archivo | Conteo en HEAD base / techo inicial | Primer cruce documentado |
|---|---:|---|
| `backend/src/ERP.Application/Modules/Access/UseCases/Permissions/UpsertProfilePermissionsHandler.cs` | 160 | `a94d4bf4f`, 2026-08-29 |
| `backend/src/ERP.Application/Modules/Auth/UseCases/Login/LoginHandler.cs` | 213 | `694b59cca`, 2026-07-29 |
| `backend/src/ERP.Application/Modules/Auth/UseCases/RefreshToken/RefreshTokenHandler.cs` | 226 | `371821614`, 2026-09-02 |
| `backend/src/ERP.Application/Modules/ElectronicInvoicing/UseCases/ValidateSriConfiguration/ValidateSriConfigurationQueryHandler.cs` | 155 | `694b59cca`, 2026-07-29 |
| `backend/src/ERP.Application/Modules/InitialLoad/UseCases/ConfirmImportBatch/ConfirmImportBatchHandler.cs` | 155 | `4df361356`, 2026-10-09 |
| `backend/src/ERP.Application/Modules/Items/UseCases/CreateItem/CreateItemCommandHandler.cs` | 191 | `694b59cca`, 2026-07-29 |
| `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/UseCases/DownloadPurchaseReceptionXml/DownloadPurchaseReceptionXmlHandler.cs` | 168 | `79ca594b0`, 2026-10-05 |
| `backend/src/ERP.Application/Modules/Settings/Operations/UseCases/UpdateOperationalPreferences/UpdateOperationalPreferencesCommandHandler.cs` | 451 | `79ca594b0`, 2026-10-05 |

Solo se exceptúa el tamaño del `Handle` existente en cada archivo. No se
exceptúan seguridad, aislamiento, transacciones, otros checks ni nuevos métodos
`Handle` añadidos al mismo archivo. No se autoriza reabrir módulos CLOSED/FROZEN.

### Representación y fuente ejecutable única

Convertir los elementos de la lista existente `handlerHandleMaxLines150` de rutas
a objetos. No crear otra lista de excepciones ni conservar cadenas que concedan
una dispensa ilimitada. Ejemplo del formato aprobado:

```json
{
  "file": "backend/src/ERP.Application/Modules/Auth/UseCases/Login/LoginHandler.cs",
  "maxHandleLines": 213,
  "baselineCommit": "9afc6c56e7eaf1f87cbcc426a9e31f6a031ada77",
  "adr": "docs/decisions/ADR-046-historical-handler-size-debt.md"
}
```

El JSON será la única configuración ejecutable de techos. La tabla de este ADR
registra la decisión original y su evidencia; no es una configuración alternativa.
La implementación inicial verificará que los ocho techos coincidan con el commit
base aprobado. Toda ampliación de inventario o aumento de techo requiere otra
decisión explícita; un fallo no autoriza recalibrar la línea base.

### Algoritmo aprobado

1. Validar toda la configuración antes de evaluar tamaños: ruta exacta y única,
   relativa, dentro de ERP.Application, sin `..`, comodines ni diferencias de caso;
   archivo existente y dentro del alcance del checker; techo entero mayor que 150;
   ADR existente y referencia de baseline válida. Rechazar cadenas legacy,
   metadatos incompletos y duplicados, incluso si el handler mide menos de 150.
2. Para un archivo no exceptuado, conservar la comprobación general de **todos**
   sus métodos `Handle`: cualquier conteo superior a 150 falla.
3. Para estos ocho archivos, exigir exactamente un `Handle` detectable, como en
   el baseline. Si desaparece, no se puede contar o aparece otro, fallar para
   revisión explícita; no extender la dispensa al máximo de varios métodos.
4. Si el conteo supera el techo individual, fallar con ruta, conteo, techo y ADR.
   Si está entre 151 y el techo, emitir advertencia visible de deuda aceptada.
5. Si alcanza 150 o menos, fallar por excepción obsoleta hasta retirarla en el
   mismo cambio. Después queda sometido exclusivamente al límite general.
6. Ejecutar en todo el árbol de Application en CI, también en pushes a main;
   nunca limitar el control a archivos cambiados ni omitirlo por ser CLOSED.
7. Un error de lectura/configuración/conteo debe fallar, nunca omitir el control.

El techo aprobado impide superar la línea base inicial. Una reducción parcial
permite todavía volver a crecer hasta ese techo; para cerrar ese margen, cada
remediación parcial debe reducir también `maxHandleLines` en el mismo cambio.
Este diseño mínimo no propone un ratchet automático contra ramas Git ni una
reescritura del parser C#; ambos aumentarían el alcance y las dependencias de CI.
La revisión del JSON sigue siendo necesaria: ningún checker almacenado en el
mismo repositorio sustituye la aprobación de sus propios cambios de política.

El reconocimiento de declaraciones admite modificadores, tipos de retorno
calificados/genéricos y firmas multilínea, enmascarando comentarios y literales
solo para reconocer firmas. El contador físico del cuerpo permanece sin cambios.
En archivos exceptuados, candidatos `Handle` no reconocidos con seguridad fallan
de forma conservadora; no se incorpora un parser C# ni dependencias nuevas.

### Remediación y criterios de retirada por excepción

No se fija una fecha artificial que fuerce intervenir un módulo cerrado. Revisar
la excepción al autorizar trabajo sobre el handler; mantener deuda visible hasta
su eliminación. El propietario debe aprobar expresamente cualquier intervención
en CLOSED/FROZEN; la excepción no concede esa autorización.

| Handler | Riesgo y condición específica antes de remediar |
|---|---|
| `ValidateSriConfigurationQueryHandler` | Validación SRI: preservar diagnósticos y resultados; candidato pequeño solo si se autoriza su intervención. |
| `UpsertProfilePermissionsHandler` | Seguridad: preservar antiescalamiento, alcance y asignación de permisos con regresiones focales. |
| `LoginHandler` | Seguridad: preservar credenciales, errores, sesión y contexto autenticado. |
| `RefreshTokenHandler` | Seguridad: preservar renovación, rechazo, alcance y contratos de tokens. |
| `UpdateOperationalPreferencesCommandHandler` | Impacto transversal: separar pasos por sección conservando orden, valores y persistencia. Su tamaño no justifica un rediseño de preferencias. |
| `ConfirmImportBatchHandler` | Flujos CLOSED: autorización específica y pruebas PostgreSQL reales de atomicidad, aislamiento e idempotencia de las cargas afectadas. |
| `CreateItemCommandHandler` | Items FROZEN / A1 CLOSED: autorización específica; preservar Company scope, naturaleza, unicidad y configuración tributaria. |
| `DownloadPurchaseReceptionXmlHandler` | Flujo congelado ADR-028: autorización específica; preservar evidencia XML, snapshot de líneas y transiciones/persistencia. |

Priorizar intervenciones autorizadas con menor superficie y pruebas existentes,
sin tratar el tamaño como evidencia de bug funcional. Seguridad requiere revisión
especializada; cargas, Items y recepción XML se posponen mientras no exista la
autorización exigida para CLOSED. PostgreSQL real es obligatorio cuando se
verifiquen invariantes de persistencia, concurrencia o transacciones.

Para **cada** fila, la retirada requiere `Handle <= 150`, regresiones focales
apropiadas PASS, eliminación de su objeto del JSON, guardrail e integridad PASS y
diff review. Si desaparece o cambia de ruta el handler, retirar la entrada vieja;
la nueva ruta vuelve al límite general y no hereda la excepción automáticamente.
Una reducción solo de comentarios o formato no demuestra una mejora estructural.

### Pruebas de aceptación del mecanismo

Fixtures aisladas; no cambiar los ocho handlers ni tocar servicios o bases reales
para probar exclusivamente este mecanismo de CI.

- Handler ordinario: 150 PASS, 151 FAIL, tanto nuevo como existente.
- Cada excepción: techo exacto PASS con advertencia; techo + 1 FAIL, sin importar
  que los demás handlers hayan disminuido. No se permite compensación entre archivos.
- Excepción a 151 PASS si no supera su techo; a 150/149 FAIL por entrada obsoleta;
  retirada la entrada, 150 PASS y 151 FAIL.
- Segundo `Handle`, método no detectable, archivo ausente, ruta renombrada,
  duplicada, fuera de alcance o con caso incorrecto: FAIL.
- JSON inválido, cadena legacy, techo ausente/no entero/<=150, ADR ausente o
  metadatos inválidos: FAIL. Sin excepciones, el límite general sigue funcionando.
- Verificar los ocho valores contra el HEAD base aprobado y rechazar una entrada
  adicional o un techo superior durante la revisión/prueba de activación.
- Mantener semántica del contador: CRLF/LF, firmas multilínea, comentarios y
  líneas vacías; reproducir los conteos del inventario sin cambiar la métrica.
- Integridad Node acepta objetos válidos y rechaza objetos malformados; las otras
  listas grandfather conservan su contrato. Runner transmite exit code no cero.
- Control real tras aprobación: los ocho generan advertencias, cero violaciones
  C-03; crecimiento simulado falla. Esto no garantiza que otros gates CI pasen.

### Archivos de implementación

- `tools/quality/check-handler-size.ps1`: validación y techos individuales.
- `tools/architecture/architecture-grandfather.json`: ocho objetos aprobados.
- `tools/architecture/check-grandfather-integrity.mjs`: forma específica para esta lista.
- `tools/architecture/check-grandfather-integrity.test.mjs`: validación de objetos.
- `tools/quality/check-handler-size.test.mjs`: fixtures temporales
  y ejecución del script real en PowerShell; integrado con el runner de pruebas
  existente, sin una segunda implementación del contador.
- `tools/quality/check-handler-size.initial.mjs`: comprobación inicial,
  ejecutada explícitamente con `node --test tools/quality/check-handler-size.initial.mjs`.
  Exige el inventario inicial exacto y ocho advertencias; no forma parte del runner
  permanente. Las pruebas permanentes permiten subconjuntos y techos menores que
  los autorizados, sin exigir conservar archivos ni código del commit histórico.
- `frontend/package.json`: fixtures conectadas al runner existente `architecture:check`,
  antes de `tools/architecture/run-all.mjs`, sin modificar workflows.
- `docs/architecture/pr-rules-catalog.md` (C-03) y
  `docs/architecture/enforcement.md`: referencia breve al régimen aprobado;
  `tools/architecture/README.md`: actualizar el contrato de la lista si lo describe.
- Este ADR y su índice registran la aprobación. Ningún archivo funcional del ERP cambia.

## Consequences

Se puede desbloquear C-03 sin refactorizar componentes sensibles, con deuda
visible, límites individuales y control estricto del resto. Los handlers siguen
siendo largos: no se acredita calidad funcional ni aptitud para piloto/despliegue.

El contador basado en texto tiene límites de análisis C#; se conserva para no
mezclar la excepción con otro proyecto técnico. Un defecto demostrado del
contador debe tratarse por separado. Los cambios de formato pueden consumir el
techo; no justifican ampliarlo automáticamente. Debe verificarse compatibilidad
Windows PowerShell 5.1 y PowerShell de Linux usado por CI.

La aprobación del ADR no acredita por sí sola un CI correcto. Implementada la excepción,
solo un CI verificado permite declarar el gate correspondiente satisfecho.

## Alternatives Considered

- Refactor inmediato de los ocho: elimina deuda, pero abre superficie de riesgo
  innecesaria e incluye módulos cerrados sin autorización de intervención.
- Lista de rutas sin techos: cambio menor, pero permite crecimiento; descartada.
- Aumentar 150, saltar C-03 o revisar solo el diff: permite incumplimientos nuevos;
  descartado.
- Mantener el bloqueo hasta una remediación autorizada: válido si se rechaza la
  excepción; no introduce riesgo funcional, pero impide desbloquear este gate.
- Baseline separado: duplica configuración de la misma excepción; se prefiere
  evolucionar la lista existente y su validador.
