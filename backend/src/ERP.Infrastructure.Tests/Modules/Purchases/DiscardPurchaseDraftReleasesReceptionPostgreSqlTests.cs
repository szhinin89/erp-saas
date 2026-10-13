using ERP.Application.Common;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — bug post-cierre: descartar un borrador creado desde Recepción dejaba la
/// recepción en Processed (vinculada al borrador descartado) y la UI mostraba "Compra creada" sin
/// permitir crear otra. Flujo real en PostgreSQL con los handlers reales de creación y descarte:
/// Recepción Verified → crear borrador (Processed + PurchaseId) → descartar (Verified + PurchaseId
/// null, XML intacto) → crear otra compra desde la misma recepción. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DiscardPurchaseDraftReleasesReceptionPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_discard_reception_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private const string InvoiceNumber = "034-005-002226390";
    private static readonly string AccessKey = new('7', 49);

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private PaymentTerm _paymentTerm = null!;
    private Guid _createdBy;
    private Guid _receptionId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "0990011223001",
            2,
            "DINADEC S.A.",
            _createdBy
        );
        _paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(_paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;

        // Recepción con XML autorizado (Verified), lista para "Crear compra".
        await using var dbReception = CreateContext();
        var reception = PurchaseReceptionDocument.Create(
            _tenantId,
            _companyId,
            _branchId,
            PurchaseReceptionSourceDocType.Invoice,
            "0990011223001",
            "DINADEC S.A.",
            _supplierId,
            AccessKey,
            InvoiceNumber,
            new DateOnly(2026, 10, 12),
            null,
            300m,
            45m,
            345m,
            _createdBy
        );
        var line = PurchaseReceptionLine.Create(
            reception.Id, _tenantId, "FANTA", 50m, 6m, "10", "2", 15m, 45m, 0m, 0m, 300m, 345m,
            supplierCode: "8433"
        );
        reception.AttachSriAuthorization(
            "1",
            DateTime.UtcNow,
            "<factura>DINADEC</factura>",
            DateTime.UtcNow,
            [line],
            _createdBy,
            "01",
            null,
            new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null)
        );
        dbReception.PurchaseReceptionDocuments.Add(reception);
        await dbReception.SaveChangesAsync();
        _receptionId = reception.Id;
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
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Handler REAL de creación (mismo que "Guardar" desde "Crear compra").</summary>
    private CreatePurchaseDraftHandler BuildCreateHandler(ErpDbContext db)
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ERP.Application.Common.Services.TaxRateResult(15m, "IVA 15%"));
        var roles = new Mock<IBusinessPartnerRoleRepository>();
        roles.Setup(r => r.GetByTypeAsync(_supplierId, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                BusinessPartnerRole.Create(
                    _tenantId,
                    _supplierId,
                    RoleType.Supplier,
                    _createdBy,
                    SupplierRoleConfig.Create()
                )
            );
        var paymentTerms = new Mock<IPaymentTermDefaultResolver>();
        paymentTerms
            .Setup(p => p.ResolveForPurchaseAsync(_supplierId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(_paymentTerm));
        var company = new FixedCurrentCompany(_companyId);

        return new CreatePurchaseDraftHandler(
            new PurchaseInvoiceRepository(db, company),
            new BusinessPartnerRepository(db),
            roles.Object,
            paymentTerms.Object,
            // Línea manual sin ítem ni bodega: no se consultan ítems/bodegas.
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            tax.Object,
            new PurchaseReceptionDocumentRepository(db, company),
            new ExpenseDocumentRepository(db, company),
            new FixedCurrentTenant(_tenantId),
            company,
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy),
            new PostgresDatabaseExceptionTranslator(),
            CreatePrecisionPolicy()
        );
    }

    private DiscardPurchaseDraftHandler BuildDiscardHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(_companyId);
        return new DiscardPurchaseDraftHandler(
            new PurchaseInvoiceRepository(db, company),
            new PurchaseReceptionDocumentRepository(db, company),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy)
        );
    }

    private CreatePurchaseDraftCommand CreateFromReception() =>
        new(
            _supplierId,
            "01",
            InvoiceNumber,
            new DateOnly(2026, 10, 12),
            [new PurchaseLineInput(null, "FANTA", 50m, 6m, "10")],
            AccessKey: AccessKey,
            PaymentTermId: _paymentTerm.Id
        );

    private async Task<Guid> CreateDraftAsync()
    {
        await using var db = CreateContext();
        var result = await BuildCreateHandler(db).Handle(CreateFromReception(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Id;
    }

    private async Task<PurchaseReceptionDocument> ReloadReceptionAsync()
    {
        await using var db = CreateContext();
        return await db.PurchaseReceptionDocuments.AsNoTracking().SingleAsync(x => x.Id == _receptionId);
    }

    [Fact]
    public async Task Recepcion_borrador_descartar_y_crear_de_nuevo_desde_la_misma_recepcion()
    {
        // 1. Crear borrador desde la recepción → la recepción queda Processed, vinculada al borrador.
        var firstDraftId = await CreateDraftAsync();
        var afterCreate = await ReloadReceptionAsync();
        afterCreate.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        afterCreate.PurchaseId.Should().Be(firstDraftId);

        // 2. Descartar el borrador → recepción Verified, sin PurchaseId, XML intacto.
        await using (var db = CreateContext())
        {
            var discarded = await BuildDiscardHandler(db)
                .Handle(new DiscardPurchaseDraftCommand(firstDraftId, "error de digitación"), CancellationToken.None);
            discarded.IsSuccess.Should().BeTrue(discarded.Error);
            discarded.Value!.IsDiscardedDraft.Should().BeTrue();
        }
        var afterDiscard = await ReloadReceptionAsync();
        afterDiscard.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        afterDiscard.PurchaseId.Should().BeNull();
        afterDiscard.XmlContent.Should().Be("<factura>DINADEC</factura>");

        // 3. Crear de nuevo desde la misma recepción → PASS (misma clave de acceso y número).
        var secondDraftId = await CreateDraftAsync();
        secondDraftId.Should().NotBe(firstDraftId);
        var afterRecreate = await ReloadReceptionAsync();
        afterRecreate.Status.Should().Be(PurchaseReceptionDocumentStatus.Processed);
        afterRecreate.PurchaseId.Should().Be(secondDraftId);

        // Historial: el borrador descartado se conserva, Cancelled y nunca confirmado.
        await using var dbRead = CreateContext();
        var history = await dbRead.PurchaseInvoices.AsNoTracking().SingleAsync(x => x.Id == firstDraftId);
        history.Status.Should().Be(PurchaseStatus.Cancelled);
        history.IsDiscardedDraft.Should().BeTrue();
    }

    private static ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider CreatePrecisionPolicy()
    {
        var dto = new ERP.Application.Modules.Companies.UseCases.PrecisionPolicy.EffectivePrecisionPolicyDto(
            ProfileType: "StandardCommercial",
            SalesUnitPriceDecimals: 2,
            PurchaseUnitPriceDecimals: 4,
            QuantityDecimals: 4,
            PercentageDecimals: 2,
            UnitCostDecimals: 6,
            AverageCostDecimals: 6,
            ConversionFactorDecimals: 6,
            SettlementToleranceAmount: 0.02m,
            IsLocked: false,
            LockedAt: null,
            LockedReason: null,
            MoneyDecimals: 2,
            TaxDecimals: 2,
            AccountingDecimals: 2,
            FiscalPercentageDecimals: ERP.Domain.Common.FiscalPrecision.Percentage,
            WarehouseCapacityDecimals: ERP.Domain.Modules.Inventory.Entities.WarehousePrecision.Capacity,
            CreditInstallmentPercentageDecimals: ERP.Domain.Modules.Finance.Entities.CreditTermsPrecision.InstallmentPercentage,
            PackagingWeightDecimals: ERP.Domain.Modules.Items.Entities.ItemPrecision.PackagingWeight
        );
        var mock = new Mock<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>();
        mock.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        return mock.Object;
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
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class FixedCurrentBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => branchId != Guid.Empty;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "discard-draft-test";
        public string? Email => null;
        public string? FullName => "Discard Draft Test";
        public string? Role => null;
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
