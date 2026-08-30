# Runbook — Backfill controlado de jerarquía del Plan de Cuentas (Production)

> Nivel 3 (documentación operativa). Procedimiento para corregir `ParentAccountId` de cuentas
> existentes en Production, alineándolas con el padre canónico implicado por su código
> (ACCOUNTING-CHART-CANONICAL-HIERARCHY-01). Ejecutar **solo si el diagnóstico previo
> (`docs/... AUDIT`) confirmó que la company piloto en Production tiene cuentas con
> `ParentAccountId` desalineado o legacy (padre `null`)** — si `RunControlledHierarchyMaintenanceAsync`
> ya reportó `IssuesBefore=0` para todas las companies, este runbook no aplica.

---

## Qué hace este comando

`backfill-accounting-chart-hierarchy` (`ERP.API/Program.cs`) es un comando de despliegue **explícito**,
no un job automático de arranque:

- Corre **fuera** del guard `IsProduction()` de `AccountingChartBackfillService.EnsureAsync` (ese
  guard sigue intacto y **nunca se levanta** — el backfill automático de cada arranque de API sigue
  excluyendo Production, sin cambios).
- Por cada company activa: diagnóstico de solo lectura (`DiagnoseHierarchyAsync`) → si no hay
  hallazgos, no toca nada → si hay hallazgos, corrige `ParentAccountId` dentro de una **transacción
  explícita por company** (`BeginTransactionAsync`/`Commit`/`Rollback` — si algo falla en una
  company, esa company revierte por completo y las demás no se ven afectadas) → diagnóstico
  posterior para confirmar el resultado real.
- Nunca crea cuentas nuevas, nunca cambia `Code` ni `Name`, nunca toca `PostingRule`/`JournalEntry`.
- Imprime un resumen por consola y termina — no levanta el host web.

## Pre-requisitos

1. **Backup previo obligatorio.** Nunca ejecutar este comando contra Production sin backup fresco:
   ```powershell
   cd C:\ProyectCursor\erp-saas
   .\scripts\backup-localprod.ps1
   .\scripts\restore-check-localprod.ps1
   ```
   El segundo script valida que el backup restaura correctamente antes de confiar en él (mismo
   criterio que el runbook diario del piloto — ver
   [`SUMAK_DAILY_RUNBOOK.md`](SUMAK_DAILY_RUNBOOK.md)).

2. **Confirmar que hay hallazgos reales** antes de correrlo — no es necesario ejecutarlo
   "por si acaso". Si tienes acceso de solo lectura a la base, puedes confirmar contando cuentas con
   `parent_account_id IS NULL` que tengan código con `.` (cuenta compuesta sin padre asignado) o
   comparar manualmente contra el blueprint — pero el propio comando ya hace este diagnóstico antes
   de tocar nada, así que basta con revisar su salida "antes" y decidir si aplicar el "después".

3. **Configurar la connection string correcta de Production** — nunca correr este comando apuntando
   por accidente a una base de desarrollo/staging, ni viceversa. `docker-compose.localprod.yml` fija
   la connection string real vía variables de entorno:
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=<puerto_publicado_postgres>;Database=dberpsaas;Username=postgres;Password=<password_real>"
   $env:ASPNETCORE_ENVIRONMENT = "Production"
   ```
   Verifica el host/puerto/password reales en `.env.docker.local` (nunca lo imprimas ni lo pegues en
   un chat/log) o usa el mismo Postgres containerizado si el comando se ejecuta desde dentro de la
   red Docker del piloto.

## Ejecución

```powershell
cd C:\ProyectCursor\erp-saas
dotnet run --project backend/src/ERP.API -- backfill-accounting-chart-hierarchy
```

Es una operación de despliegue **manual y deliberada** — nadie más que un operador humano (o un
agente IA con autorización explícita del usuario para esta acción puntual) debe dispararla, y solo
una vez por cada corrida necesaria. No agregar esto a ningún script de arranque automático ni CI/CD.

## Interpretar la salida

```
[backfill-accounting-chart-hierarchy] Companies: N. Con hallazgos antes: X. Con hallazgos después: Y. ParentAccountId corregidos: F. Pendientes sin resolver (fuera del blueprint): U.
  - Company <guid>: antes=A después=B corregidos=C sin_resolver=D
```

- **`Con hallazgos después` (Y) debe ser 0** — si no lo es, hay compañías con inconsistencias que el
  backfill no pudo resolver (ver `sin_resolver` por company: son cuentas cuyo código implica un
  padre que no existe en el Plan de Cuentas de esa company — típicamente cuentas custom creadas por
  un admin con un código que no sigue el blueprint retail; el comando **nunca inventa** una cuenta
  agrupadora para resolverlas, por diseño).
- Si `sin_resolver > 0` para alguna company, **no es un fallo del comando** — es una inconsistencia
  real de datos que requiere decisión humana (¿corregir el código de esa cuenta? ¿crear la
  agrupadora manualmente vía Create Account, que ahora valida el padre canónico? ¿el código estaba
  mal desde el origen?). Reportarlo, no forzar nada.
- Un `Rollback` de una company específica (excepción no capturada) aborta el comando completo con
  stack trace en consola — la company que falló queda exactamente como estaba antes (transacción
  revertida), las companies ya procesadas antes de la que falló quedan con su fix ya aplicado y
  comprometido (cada company es su propia transacción independiente).

## Criterio de éxito

- `Con hallazgos después: 0` para todas las companies, **o** cada company con `sin_resolver > 0`
  tiene una causa identificada y documentada (no es una sorpresa).
- Ningún `Code`/`Name` de cuenta cambió (el comando no lo permite estructuralmente).
- `PostingRule`/`JournalEntry` sin ningún cambio (el comando no los toca).

## Después de ejecutar

1. Confirmar que la API en producción (contenedor) sigue sirviendo con normalidad — este comando no
   la reinicia ni la detiene, corre como proceso aparte:
   ```powershell
   curl.exe -s http://localhost:5003/health/live
   curl.exe -s http://localhost:5003/health/ready
   ```
2. Si se aplicaron correcciones (`ParentAccountId corregidos > 0`), verificar visualmente en
   Plan de Cuentas (`ChartOfAccountsPage`) que la columna "Nivel" y la indentación visual ahora
   coinciden para las cuentas afectadas.
3. Guardar la salida de consola completa (antes/después por company) como evidencia de la
   corrección aplicada.

## Qué NO hacer

- No levantar el guard `IsProduction()` de `AccountingChartBackfillService.EnsureAsync` para que el
  backfill "normal" corra automáticamente en Production en cada arranque — ese guard es deliberado
  y permanece así.
- No ejecutar este comando repetidamente "para asegurar" — es idempotente pero no es gratis (abre
  una transacción por company); una corrida es suficiente si `Con hallazgos después: 0`.
- No mezclar esta operación con ningún otro despliegue/migración en la misma ventana de mantenimiento
  sin necesidad — es independiente y no requiere downtime.
