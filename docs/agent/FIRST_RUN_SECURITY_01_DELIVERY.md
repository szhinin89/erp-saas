# FIRST-RUN-SECURITY-01

Implementacion focal, pendiente de revision del propietario.

- KEEP: endpoint y validadores first-run existentes, hash del token, expiracion y anulacion al inicializar.
- BUG REAL: captura automatica desde log, alta con credenciales predefinidas y wizard/login en launcher. Eliminados; solo consulta estado. Alta manual por propietario en flujo existente.
- BUG REAL: drop sin destino efectivo validado. Factory EF confirma antes de construir contexto: solo Development, localhost/127.0.0.1/::1, puerto 5435, erp_zh_dev. Coincidencia textual exacta; EOF y respuestas genericas bloquean. Entorno restaurado al salir del launcher.
- STRUCTURAL DEBT: token sigue siendo bearer visible en consola API existente. El acceso a consola/logs del host requiere control operativo del propietario. No hay prueba de identidad humana del propietario en el endpoint.

Reproduccion estatica sobre HEAD: cuatro patrones inseguros presentes. Regresion PowerShell PASS; .NET 13/13 PASS (11 casos de guard + 2 contratos del estado token). No se crean usuarios, no se consume ningun token real ni se abren conexiones. Estos contratos no prueban atomicidad ni concurrencia PostgreSQL. No ejecutar integracion de persistencia bajo las restricciones actuales.

Archivos: CLAUDE.md; scripts/dev-launcher.ps1; scripts/tests/first-run-security.tests.ps1; backend/src/ERP.API/Hosting/{ErpDesignTimeDbContextFactory,LauncherResetGuard}.cs; backend/src/ERP.API.Tests/Hosting/{LauncherResetGuardTests,FirstRunTokenContractTests}.cs; docs/architecture/security.md; docs/DEVELOPMENT.md; STATUS.md; docs/agent/{PROJECT_STATE,WORK_QUEUE,FIRST_RUN_SECURITY_01_DELIVERY}.md.

Gate PowerShell desde la raiz, sin instalacion ni acceso BD:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/tests/first-run-security.tests.ps1
if ($LASTEXITCODE -ne 0) { throw 'FAIL launcher guard' }
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-restore --filter "FullyQualifiedName~LauncherResetGuardTests|FullyQualifiedName~FirstRunTokenContractTests" --nologo --verbosity quiet -p:WarningLevel=0
if ($LASTEXITCODE -ne 0) { throw 'FAIL focal tests' }
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'FAIL diff check' }
```

No instalador, migraciones, seeds, alta de usuarios, operaciones BD, suites completas, staging, commit ni push. La revision no autoriza ejecutar reset. No se modificaron modulos CLOSED. Logs previos no inspeccionados ni eliminados.
