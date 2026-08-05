using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 9 — pruebas PostgreSQL reales de <see cref="RegisterAndLinkSupplierCreditNoteHandler"/>:
/// (1) AccessKey duplicado en una carrera real de dos contextos concurrentes es detectado por
/// <see cref="IDatabaseExceptionTranslator"/> → SC-007, nunca una excepción no traducida (500);
/// (2) validación cuantitativa ejecutada dentro de la misma transacción que la mutación de
/// <c>FiscalStatus</c> — verificado forzando el rollback vía la colisión de unicidad del vínculo
/// 1:1 (SC-012) inmediatamente después de que la validación cuantitativa y la mutación de dominio
/// ya se ejecutaron con éxito en memoria: tras el rollback, <c>FiscalStatus</c> persistido debe
/// seguir siendo <c>PendingSupplierCreditNote</c> (§18.4bis/§16.1).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RegisterSupplierCreditNoteIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_register_supplier_credit_note_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
    private Guid _itemId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
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
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            _tenantId,
            _branchId,
            "Bodega Principal",
            "BOD-01",
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
            _companyId,
            isMain: true
        );
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _warehouseId = warehouse.Id;

        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            _tenantId,
            "MERCH",
            "Mercadería",
            1,
            _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(
                saleVatCode: "10",
                purchaseVatCode: "10"
            ),
            saleConfig: ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(
                isForSale: true
            ),
            stockConfig: ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(
                tracksStock: true
            ),
            createdBy: _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    private async Task<Guid> SeedAuthorizedReturnAsync(decimal grandTotal)
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto 1",
            quantity: 1m,
            unitPrice: grandTotal,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _userId);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            inv.Id,
            _supplierId,
            "Producto defectuoso",
            new[] { new PurchaseReturn.DraftLineInput(inv.Lines[0].Id, _itemId, 1m, _warehouseId) },
            _userId,
            Guid.NewGuid(),
            "hash-draft"
        );
        var original = inv.Lines[0];
        var snapshot = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new PurchaseReturn.OriginalLineSnapshot(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost
            ),
        };
        ret.Authorize(
            Random.Shared.Next(1, 99999999).ToString("D8"),
            snapshot,
            balanceDueBeforeApplication: grandTotal,
            inv.CurrencyCode,
            hasIssuedWithholding: false,
            _userId,
            Guid.NewGuid(),
            "hash-authorize"
        );

        db.PurchaseInvoices.Add(inv);
        db.PurchaseReturns.Add(ret);
        await db.SaveChangesAsync();
        return ret.Id;
    }

    private async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid purchaseReturnId,
        string accessKey,
        decimal totalAmount,
        Guid clientRequestId
    )
    {
        await using var db = CreateContext();
        var handler = new RegisterAndLinkSupplierCreditNoteHandler(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new RegisterAndLinkSupplierCreditNoteCommand(
                purchaseReturnId,
                accessKey,
                "1791352688001",
                "Proveedor Test",
                $"001-001-{Random.Shared.Next(100000, 999999)}",
                DateOnly.FromDateTime(DateTime.UtcNow),
                totalAmount,
                0m,
                totalAmount,
                "USD",
                clientRequestId
            ),
            CancellationToken.None
        );
        return (result.IsSuccess, result.IsSuccess ? null : result.Error);
    }

    private async Task<(bool Success, string? Error)> ExecuteWithRendezvousAsync(
        Guid purchaseReturnId,
        string accessKey,
        decimal totalAmount,
        Guid clientRequestId,
        DocumentReuseRendezvous rendezvous
    )
    {
        await using var db = CreateContext();
        var handler = new RegisterAndLinkSupplierCreditNoteHandler(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new RendezvousReceptionRepository(
                new PurchaseReceptionDocumentRepository(
                    db,
                    new FixedCurrentCompany(() => _companyId)
                ),
                rendezvous
            ),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
        var result = await handler.Handle(
            new RegisterAndLinkSupplierCreditNoteCommand(
                purchaseReturnId,
                accessKey,
                "1791352688001",
                "Proveedor Test",
                $"001-001-{Random.Shared.Next(100000, 999999)}",
                DateOnly.FromDateTime(DateTime.UtcNow),
                totalAmount,
                0m,
                totalAmount,
                "USD",
                clientRequestId
            ),
            CancellationToken.None
        );
        return (result.IsSuccess, result.IsSuccess ? null : result.Error);
    }

    [Fact]
    public async Task AccessKey_duplicado_concurrente_es_detectado_por_IDatabaseExceptionTranslator_y_rechaza_SC_007_sin_500()
    {
        // Ambos intentos parten de un AccessKey nunca antes visto. El rendezvous obliga a los dos
        // a completar su GetByAccessKeyAsync (ambos ven null — TOCTOU) antes de que cualquiera
        // pueda continuar hacia el INSERT, garantizando la carrera real en vez de depender del
        // timing incidental de dos tareas en paralelo. Solo uno puede ganar la unicidad de BD
        // (uq_purchase_reception_documents_tenant_access_key); el otro debe fallar de forma
        // controlada (SC-007), nunca con una excepción no traducida (HTTP 500).
        var sharedAccessKey = $"AK-{Guid.NewGuid():N}";
        var return1Id = await SeedAuthorizedReturnAsync(100m);
        var return2Id = await SeedAuthorizedReturnAsync(100m);
        var rendezvous = new DocumentReuseRendezvous(participants: 2);

        var task1 = ExecuteWithRendezvousAsync(
            return1Id,
            sharedAccessKey,
            100m,
            Guid.NewGuid(),
            rendezvous
        );
        var task2 = ExecuteWithRendezvousAsync(
            return2Id,
            sharedAccessKey,
            100m,
            Guid.NewGuid(),
            rendezvous
        );
        var results = await Task.WhenAll(task1, task2);

        results
            .Count(r => r.Success)
            .Should()
            .Be(1, "solo un intento puede registrar el AccessKey");
        var loser = results.Single(r => !r.Success);
        loser.Error.Should().Contain("clave de acceso");
    }

    [Fact]
    public async Task Validacion_cuantitativa_y_mutacion_de_FiscalStatus_comparten_transaccion_rollback_forzado_no_persiste_cambios()
    {
        var sharedAccessKey = $"AK-{Guid.NewGuid():N}";
        var return1Id = await SeedAuthorizedReturnAsync(100m);
        var return2Id = await SeedAuthorizedReturnAsync(100m);

        var first = await ExecuteAsync(return1Id, sharedAccessKey, 100m, Guid.NewGuid());
        first.Success.Should().BeTrue(first.Error);

        // return2 reutiliza el mismo PurchaseReceptionDocument por AccessKey (ya persistido por el
        // primer intento) — pasa SC-008/SC-013/SC-016..018 (Difference = 0) y la mutación de
        // dominio (LinkSupplierCreditNote) se ejecuta con éxito en memoria; el fallo ocurre recién
        // en SaveChangesAsync por la unicidad 1:1 del vínculo (SC-012, el documento ya está
        // vinculado a return1) — dentro de la misma transacción abierta por el handler. El
        // rollback debe deshacer también la mutación de FiscalStatus.
        var second = await ExecuteAsync(return2Id, sharedAccessKey, 100m, Guid.NewGuid());
        second.Success.Should().BeFalse();
        second.Error.Should().Contain("vinculada a otra devolución");

        await using var verify = CreateContext();
        var persisted = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(x => x.Id == return2Id);

        persisted.FiscalStatus.Should().Be(PurchaseReturnFiscalStatus.PendingSupplierCreditNote);
        persisted.SupplierCreditNoteDocumentId.Should().BeNull();
        persisted.LinkCreditNoteClientRequestId.Should().BeNull();
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    /// <summary>
    /// Rendezvous determinista para forzar una carrera real de TOCTOU en pruebas: bloquea a cada
    /// participante hasta que todos hayan completado su lectura de comprobación, garantizando que
    /// ninguno vea el efecto del otro antes de intentar el INSERT — sustituye la dependencia del
    /// timing incidental de <c>Task.WhenAll</c> sin tocar el handler de producción.
    /// </summary>
    private sealed class DocumentReuseRendezvous(int participants)
    {
        private readonly SemaphoreSlim _release = new(0);
        private int _arrived;

        public async Task ArriveAndWaitAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _arrived) >= participants)
                _release.Release(participants);
            await _release.WaitAsync(ct);
        }
    }

    private sealed class RendezvousReceptionRepository(
        IPurchaseReceptionDocumentRepository inner,
        DocumentReuseRendezvous rendezvous
    ) : IPurchaseReceptionDocumentRepository
    {
        public async Task<PurchaseReceptionDocument?> GetByAccessKeyAsync(
            Guid tenantId,
            string accessKey,
            CancellationToken ct = default
        )
        {
            var result = await inner.GetByAccessKeyAsync(tenantId, accessKey, ct);
            await rendezvous.ArriveAndWaitAsync(ct);
            return result;
        }

        public Task AddAsync(PurchaseReceptionDocument document, CancellationToken ct = default) =>
            inner.AddAsync(document, ct);

        public Task<PurchaseReceptionDocument?> GetByIdAsync(
            Guid tenantId,
            Guid id,
            CancellationToken ct = default
        ) => inner.GetByIdAsync(tenantId, id, ct);

        public Task<PurchaseReceptionDocument?> GetByLineIdAsync(
            Guid tenantId,
            Guid lineId,
            CancellationToken ct = default
        ) => inner.GetByLineIdAsync(tenantId, lineId, ct);

        public Task<(IReadOnlyList<PurchaseReceptionDocument> Items, int Total)> GetPagedAsync(
            Guid tenantId,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => inner.GetPagedAsync(tenantId, page, pageSize, ct);

        public Task<bool> ExistsByAccessKeyAsync(
            Guid tenantId,
            string accessKey,
            CancellationToken ct = default
        ) => inner.ExistsByAccessKeyAsync(tenantId, accessKey, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
