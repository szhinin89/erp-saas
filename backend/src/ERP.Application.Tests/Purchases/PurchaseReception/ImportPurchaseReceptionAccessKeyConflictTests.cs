using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// PURCHASES-RECEPTION-CROSS-COMPANY-KEY-01 (H2) — solo la violación de
/// <c>uq_purchase_reception_documents_tenant_access_key</c> se traduce a un conflicto genérico; cualquier
/// otro fallo de persistencia sigue su camino normal. La evidencia PostgreSQL (dos empresas, rollback)
/// está en <c>ImportPurchaseReceptionCrossCompanyPostgreSqlTests</c>.
/// </summary>
public sealed class ImportPurchaseReceptionAccessKeyConflictTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string CompanyRuc = "1790000000001";

    private readonly Mock<IPurchaseReceptionDocumentRepository> _repo = new();
    private readonly Mock<IDatabaseExceptionTranslator> _dbEx = new();

    private ImportPurchaseReceptionHandler Handler()
    {
        var record = new PurchaseReceptionRecord(
            2, PurchaseReceptionSourceDocType.Invoice, "1791111111001", "Proveedor S.A.",
            "001-001-000000001", new string('7', 49), new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 10, 0, 0), CompanyRuc, 10m, 1.5m, 11.5m, null);
        var parser = new Mock<IPurchaseReceptionParser>();
        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record], [], 0));
        var verifier = new Mock<IPurchaseReceptionVerifier>();
        verifier
            .Setup(v => v.VerifyAsync(It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PurchaseReceptionVerifiedItem(record, true, PurchaseReceptionStatus.Pending)]);
        var companies = new Mock<ICompanyRepository>();
        companies
            .Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity.CreateManaged(TenantId, CompanyRuc, "Empresa"));
        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.CompanyLocalToUtcAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc));

        return new ImportPurchaseReceptionHandler(
            parser.Object,
            verifier.Object,
            _repo.Object,
            Mock.Of<IPurchaseCreditNoteRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid()),
            clock.Object,
            companies.Object,
            _dbEx.Object
        );
    }

    private static ImportPurchaseReceptionCommand Command() =>
        new(new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10));

    private void SaveFailsWith(string constraint)
    {
        var failure = new InvalidOperationException("db");
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var info = new DatabaseUniqueViolationInfo("23505", constraint, "t", "detalle con datos ajenos");
        _dbEx.Setup(t => t.TryGetUniqueViolation(failure, out info)).Returns(true);
    }

    [Fact]
    public async Task Reception_access_key_violation_returns_a_generic_conflict()
    {
        SaveFailsWith(ImportPurchaseReceptionHandler.AccessKeyUniqueConstraint);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Be(ImportPurchaseReceptionHandler.AccessKeyConflictMessage);
        result.Error.Should().NotContain("detalle").And.NotContain(new string('7', 49));
    }

    [Fact]
    public async Task A_failure_that_is_not_a_unique_violation_is_not_swallowed()
    {
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("db caída"));

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Any_other_unique_violation_is_not_swallowed()
    {
        SaveFailsWith("uq_otra_restriccion");

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
