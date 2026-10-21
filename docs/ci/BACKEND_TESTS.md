# Backend CI: distribución y resultados

Clasificación: STRUCTURAL DEBT. Implementación local; pendiente de validar la matriz completa en GitHub Actions.

`ci.yml` llama una sola vez a `backend-ci.yml`. El manifiesto
[`backend-test-groups.json`](../../tools/ci/backend-test-groups.json) es la fuente de la matriz:

| Grupo | Runner | Selección | Casos descubiertos localmente |
|---|---|---|---:|
| domain | Ubuntu | ERP.Domain.Tests completo | Sin filtro |
| application | Ubuntu | ERP.Application.Tests completo | Sin filtro |
| architecture | Ubuntu | ERP.Architecture.Tests completo | Sin filtro |
| api | Ubuntu | ERP.API.Tests completo | Sin filtro |
| infrastructure-01 | Ubuntu | 20 clases explícitas | 174 |
| infrastructure-02 | Ubuntu | 21 clases explícitas | 173 |
| infrastructure-03 | Ubuntu | 22 clases explícitas | 207 |
| infrastructure-04 | Ubuntu | 24 clases explícitas | 171 |
| infrastructure-05 | Ubuntu | 23 clases explícitas | 179 |
| infrastructure-06 | Ubuntu | 25 clases explícitas | 169 |
| infrastructure-07 | Ubuntu | 34 clases explícitas | 334 |
| infrastructure-08 | Ubuntu | 35 clases explícitas | 332 |
| ride-golden | Windows | RideGoldenFileTests completo | 6 |

Inventario local Debug: **1745 casos / 205 clases Infrastructure**, distribuidos sin solapamiento.
Las cantidades son una observación, no una allowlist de casos: nuevos métodos de clases existentes
se incluyen automáticamente; clases nuevas requieren asignación explícita. Los grupos se equilibraron
inicialmente por casos descubiertos, dando mayor peso a clases con PostgreSQL/fixtures asíncronas.
No son tiempos medidos en GitHub. Se conserva `maxParallelThreads: 2` / `conservative` por ensamblado.

Cada job mantiene **25 minutos**, con hasta seis jobs de la matriz concurrentes y `fail-fast: false`.
Los cuatro proyectos pequeños no tienen filtros. No se usa `Category=PostgreSql`: los traits no
cubren todas las clases que utilizan contenedores. PostgreSQL/Redis de integración siguen siendo
los recursos efímeros de Testcontainers; no se suministran conexiones de desarrollo o producción.

## Cobertura verificable

[`backend_tests.py`](../../tools/ci/backend_tests.py), sin paquetes Python externos:

1. Comprueba que todos los proyectos `.Tests` de `ERP.slnx` figuren en el manifiesto y que cada
   clase Infrastructure tenga un único grupo. Solo los seis golden se asignan a Windows.
2. Restaura y compila únicamente el proyecto del job en Release.
3. Descubre todas las pruebas del ensamblado sin ejecutarlas. Infrastructure falla ante clases
   sin asignar, entradas obsoletas o un número de golden distinto de seis.
4. Contrasta el filtro real de VSTest con el inventario esperado del grupo antes de ejecutarlo.
5. Contrasta los métodos/casos del TRX con el descubrimiento; teorías expandidas durante ejecución
   pueden añadir casos, pero ningún caso descubierto puede desaparecer. Resultados vacíos,
   ausentes, fallidos, omitidos o incompletos fallan el reporte. Un fallo de test conserva su exit code.

Los golden existentes se conservan byte a byte. Diagnóstico CI #972: Windows 6/6 PASS y Linux 6/6
FAIL con el mismo ensamblado; las diferencias son de métricas de fuentes/posiciones de glifos del
render nativo, reproducibles por plataforma, no de fechas normalizadas ni texto documental.

## CI #978 — BUG REAL (2026-10-21)

Reproducido el inventario con nueve clases SriCatalogs sin asignar y ninguna obsoleta.
Asignadas en el manifiesto existente a los grupos 01–06; 07–08 conservan su carga.
La clase `SriIceTable18CatalogTests` aporta 40 casos y permanece indivisible en 03.
Validación focal: 17/17 pruebas del distribuidor y descubrimiento/filtros de los ocho
grupos Infrastructure, sin ejecutar pruebas funcionales; `unassigned=[]`, `obsolete=[]`.

## Evidencia por job

El artefacto `backend-<grupo>` (14 días) contiene `tests.trx`, `inventory.json`, logs de
restore/build/discovery/test, `timings.json` y `summary.md`. El resumen de Actions publica tiempos
de pared por etapa, outcomes y las 20 pruebas más lentas según TRX. Se intenta publicar incluso
si el job falla; una cancelación puede dejar solo logs parciales y nunca representa PASS.

Los tiempos por prueba del adaptador no sustituyen el tiempo de pared de fixtures, Docker y build.
Contexto anterior: [705 pruebas Testcontainers en 55 min 56 s](../QA/TESTCONTAINERS-FULL-REGRESSION-STABILITY.md).

## Publicación única tras los gates

`build-and-deploy.yml` es exclusivamente reutilizable. `ci.yml` lo llama después de backend,
frontend y seguridad (arquitectura ya es dependencia); conserva las ramas de publicación
main/develop y PR hacia main. También puede alcanzarse con el dispatch manual de CI.
Publica la API sin volver a ejecutar pruebas. Azure sigue condicionado a push en main y
`AZURE_WEBAPP_NAME`; se pasa únicamente el secret de publicación existente.
Los controles de arquitectura/seguridad permanecen en sus workflows; el guard Platform continúa
en arquitectura y frontend, sin la tercera repetición del antiguo backend monolítico.

## Validación local y pendiente

```powershell
python -m unittest discover -s tools/ci -p test_backend_tests.py -v
python tools/ci/backend_tests.py --matrix
# Descubrimiento únicamente, con un ensamblado Debug ya compilado:
python tools/ci/backend_tests.py --group infrastructure-01 --configuration Debug --no-build --verify-only
# Ejecución focal Windows, sin reconstruir:
python tools/ci/backend_tests.py --group ride-golden --configuration Debug --no-build
python tools/ci/backend_tests.py --group ride-golden --report
```

Validado localmente: 17/17 pruebas de los guards del ejecutor; descubrimiento real y filtros de
los ocho grupos Infrastructure (1671 casos con los golden); restore/build Release Windows y
golden 6/6 PASS, con TRX y resumen de tiempos. `actionlint` sobre todos los workflows y
`git diff --check`: PASS. No se ejecutó la suite completa localmente.

Pendiente en GitHub: restore/build Release en ambos
runners, ejecución de los 13 grupos, sus tiempos reales bajo 25 minutos, artifacts/resúmenes y
encadenamiento de publicación. Los fallos ajenos a RIDE ya observados en #972 no se corrigen
con esta organización: seguirán reportándose como fallos, sin ocultarlos ni saltarlos.
