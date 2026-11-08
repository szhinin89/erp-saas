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
/// PURCHASES-RECEPTION-RECEIVER-RUC-01 (H1) — el receptor de TODAS las filas se valida contra el RUC de
/// la empresa operativa antes del verificador y de cualquier escritura; una sola fila ajena, sin
/// receptor o inválida rechaza el archivo completo.
/// </summary>
public sealed class ImportPurchaseReceptionReceiverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string CompanyRuc = "1700000001001";
    private const string CompanyCedula = "1700000001";
    private const string ForeignRuc = "0990000000001";

    private readonly Mock<IPurchaseReceptionParser> _parser = new();
    private readonly Mock<IPurchaseReceptionVerifier> _verifier = new();
    private readonly Mock<IPurchaseReceptionDocumentRepository> _repo = new();
    private readonly Mock<ICompanyRepository> _companies = new();

    private ImportPurchaseReceptionHandler Handler()
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.CompanyLocalToUtcAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, DateTime local, CancellationToken _) =>
                DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc));
        var creditNotes = new Mock<IPurchaseCreditNoteRepository>();
        creditNotes
            .Setup(r => r.GetIdByReceptionDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        return new ImportPurchaseReceptionHandler(
            _parser.Object,
            _verifier.Object,
            _repo.Object,
            creditNotes.Object,
            tenant.Object,
            company.Object,
            Mock.Of<ICurrentBranch>(b => b.BranchId == Guid.NewGuid()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid()),
            clock.Object,
            _companies.Object,
            new Mock<IDatabaseExceptionTranslator>().Object
        );
    }

    private void GivenCompany(string ruc = CompanyRuc, bool temporary = false, Guid? tenantId = null) =>
        _companies
            .Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity.CreateManaged(
                tenantId ?? TenantId, ruc, "Empresa receptora", isTemporaryTaxIdentification: temporary));

    private void GivenRows(params PurchaseReceptionRecord[] records)
    {
        _parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult(records, [], 0));
        _verifier
            .Setup(v => v.VerifyAsync(
                It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PurchaseReceptionRecord> rows, CancellationToken _) =>
                rows.Select(r => new PurchaseReceptionVerifiedItem(r, true, PurchaseReceptionStatus.Pending))
                    .ToList());
    }

    private static PurchaseReceptionRecord Row(int line, string? receiver) =>
        new(
            line,
            PurchaseReceptionSourceDocType.Invoice,
            "1791111111001",
            "Proveedor S.A.",
            $"001-001-00000000{line}",
            line.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + new string('1', 47),
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 10, 0, 0),
            receiver,
            10m,
            1.5m,
            11.5m,
            null
        );

    private static ImportPurchaseReceptionCommand Command() =>
        new(new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10));

    private void AssertNothingWasRead_orWritten()
    {
        _verifier.Verify(
            v => v.VerifyAsync(It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repo.Verify(r => r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accepts_exact_ruc_and_valid_natural_person_cedula()
    {
        GivenCompany();
        GivenRows(Row(2, CompanyRuc), Row(3, CompanyCedula));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Items.Should().HaveCount(2);
        _repo.Verify(r => r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task One_foreign_row_rejects_the_whole_file_without_reads_or_writes()
    {
        GivenCompany();
        GivenRows(Row(2, CompanyRuc), Row(3, ForeignRuc), Row(4, CompanyRuc));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("No se importó ningún documento").And.Contain("(líneas 3)");
        result.Error.Should().NotContain(ForeignRuc);
        AssertNothingWasRead_orWritten();
    }

    [Theory]
    [InlineData(null, "no indican la identificación del receptor")]
    [InlineData("", "no indican la identificación del receptor")]
    [InlineData("1700000002", "identificación del receptor inválida")]
    [InlineData("12345", "identificación del receptor inválida")]
    public async Task Missing_or_invalid_receiver_rejects_the_whole_file(string? receiver, string expected)
    {
        GivenCompany();
        GivenRows(Row(2, CompanyRuc), Row(3, receiver));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(expected);
        AssertNothingWasRead_orWritten();
    }

    [Fact]
    public async Task Company_with_temporary_tax_identification_cannot_import()
    {
        GivenCompany(temporary: true);
        GivenRows(Row(2, CompanyRuc));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("RUC definitivo válido");
        AssertNothingWasRead_orWritten();
    }

    [Fact]
    public async Task Company_of_another_tenant_is_never_used_as_receiver_reference()
    {
        GivenCompany(tenantId: Guid.NewGuid());
        GivenRows(Row(2, CompanyRuc));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        AssertNothingWasRead_orWritten();
    }

    [Fact]
    public async Task File_without_supported_rows_does_not_need_the_company()
    {
        GivenRows();

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _companies.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
