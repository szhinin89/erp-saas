# Bootstrap del ERP

Infraestructura de inicialización automática. Dos niveles, cada uno con **un único
orquestador** y **un step por dominio/módulo**. Política general de datos globales
vs. de empresa: [`docs/DATABASE.md`](../../../../docs/DATABASE.md).

## Los dos niveles

| | Bootstrap Global | Bootstrap de Empresa |
|---|---|---|
| Cuándo | Una vez por instalación, en cada arranque de la API | Cada vez que se crea una `Company` |
| Prerrequisito | Migraciones EF aplicadas (`Database.MigrateAsync()` + `HasData()`) | La `Company` ya persistida |
| Orquestador | `Seeding/Global/GlobalBootstrapOrchestrator` (`IGlobalBootstrapOrchestrator`) | `Seeding/CompanyBootstrapOrchestrator` (`ICompanyBootstrapService`) |
| Interfaz de step | `Seeding/Global/IGlobalBootstrapStep` | `ERP.Application/Common/Interfaces/ICompanyBootstrapStep` |
| Constantes de orden | `Seeding/Global/GlobalBootstrapStepOrder` | `ERP.Application/Common/Interfaces/CompanyBootstrapStepOrder` |
| Único invocador | `Program.cs` (composition root) | `ERP.Infrastructure/Services/CompanyProvisioningService` |
| Steps actuales | `NavigationBootstrapStep` (10), `InstallDataBootstrapStep` (20) | `OrganizationBootstrapStep` (10), `ElectronicDocumentsBootstrapStep` (20), `InventoryBootstrapStep` (30), `SalesBootstrapStep` (40), `CajaBootstrapStep` (45), `AccessBootstrapStep` (50) |

## Flujo oficial (único, no hay atajos)

```
Bootstrap Global:
  Program.cs → IGlobalBootstrapOrchestrator → IGlobalBootstrapStep (por orden)

Bootstrap de Empresa:
  Handler → ICompanyProvisioningService → ICompanyBootstrapService
          → ICompanyBootstrapStep (por orden)
```

**Prohibido**: crear datos iniciales (globales o de empresa) desde un Controller, un
Handler que no sea uno de los dos invocadores oficiales de arriba, un Repository, el
`ErpDbContext` (fuera de `HasData()` en migraciones), o cualquier `IHostedService`/bloque
de `Program.cs` que no pase por el orquestador correspondiente.

## Reglas obligatorias de un `BootstrapStep` (Global o de Empresa)

1. **Responsabilidad única**: solo crea datos de su propio dominio. Nunca datos de otro
   módulo, ni siquiera "por comodidad" cuando ambos se necesitan juntos.
2. **Idempotente**: verifica existencia (`AnyAsync`/`FirstOrDefaultAsync`) antes de
   insertar. Debe ser seguro ejecutarlo en cada arranque (Global) o si se reintenta la
   creación de la misma empresa (Empresa).
3. **Sin dependencias directas a otro step**: si necesita algo creado por un step
   anterior, lo **consulta desde la base de datos** (nunca inyecta el tipo del otro step
   en su constructor — verificado por
   `BootstrapStepGovernanceTests.No_bootstrap_step_depends_directly_on_another_bootstrap_step`).
   Ejemplo real: `ElectronicDocumentsBootstrapStep` necesita el punto de emisión que crea
   `OrganizationBootstrapStep`, y lo obtiene consultando `EmissionPoint.IsDefault`, no por
   referencia directa.
4. **Orden explícito y único**: `Order` siempre referencia una constante del archivo de
   constantes correspondiente (`CompanyBootstrapStepOrder`/`GlobalBootstrapStepOrder`),
   nunca un literal. Convención: incrementos de 10 para insertar un step nuevo entre dos
   existentes sin renumerar los demás.
5. **Resiliencia propia si aplica**: el orquestador nunca decide qué step puede fallar
   sin bloquear el arranque — esa decisión (ver `InstallDataBootstrapStep`, que captura
   sus propios errores) vive dentro del step.

## Cómo agregar un nuevo `BootstrapStep`

1. Agregar la constante de orden en `CompanyBootstrapStepOrder` (o `GlobalBootstrapStepOrder`).
2. Crear la clase en `Seeding/Steps/` (o `Seeding/Global/Steps/`) implementando
   `ICompanyBootstrapStep`/`IGlobalBootstrapStep`, con la lógica de seed idempotente.
3. Registrarla en `DependencyInjection.cs` como `AddScoped<ICompanyBootstrapStep, MiNuevoStep>()`
   (o el equivalente global).
4. No tocar el orquestador — descubre los steps automáticamente vía
   `IEnumerable<ICompanyBootstrapStep>`/`IEnumerable<IGlobalBootstrapStep>` de DI.
5. `BootstrapStepGovernanceTests` falla automáticamente si el step no quedó registrado
   (o si algo registrado ya no existe como clase) — no requiere actualizar esa suite.

## Tests de infraestructura

| Archivo | Cubre |
|---|---|
| `ERP.Infrastructure.Tests/Seeding/CompanyBootstrapOrchestratorTests.cs` | Orden de ejecución, ejecución completa, tolerancia a cero steps, idempotencia del orquestador |
| `ERP.Infrastructure.Tests/Seeding/GlobalBootstrapOrchestratorTests.cs` | Ídem, a nivel global |
| `ERP.Infrastructure.Tests/Seeding/BootstrapStepGovernanceTests.cs` | Órdenes únicos, todo step registrado en DI (y viceversa), ausencia de acoplamiento directo entre steps |
| `ERP.Infrastructure.Tests/Seeding/InstallDataBootstrapStepTests.cs` | Comportamiento no bloqueante específico de `InstallDataBootstrapStep` |

## Ver también

- [`InstallData/README.md`](InstallData/README.md) — detalle del mecanismo de scripts SQL de datos globales inmutables.
