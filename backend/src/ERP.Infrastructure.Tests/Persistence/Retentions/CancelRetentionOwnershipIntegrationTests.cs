using ERP.Application.Common;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Retentions;

/// <summary>
/// ZH-PURCHASES-RETENTION-OWNERSHIP-01 — ownership de la anulación de retenciones contra PostgreSQL
/// real (repositorio, query filters y UnitOfWork reales, handler real): solo se anula la retención
/// ACTIVA del documento origen de la ruta. Otra compra, un gasto, otra sucursal, otro tenant o una
/// ya anulada responden el mismo NotFound que un id inexistente y la fila queda intacta.
/// El canceller solo aplica la anulación de Domain: la reversa de CxP/posting está cubierta en
/// RetentionCancellerTests. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CancelRetentionOwnershipIntegrationTests : IAsyncLifetime
{
    private const string PurchaseNotFound = "La retención no existe o no pertenece a esta compra.";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_retention_ownership_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _otherBranchId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherTenantId;
    private Guid _otherCompanyId;

    private readonly Guid _purchase = Guid.NewGuid();
    private readonly Guid _otherPurchase = Guid.NewGuid();
    private readonly Guid _expense = Guid.NewGuid();
    private readonly Guid _otherBranchPurchase = Guid.NewGuid();
    private readonly Guid _otherTenantPurchase = Guid.NewGuid();
    private readonly Guid _cancelledPurchase = Guid.NewGuid();

    private Guid _retention;
    private Guid _otherPurchaseRetention;
    private Guid _expenseRetention;
    private Guid _otherBranchRetention;
    private Guid _otherTenantRetention;
    private Guid _cancelledRetention;

    private int _number;

    private sealed class DomainOnlyCanceller : IRetentionCanceller
    {
        public Task<Result<RetentionDocument>> CancelAsync(
            RetentionDocument document,
            string reason,
            Guid cancelledBy,
            CancellationToken ct = default
        )
        {
            document.Cancel(reason, cancelledBy);
            return Task.FromResult(Result<RetentionDocument>.Success(document));
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var db = CreateContext(Guid.Empty, Guid.Empty))
        {
            await db.Database.MigrateAsync();
            var tenant = Tenant.Create("Tenant", $"t-{Guid.NewGuid():N}"[..16], _userId);
            var other = Tenant.Create("Otro", $"o-{Guid.NewGuid():N}"[..16], _userId);
            db.Tenants.AddRange(tenant, other);
            var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
            var otherCompany = Company.CreateManaged(other.Id, "1790098765001", "Otra S.A.", createdBy: _userId);
            db.Companies.AddRange(company, otherCompany);
            await db.SaveChangesAsync();
            (_tenantId, _companyId, _otherTenantId, _otherCompanyId) = (tenant.Id, company.Id, other.Id, otherCompany.Id);
        }

        _retention = await Seed(_tenantId, _companyId, _branchId, RetentionSourceDocumentType.PurchaseInvoice, _purchase);
        _otherPurchaseRetention = await Seed(_tenantId, _companyId, _branchId, RetentionSourceDocumentType.PurchaseInvoice, _otherPurchase);
        _expenseRetention = await Seed(_tenantId, _companyId, _branchId, RetentionSourceDocumentType.ExpenseDocument, _expense);
        _otherBranchRetention = await Seed(_tenantId, _companyId, _otherBranchId, RetentionSourceDocumentType.PurchaseInvoice, _otherBranchPurchase);
        _otherTenantRetention = await Seed(_otherTenantId, _otherCompanyId, _branchId, RetentionSourceDocumentType.PurchaseInvoice, _otherTenantPurchase);
        _cancelledRetention = await Seed(_tenantId, _companyId, _branchId, RetentionSourceDocumentType.PurchaseInvoice, _cancelledPurchase, cancelled: true);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private async Task<Guid> Seed(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        bool cancelled = false
    )
    {
        await using var db = CreateContext(tenantId, companyId);
        var doc = RetentionDocument.Create(tenantId, companyId, branchId, sourceType, sourceId, Guid.NewGuid(), Guid.NewGuid(), _userId);
        doc.AddLine(RetentionDocumentLine.Create(doc.Id, tenantId, RetentionTaxType.Vat, "725", "Retención IVA 725", 100m, 30m, 30m));
        doc.Issue($"001-001-{++_number:000000000}", new DateOnly(2026, 9, 3), _userId);
        if (cancelled)
            doc.Cancel("Anulada antes", _userId);
        doc.ClearDomainEvents();
        await new RetentionDocumentRepository(db, new FixedCurrentCompany(companyId)).AddAsync(doc);
        await db.SaveChangesAsync();
        return doc.Id;
    }

    private async Task<Result<ERP.Application.Modules.Retentions.DTOs.RetentionDocumentDto>> Cancel(Guid sourceId, Guid retentionId, RetentionSourceDocumentType sourceType = RetentionSourceDocumentType.PurchaseInvoice)
    {
        await using var db = CreateContext(_tenantId, _companyId);
        var handler = new CancelRetentionHandler(
            new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new DomainOnlyCanceller(),
            new UnitOfWork(db),
            new FixedCurrentTenant(_tenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId),
            Mock.Of<ICurrentUser>(u => u.UserId == _userId)
        );
        return await handler.Handle(new CancelRetentionCommand(sourceType, sourceId, retentionId, "Error en el cálculo"), CancellationToken.None);
    }

    private async Task<RetentionStatus> StatusOf(Guid retentionId, Guid tenantId, Guid companyId)
    {
        await using var db = CreateContext(tenantId, companyId);
        return (await db.Set<RetentionDocument>().AsNoTracking().SingleAsync(x => x.Id == retentionId)).Status;
    }

    [Fact]
    public async Task Retencion_de_la_compra_de_la_ruta_se_anula_y_persiste()
    {
        var result = await Cancel(_purchase, _retention);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StatusOf(_retention, _tenantId, _companyId)).Should().Be(RetentionStatus.Cancelled);
    }

    [Fact]
    public async Task Casos_invalidos_responden_el_mismo_NotFound_que_un_id_inexistente_y_no_modifican_nada()
    {
        var missing = await Cancel(_purchase, Guid.NewGuid());
        missing.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        missing.Error.Should().Be(PurchaseNotFound);

        var cases = new (string Name, Guid Source, Guid Retention, RetentionSourceDocumentType Type, Guid Tenant, Guid Company, RetentionStatus Expected)[]
        {
            ("retención de otra compra", _purchase, _otherPurchaseRetention, RetentionSourceDocumentType.PurchaseInvoice, _tenantId, _companyId, RetentionStatus.Issued),
            ("retención de un gasto por la ruta de la compra", _purchase, _expenseRetention, RetentionSourceDocumentType.PurchaseInvoice, _tenantId, _companyId, RetentionStatus.Issued),
            ("retención de un gasto con su propio id como compra", _expense, _expenseRetention, RetentionSourceDocumentType.PurchaseInvoice, _tenantId, _companyId, RetentionStatus.Issued),
            ("retención de otra sucursal", _otherBranchPurchase, _otherBranchRetention, RetentionSourceDocumentType.PurchaseInvoice, _tenantId, _companyId, RetentionStatus.Issued),
            ("retención de otro tenant", _otherTenantPurchase, _otherTenantRetention, RetentionSourceDocumentType.PurchaseInvoice, _otherTenantId, _otherCompanyId, RetentionStatus.Issued),
            ("retención ya anulada", _cancelledPurchase, _cancelledRetention, RetentionSourceDocumentType.PurchaseInvoice, _tenantId, _companyId, RetentionStatus.Cancelled),
        };

        foreach (var c in cases)
        {
            var result = await Cancel(c.Source, c.Retention, c.Type);

            (result.Code, result.Error).Should().Be((missing.Code, missing.Error), c.Name);
            (await StatusOf(c.Retention, c.Tenant, c.Company)).Should().Be(c.Expected, $"{c.Name}: la fila no se toca");
        }
    }

    // ── Dobles de identidad (mismo patrón que RetentionDocumentRepositoryTests) ──

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId { get; } = companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
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
}
