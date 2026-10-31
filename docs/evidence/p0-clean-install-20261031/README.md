# P0 — instalación limpia ERP ZH V1 (2026-10-31)

**READY FOR REVIEW**. Instalación limpia PASS; sin error reproducido de instalación.
Infrastructure **84/84**, API **23/23**, Application **5/5**, cliente frontend **4/4**,
y cuatro scripts oficiales del launcher PASS. Cero fallos u omisiones en los grupos
backend. Pendiente gate/revisión del propietario; no CLOSED.

Base: `main@d462cddde19a0b8196cf75089f388a4316dae2ad`; árbol inicialmente limpio.
CI SUCCESS confirmado por el propietario en la autorización de esta tarea. La CLI `gh`
no tiene sesión y las consultas del conector a combined status y workflows de PR
devolvieron listas vacías; no se presenta una verificación remota independiente ni
se infiere un número de run. Las pruebas locales siguientes son evidencia separada.

## Destino y alcance

PostgreSQL **16.14**, imagen `postgres:16-alpine`, contenedor nuevo
`erp-p0-clean-d462cddde-20261031`, ID
`8688f80f1594d3aacfcae786d4983b6cfafb9ed49877c68c7ce07af9d241ec24`.
Destino exclusivo: `127.0.0.1:63780/erp_p0_clean_isolated`, usuario sintético
`p0_synthetic_owner`, sin montaje de directorios ni volúmenes preexistentes.
Antes de migrar: **0 tablas de aplicación**. Después: **217 tablas**, incluidas
`__EFMigrationsHistory` y **28 GLOBAL**. Conteo anterior al bootstrap de runtime.

Se aplicó la cadena oficial completa, sin editar migraciones:

1. `20261021162651_InitialProductionBaseline`.
2. `20261031012613_PhysicalDocumentAuthorization`.

Ambas figuran en el historial EF con versión 10.0.4. `database update` terminó con
exit 0; `has-pending-model-changes` terminó con exit 0 y
`No changes have been made to the model since the last migration.`
El primer SELECT del historial en una base vacía registra `Failed executing DbCommand`
antes de crear `__EFMigrationsHistory`; no impidió ninguna migración. El resultado
final y el historial persistido constan en [migrations.txt](migrations.txt),
[model.txt](model.txt) y [database.txt](database.txt).

## Cobertura y límites

- GLOBAL: consultas de todas sus tablas y flags persistidos; General `01` → factura
  `01` permanece **inactiva**. Negocio Popular `03` → nota `02` tiene fila de relación
  activa, pero el tipo documental `02` permanece **inactivo y no electrónico**;
  esto no habilita su emisión. No se cambió ninguna regla fiscal.
- La prueba oficial `Fresh_install_loads_only_the_inec_cge_2026_geography_once`
  aplica migraciones y el bootstrap INEC real, con comprobación de idempotencia y
  25 provincias / 224 cantones / 1048 parroquias. El contenedor de la consulta SQL
  directa solo ejecutó EF; la geografía se valida en el contenedor de esa prueba.
- First Run se valida con las pruebas existentes enlazadas por la
  [SSOT](../../architecture/first-run.md#pruebas-que-garantizan-la-implementación):
  DI/provisioning real, migraciones, transacción atómica, exclusividad, rollback,
  invalidación y reinicio. Las pruebas HTTP arrancan el ejecutable API real,
  esperan `/health/live`, comprueban `/api/v1/setup/status` y el alta oficial
  `/api/v1/setup/admin`, con valores sintéticos solo en sus contenedores efímeros.
  No se obtuvo ningún token ni credencial de una instalación existente.
- Consulta operativa: las siete pruebas `CompanyOperationalReadinessBranchHttpTests`
  usan pipeline HTTP y PostgreSQL real recién migrado; comprueban HTTP 200 de
  `/api/v1/companies/operational-readiness`, aislamiento empresa/sucursal y
  `physicalEmission.docTypeNotPermitted` incluso con autorización física vigente.
  La configuración fiscal incompleta se devuelve como requisito, sin error SQL.
  Este fixture provisiona datos sintéticos; no es la misma BD de First Run.
- Los cuatro scripts oficiales del launcher terminaron con exit 0. Son assertions
  sin servicios reales. No se ejecutó el launcher contra su destino dev ni una
  instalación manual en navegador. Las pruebas HTTP de First Run deshabilitan
  InstallData y Hangfire; geografía y objetos raw se validan por separado.
- Cliente público de autenticación: `authPublicFlows.test.ts`, **4/4 PASS**.

No se conectó a `erp_zh_dev` ni `erp_zh_prod`, no se emitió al SRI, no se usó el
seed E2E como instalación, no se consolidó la baseline ni se modificó código.
El contenedor P0 fue eliminado junto con su volumen anónimo tras verificar ID,
etiqueta, nombre de base y ausencia de bind mounts. Testcontainers administra
la eliminación de los contenedores de sus fixtures.

## Comandos de reproducción / gate del propietario

Desde la raíz, con Docker accesible. Crear otro contenedor nuevo; no reutilizar
bases ni el launcher dev. La contraseña siguiente es exclusivamente sintética.

```powershell
docker run --detach --name erp-p0-clean-d462cddde-20261031 --label erp.task=p0-clean-install --publish 127.0.0.1::5432 --env POSTGRES_DB=erp_p0_clean_isolated --env POSTGRES_USER=p0_synthetic_owner --env POSTGRES_PASSWORD=P0SyntheticOnly2026 postgres:16-alpine
docker port erp-p0-clean-d462cddde-20261031 5432
# Sustituir <puerto> por el puerto aleatorio informado arriba.
$env:ASPNETCORE_ENVIRONMENT='Testing'
$env:DOTNET_ENVIRONMENT='Testing'
$env:ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=<puerto>;Database=erp_p0_clean_isolated;Username=p0_synthetic_owner;Password=P0SyntheticOnly2026'
dotnet tool restore
dotnet ef database update --no-build --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API
dotnet ef migrations has-pending-model-changes --no-build --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API
```

En esta ejecución los proyectos ya se habían compilado por las pruebas. En un
checkout sin compilar, omitir `--no-build`. Comprobar cada exit code antes de avanzar.
Ejecutar los grupos **secuencialmente** para evitar escrituras simultáneas en `obj`:

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --filter 'FullyQualifiedName~ERP.Infrastructure.Tests.Setup|FullyQualifiedName~GlobalCatalogOfficialAlignmentPostgreSqlTests|FullyQualifiedName~SriDocTypeActiveFlagsPostgreSqlTests|FullyQualifiedName~SriCatalogComplianceTests|FullyQualifiedName~RawSqlDatabaseObjectsBaselineIntegrationTests' --logger 'trx;LogFileName=infrastructure.trx' --results-directory .local/p0-clean-install --verbosity minimal
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-restore --filter 'FullyQualifiedName~FirstRunHttpTests|FullyQualifiedName~FirstRunTokenContractTests|FullyQualifiedName~LauncherResetGuardTests|FullyQualifiedName~CompanyOperationalReadinessBranchHttpTests' --logger 'trx;LogFileName=api.trx' --results-directory .local/p0-clean-install --verbosity minimal
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter FullyQualifiedName~CreateInitialAdminHandlerTests --logger 'trx;LogFileName=application.trx' --results-directory .local/p0-clean-install --verbosity minimal
foreach ($t in 'first-run-security','launcher-startup','frontend-process-ownership','frontend-readiness') {
    pwsh -NoProfile -ExecutionPolicy Bypass -File ".\scripts\tests\$t.tests.ps1"
    if ($LASTEXITCODE -ne 0) { throw "FAIL $t" }
}
Push-Location frontend
npx.cmd vitest run src/modules/auth/api/authPublicFlows.test.ts
Pop-Location
git diff --check
```

Salidas completas y TRX: `.local/p0-clean-install/` (ignoradas por Git).
Inventario de resultados sin secretos: [tests.json](tests.json).

## Incidencias de ejecución

- Acceso a Docker y PowerShell 7 bloqueado dentro del sandbox: ejecución con
  escalación aprobada, sin cambiar destinos.
- Primer intento API falló al compilar por `CS2012` sobre `ERP.Domain.dll`, bloqueada
  por compilación simultánea. Repetición secuencial: **23/23 PASS**. Es una incidencia
  de ejecución del agente, no un error de instalación.
- Handler First Run: **5/5 PASS**. No hay fallo reproducido de instalación en estos
  grupos. El gate del propietario no queda sustituido por las pruebas del agente.
- Infrastructure: **84/84 PASS**, incluidos instalación completa, concurrencia,
  INEC, flags de catálogos y objetos raw SQL; duración de pruebas 4 min 46 s.

Clasificación: **KEEP** cadena EF, bootstrap, First Run y política física bloqueada;
**DEBT documental** de estados históricos corregida mediante actualización aditiva
en STATUS y descripción de la cadena actual. Sin nuevos frentes ni reparación amplia.

Archivos documentales modificados: `STATUS.md`, `docs/DATABASE.md`,
`docs/architecture/architecture.md` y esta carpeta de evidencia (`README.md`,
`database.txt`, `migrations.txt`, `model.txt`, `tests.json`). Historial conservado.
`git diff --check` PASS. Sin staging, commit ni push.
