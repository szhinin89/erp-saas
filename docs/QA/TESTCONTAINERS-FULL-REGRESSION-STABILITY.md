# Testcontainers — estabilidad de full backend regression

Fecha: 2026-10-05 (America/Guayaquil).
Clasificación: **QA/INFRA DEBT; NO PILOT BLOCKER**.

## Causa y alcance

Infrastructure.Tests usa xUnit 2.9.3 y Testcontainers 4.3.0. No tenía collections ni configuración de concurrencia. xUnit crea una collection por clase y, en este equipo de 16 procesadores lógicos, ejecutaba hasta 16 collections concurrentes. El inventario detectó 93 clases de tests con dependencia de containers y 705 casos descubiertos; varias clases no tienen trait Category.

La mayoría de las suites crea un PostgreSQL por instancia de test: `InitializeAsync` ejecuta `StartAsync` y luego migraciones/seeds; `DisposeAsync` elimina el container. Las suites Communications y SriRetentionCatalogResolution ya usan `IClassFixture`: comparten un container dentro de una clase, pero no entre clases. No hay `ICollectionFixture` ni collections explícitas en Infrastructure.Tests.

El baseline reprodujo 12 `System.TimeoutException` en operaciones Docker durante arranque/readiness, incluidas `DockerSystemOperations.GetIsWindowsEngineEnabled` y `DockerClient.PrivateMakeRequestAsync`. El pico observado fue 20 containers de tests más un Ryuk. El pico puede superar los slots de ejecución cuando hay recursos todavía vivos después de fallar un arranque. En seis clases la limpieza de otro recurso precede a la del container sin `finally`; ampliar ese refactor queda fuera del cambio mínimo mientras no sea necesario para la validación.

Readiness PostgreSQL conserva el `pg_isready --host localhost --dbname ... --username ...` del [módulo 4.3.0](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.3.0/src/Testcontainers.PostgreSql/PostgreSqlBuilder.cs). Redis conserva la espera del log `Ready to accept connections`. El [WaitStrategy 4.3.0](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.3.0/src/Testcontainers/Configurations/WaitStrategies/WaitStrategy.cs) usa por defecto intervalo de 1 s y timeout de 1 h; no había overrides de entorno ni `.testcontainers.properties`. Los fallos observados son timeouts de peticiones a Docker propagados durante el arranque, no evidencia de que PostgreSQL necesite ampliar ese timeout de readiness.

## Cambio mínimo

Solo configuración de tests:

- `backend/src/ERP.Infrastructure.Tests/xunit.runner.json`: `maxParallelThreads: 2` y `parallelAlgorithm: conservative`.
- `ERP.Infrastructure.Tests.csproj`: copia la configuración al directorio de salida.

La política limita el ensamblado Infrastructure.Tests completo, incluidos sus tests unitarios; evita anotar 93 clases o depender de traits incompletos. No impone un límite global a otros ensamblados ni a ejecuciones independientes. El algoritmo conservador incluye la espera asíncrona en los slots y evita abrir trabajo adicional mientras los tests activos esperan Docker.

Se conservan el aislamiento de bases de datos, los fixtures existentes, las pruebas de concurrencia internas y las estrategias de readiness. No se cambian timeouts, imágenes, versiones, assertions ni código productivo. No se desactiva ningún test ni se convierte ningún failure en skip. No hay commit ni push.

Referencia de la política: [documentación oficial de paralelismo xUnit](https://xunit.net/docs/running-tests-in-parallel).

## Evidencia y validación

Los logs, TRX y scripts temporales de `.qa-testcontainers/` se eliminaron a solicitud del usuario; sus resultados quedan consolidados en este informe.

| Etapa | Resultado | Tiempo de pared | Pico de containers de tests |
|---|---|---:|---:|
| Baseline Infrastructure, sin límite | 12 timeouts registrados; diagnóstico detenido, sin resultado completo | 478,54 s | 20 + 1 Ryuk |
| Los 12 casos fallidos, uno por ejecución | 12/12 passed; 0 failed; 0 skipped | 402,11 s acumulados | 1 + 1 Ryuk por ejecución |
| Grupo Testcontainers | 705/705 passed; 0 failed; 0 skipped | 3356,00 s (55 min 56 s) | 2 + 1 Ryuk |
| Full backend regression | Detenida limpiamente a solicitud del usuario; sin fallos registrados; Infrastructure incompleto | Última muestra: 1760,47 s (29 min 20 s) | 22 globales; Infrastructure mantuvo 2 |

El baseline se detuvo después de reproducir los timeouts, no se presenta como una regresión completa ni se comparan sus 478,54 s con la duración de una suite terminada. El muestreo solicita pausas de 500 ms; el tiempo de cada llamada Docker aumenta el intervalo efectivo. El grupo y la full regression también registran eventos para comprobar el pico y la eliminación de containers. Los servicios locales preexistentes `postgreszh` y `erp-saas-redis` se contabilizan aparte.

El grupo se seleccionó mediante los nombres de las 93 clases inventariadas, incluidos consumidores de fixtures, para cubrir suites sin Category. La full regression ejecutó `backend/src/ERP.slnx` sin filtro ni serialización adicional de proyectos. Los cuatro proyectos terminados tuvieron cero failures y cero skips:

| Proyecto | Passed | Tiempo TRX |
|---|---:|---:|
| Domain | 1310 | 14,49 s |
| Application | 2613 | 63,67 s |
| Architecture | 143 | 86,54 s |
| API | 962 | 434,28 s |

Son 5028 tests aprobados en la ejecución parcial. El pico global de 22 incluye otros productores de containers: API y un PostgreSQL puntual de Application. La configuración de concurrencia de esos proyectos no se modificó. El límite de dos está validado para Infrastructure, no es un límite global de la solución.

Grupo aprobado: los eventos registraron 633 containers iniciados y 633 terminados. La full regression se canceló mediante señal de consola a solicitud del usuario, sin ejecutar más suites. Se comprobó la terminación del proceso de tests y sus auxiliares; `docker ps -a --filter label=org.testcontainers=true` quedó vacío. Solo permanecieron los servicios locales preexistentes `postgreszh` y `erp-saas-redis`.

Resultado final: estabilización de Infrastructure validada por los 12 casos aislados y el grupo 705/705; full regression parcial sin fallos, detenida por decisión del usuario. No se declara una full regression completada.
