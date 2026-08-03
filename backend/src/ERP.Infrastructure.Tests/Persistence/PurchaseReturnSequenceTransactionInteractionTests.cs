using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// FASE 0 de P0-02 (Devolución de Compra) — prueba de PostgreSQL real exigida por §16.3 /
/// §7.1bis del diseño (P0-02_PURCHASE_RETURN_DESIGN.md), prerrequisito bloqueante antes de
/// codificar <c>AuthorizePurchaseReturnUseCases</c>.
///
/// Reproduce exactamente el patrón compuesto que usará la futura Fase 6:
///   transacción explícita del handler (<c>IUnitOfWork.BeginTransactionAsync</c> — aquí,
///   <c>Database.BeginTransactionAsync</c> directo, mismo mecanismo subyacente) →
///   advisory lock transaccional (<c>pg_advisory_xact_lock</c>, namespace de prueba dedicado,
///   distinto de "PurchaseInvoice.FinancialLock"/"SupplierCredit.Lock"/"PurchaseReturn.Sequence") →
///   <c>StockRepository.SaveChangesWithSequenceRetryAsync</c> → conflicto de secuencia forzado por
///   un segundo escritor concurrente sobre el mismo <c>CurrentStock</c> (mismo
///   Company/Product/Warehouse) → verificación empírica de si
///   <c>RecoverFromConflictAndRetrackAsync</c> logra reintentar con éxito dentro de la misma
///   transacción ambiente, o si PostgreSQL ya abortó la transacción completa tras el primer error
///   (comportamiento estándar de Postgres: cualquier error dentro de una transacción la deja en
///   estado "aborted" — SQLSTATE 25P02 — y todo comando posterior, incluidas lecturas, es
///   rechazado hasta ROLLBACK/COMMIT).
///
/// Resultado real observado (ver comentario al final de la clase) determina si la Fase 6 debe usar
/// la opción (a) reabrir transacción completa, o (b) SAVEPOINT alrededor del SaveChanges interno —
/// tal como exige §16.3.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnSequenceTransactionInteractionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_p002_phase0_seq_interaction")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouseId;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly ITestOutputHelper _out;

    // Namespace de advisory lock EXCLUSIVO de esta prueba — nunca colisiona con los locks de
    // producción ("PurchaseInvoice.FinancialLock", "SupplierCredit.Lock", "PurchaseReturn.Sequence").
    private const string TestLockNamespace = "Test.P0-02.Phase0.§16.3.SequenceInteraction";

    public PurchaseReturnSequenceTransactionInteractionTests(ITestOutputHelper output)
    {
        _out = output;
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("P0-02 Phase0 Tenant", $"p002-ph0-{Guid.NewGuid():N}"[..20], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            $"179{Guid.NewGuid():N}"[..13],
            "P0-02 Phase0 S.A.",
            createdBy: _userId
        );

        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Sucursal Test",
            address: "Av. Test 1",
            code: "BT",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: company.Id
        );

        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Test",
            "WT",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            company.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Set<Warehouse>().Add(warehouse);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _warehouseId = warehouse.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private static StockRepository CreateRepo(ErpDbContext db, Guid companyId) =>
        new(db, new FixedCurrentCompany(companyId), new PostgresDatabaseExceptionTranslator());

    /// <summary>
    /// Escenario central de §16.3: transacción explícita ambiente + advisory lock +
    /// SaveChangesWithSequenceRetryAsync ante un conflicto de secuencia real forzado por un
    /// segundo escritor concurrente que ya comiteó antes de que la transacción bajo prueba
    /// intente guardar. Documenta con evidencia real (no supuesta) cuál de los dos resultados
    /// posibles ocurre.
    /// </summary>
    [Fact]
    public async Task Conflicto_de_secuencia_dentro_de_transaccion_explicita_ambiente_aborta_la_transaccion_completa()
    {
        // 1) Movimiento inicial committeado fuera de cualquier transacción explícita — establece
        //    SequenceNumber=1 para (CompanyId, ProductId, WarehouseId), como línea base real.
        await using (var seedDb = CreateContext())
        {
            var seedRepo = CreateRepo(seedDb, _companyId);
            await seedRepo.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.PositiveAdjust,
                100m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Stock inicial",
                null,
                null,
                _userId,
                unitCost: 10m
            );
            await seedRepo.SaveChangesWithSequenceRetryAsync();
        }

        // 2) Transacción A: réplica exacta del patrón de AuthorizePurchaseReturnUseCases (Fase 6) —
        //    transacción explícita ambiente abierta por el "handler" (aquí, Database.BeginTransactionAsync,
        //    mecanismo subyacente idéntico al que usa IUnitOfWork.BeginTransactionAsync) seguida de un
        //    advisory lock transaccional dedicado.
        await using var dbA = CreateContext();
        await using var txA = await dbA.Database.BeginTransactionAsync();

        await dbA.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({TestLockNamespace}), hashtext({_companyId.ToString()}))"
        );

        var repoA = CreateRepo(dbA, _companyId);

        // repoA calcula, DENTRO de la transacción A todavía no comiteada, que el próximo
        // SequenceNumber es 2 (basado en el último movimiento COMMITEADO visible = seq 1).
        await repoA.AppendMovementAsync(
            _tenantId,
            _companyId,
            _productId,
            _warehouseId,
            StockMovementType.NegativeAdjust,
            -10m,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Movimiento bajo transacción A (aún no comiteado)",
            null,
            null,
            _userId
        );

        // 3) "Segundo escritor concurrente sobre el mismo CurrentStock" (exigido literalmente por
        //    §16.3): una conexión totalmente independiente, SIN transacción explícita (autocommit
        //    real por SaveChanges), que también calcula SequenceNumber=2 para la misma clave
        //    (Company/Product/Warehouse) — porque no ve el movimiento aún no comiteado de A — y
        //    LO COMITEA ANTES de que A intente guardar. Esto fuerza, de forma determinista y real
        //    (no simulada con mocks), la violación de
        //    uq_stock_movements_company_product_warehouse_sequence cuando A guarde.
        await using (var dbB = CreateContext())
        {
            var repoB = CreateRepo(dbB, _companyId);
            await repoB.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.NegativeAdjust,
                -5m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Movimiento concurrente B (comitea antes que A)",
                null,
                null,
                _userId
            );
            await repoB.SaveChangesWithSequenceRetryAsync();
        }

        // 4) Punto bajo prueba: repoA.SaveChangesWithSequenceRetryAsync() dentro de la transacción A
        //    ya abierta. El primer intento debe fallar por conflicto de secuencia (seq=2 ya tomado
        //    por B). ¿El mecanismo de reintento in-process (RecoverFromConflictAndRetrackAsync)
        //    logra recuperarse y reintentar con éxito DENTRO de la misma transacción A, o
        //    PostgreSQL ya abortó la transacción completa (25P02) de modo que hasta las lecturas
        //    de RecoverFromConflictAndRetrackAsync fallan?
        Exception? observedException = null;
        var retrySucceeded = false;
        try
        {
            await repoA.SaveChangesWithSequenceRetryAsync();
            retrySucceeded = true;
        }
        catch (Exception ex)
        {
            observedException = ex;
        }

        // ── Evidencia empírica ──────────────────────────────────────────────────
        // Esta prueba caracteriza un comportamiento real de infraestructura, no impone un
        // resultado esperado de antemano: ambas ramas son "passing" — lo que se registra es CUÁL
        // de las dos ocurrió, con evidencia real en el output del test (§16.3 exige evidencia,
        // no una suposición).
        if (retrySucceeded)
        {
            // Resultado (a): el reintento in-process funcionó DENTRO de la transacción ambiente.
            await txA.CommitAsync();

            await using var verifyDb = CreateContext();
            var movements = await verifyDb
                .Set<StockMovement>()
                .Where(m => m.ProductId == _productId && m.WarehouseId == _warehouseId)
                .OrderBy(m => m.SequenceNumber)
                .ToListAsync();

            movements.Select(m => m.SequenceNumber).Should().OnlyHaveUniqueItems();

            _out.WriteLine(
                "RESULTADO EMPÍRICO §16.3: retrySucceeded=true. El reintento in-process de "
                    + "SaveChangesWithSequenceRetryAsync SÍ logra recuperarse y confirmar dentro de "
                    + "la misma transacción ambiente tras un conflicto de secuencia. SequenceNumbers "
                    + "finales: "
                    + string.Join(", ", movements.Select(m => m.SequenceNumber))
                    + ". Patrón para Fase 6: NO se requiere SAVEPOINT ni reapertura de transacción — "
                    + "el mecanismo actual de StockRepository ya es seguro para "
                    + "AuthorizePurchaseReturnUseCases (opción: reutilizar el mecanismo existente tal cual)."
            );
        }
        else
        {
            // Resultado (b): PostgreSQL abortó la transacción completa tras el primer conflicto —
            // el propio mecanismo de recuperación (lecturas incluidas) fue rechazado por el motor.
            observedException.Should().NotBeNull();

            var pg = UnwrapPostgresException(observedException!);

            await txA.RollbackAsync();

            _out.WriteLine(
                "RESULTADO EMPÍRICO §16.3: retrySucceeded=false. PostgreSQL abortó la transacción "
                    + "completa tras el primer conflicto de secuencia — el reintento in-process de "
                    + "SaveChangesWithSequenceRetryAsync NO puede recuperarse dentro de la misma "
                    + "transacción ambiente. Excepción real observada: "
                    + (pg is null
                        ? observedException!.ToString()
                        : $"SqlState={pg.SqlState} MessageText={pg.MessageText}")
                    + ". Patrón obligatorio para Fase 6 (AuthorizePurchaseReturnUseCases): opción (b) "
                    + "— usar SAVEPOINT de PostgreSQL alrededor del SaveChanges interno de la captura "
                    + "de secuencia/stock, para poder revertir solo esa porción sin abortar la "
                    + "transacción externa completa (advisory lock + PurchaseReturnSequence.CaptureNextAsync "
                    + "+ resto de efectos de Authorize())."
            );

            // Assertion real (no forzada): la causa raíz debe ser efectivamente un rechazo de
            // PostgreSQL (violación de unicidad y/o transacción abortada 25P02), nunca un error de
            // otra naturaleza — de lo contrario el hallazgo no sería atribuible al comportamiento
            // de Postgres que este test existe para caracterizar.
            (pg is not null || observedException is DbUpdateException)
                .Should()
                .BeTrue(
                    $"se espera que la causa raíz sea de origen PostgreSQL/EF, no "
                        + $"{observedException!.GetType().FullName}"
                );
        }
    }

    /// <summary>
    /// Control negativo — mismo conflicto de secuencia, pero SIN transacción explícita ambiente
    /// (exactamente el uso actual y ya probado de StockRepository fuera de un handler con
    /// IUnitOfWork.BeginTransactionAsync). Debe recuperarse con éxito, confirmando que el
    /// mecanismo de reintento funciona quando cada SaveChangesAsync corre en su propia transacción
    /// implícita — y aislando así que el problema (si existe) es específico de la composición con
    /// una transacción explícita ambiente, no un defecto general del mecanismo de retry.
    /// </summary>
    [Fact]
    public async Task Conflicto_de_secuencia_sin_transaccion_explicita_ambiente_se_recupera_con_exito()
    {
        await using (var seedDb = CreateContext())
        {
            var seedRepo = CreateRepo(seedDb, _companyId);
            await seedRepo.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.PositiveAdjust,
                100m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Stock inicial (control)",
                null,
                null,
                _userId,
                unitCost: 10m
            );
            await seedRepo.SaveChangesWithSequenceRetryAsync();
        }

        await using var dbA = CreateContext();
        var repoA = CreateRepo(dbA, _companyId);
        await repoA.AppendMovementAsync(
            _tenantId,
            _companyId,
            _productId,
            _warehouseId,
            StockMovementType.NegativeAdjust,
            -10m,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Movimiento A sin transacción explícita (control)",
            null,
            null,
            _userId
        );

        await using (var dbB = CreateContext())
        {
            var repoB = CreateRepo(dbB, _companyId);
            await repoB.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.NegativeAdjust,
                -5m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Movimiento concurrente B (control)",
                null,
                null,
                _userId
            );
            await repoB.SaveChangesWithSequenceRetryAsync();
        }

        var act = async () => await repoA.SaveChangesWithSequenceRetryAsync();
        await act.Should()
            .NotThrowAsync(
                "sin transacción explícita ambiente, cada intento de SaveChangesAsync corre en su "
                    + "propia transacción implícita de PostgreSQL — un conflicto en el primer intento "
                    + "no deja ningún estado 'aborted' que afecte al segundo intento, por eso el "
                    + "mecanismo de retry de StockRepository ya funciona correctamente en este modo "
                    + "(el mismo que usan hoy ConfirmPurchaseUseCases/ExecuteStockAdjustmentCommandHandler "
                    + "para las llamadas que NO están compuestas con una transacción explícita externa)."
            );
    }

    private static PostgresException? UnwrapPostgresException(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
            if (e is PostgresException pg)
                return pg;
        return null;
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}

// ── Resultado empírico registrado tras ejecución real (Fase 0, P0-02, 2026-07-31) ──────────────
//
// Conflicto_de_secuencia_dentro_de_transaccion_explicita_ambiente_aborta_la_transaccion_completa
// (nombre del método conserva la hipótesis original del diseño por trazabilidad — el resultado
// real REFUTA esa hipótesis):
//
//   retrySucceeded = TRUE, de forma determinista en 4 ejecuciones consecutivas (no flaky).
//   SequenceNumbers finales observados: 1 (seed), 2 (escritor B, comiteado primero),
//   3 (escritor A, recuperado tras el conflicto real con seq=2 — RecoverFromConflictAndRetrackAsync
//   sí pudo volver a leer CurrentStock/StockMovement y reintentar SaveChangesAsync DENTRO de la
//   misma transacción explícita ambiente sin que PostgreSQL la hubiera abortado).
//
// Causa raíz identificada: EF Core (>= 6, provider Npgsql) crea automáticamente un SAVEPOINT
// implícito antes de cada SaveChangesAsync() cuando el DbContext detecta una transacción ya
// abierta externamente (Database.BeginTransactionAsync ya iniciado por el llamador, no por el
// propio SaveChanges) — comportamiento "automatic savepoints", habilitado por defecto para
// providers que soportan SAVEPOINT (PostgreSQL lo soporta). Ni ErpDbContext ni UnitOfWork
// deshabilitan `Database.AutoSavepointsEnabled` en ningún punto del código (verificado por grep,
// cero resultados) — por lo tanto el comportamiento por defecto está activo hoy en todo el ERP,
// no solo en esta prueba.
//
// Verdicto para la Fase 6 (AuthorizePurchaseReturnUseCases), exigido por §16.3 del diseño:
//   Opción (b) en espíritu — "aislar el SaveChanges interno con un mecanismo tipo SAVEPOINT para
//   no abortar la transacción externa" — YA ESTÁ VIGENTE, provisto automáticamente por EF Core sin
//   necesidad de código nuevo. AuthorizePurchaseReturnUseCases puede reutilizar exactamente el
//   mismo patrón compuesto (IUnitOfWork.BeginTransactionAsync → advisory lock →
//   StockRepository.SaveChangesWithSequenceRetryAsync → CommitAsync) que ya usa
//   AuthorizeSalesReturnUseCases, sin envolver manualmente un SAVEPOINT explícito y sin reabrir la
//   transacción completa ante un conflicto de secuencia de StockMovement. Este hallazgo también
//   implica que el mismo patrón, ya en producción en AuthorizeSalesReturnUseCases (Sales), es
//   seguro bajo este escenario de concurrencia — no es una vulnerabilidad preexistente sin cubrir.
//
// Advertencia de alcance: esta prueba caracteriza específicamente el conflicto de
// UNIQUE(company,product,warehouse,sequence)/concurrencia de CurrentStock vía StockRepository. No
// caracteriza el comportamiento ante *otros* tipos de error dentro de la misma transacción (p. ej.
// una violación de check constraint de negocio, o un error no relacionado a IsSequenceConflict) —
// esos casos no pasan por RecoverFromConflictAndRetrackAsync y su interacción con el savepoint
// automático de EF Core no fue ejercida por esta suite.
