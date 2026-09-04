using ERP.Application.Common;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-APPLICATION-01C — cubre <see cref="GetRetentionBySourceHandler"/>. "No encontrada"
/// es un estado normal (no hay retención activa para ese origen todavía), por eso se modela como
/// <c>Result&lt;RetentionDocumentDto?&gt;.Success(null)</c>, mismo criterio que
/// <c>GetWithholdingByPurchaseQuery</c> de Purchases.
/// </summary>
public sealed class GetRetentionBySourceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid SourceDocumentId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static RetentionDocument IssuedDocument(Guid branchId)
    {
        var doc = RetentionDocument.Create(
            TenantId, CompanyId, branchId, RetentionSourceDocumentType.ExpenseDocument,
            SourceDocumentId, SupplierId, EmissionPointId, UserId
        );
        doc.AddLine(
            RetentionDocumentLine.Create(doc.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 725", 100m, 30m, 30m)
        );
        doc.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);
        doc.ClearDomainEvents();
        return doc;
    }

    // ── 22) Devuelve retención por origen ─────────────────────────────────

    [Fact]
    public async Task Devuelve_retencion_activa_por_origen()
    {
        var fx = new Fixture();
        var document = IssuedDocument(BranchId);
        fx.SetupSource(document);

        var result = await fx.Handler.Handle(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, SourceDocumentId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(document.Id);
        result.Value.Lines.Should().HaveCount(1);
    }

    // ── 23) No devuelve de otra company/branch ────────────────────────────

    [Fact]
    public async Task No_devuelve_retencion_de_otra_sucursal()
    {
        var fx = new Fixture();
        var document = IssuedDocument(OtherBranchId);
        fx.SetupSource(document);

        var result = await fx.Handler.Handle(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, SourceDocumentId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task No_devuelve_retencion_de_otro_tenant_o_company()
    {
        var fx = new Fixture();
        // GetBySourceAsync ya filtra tenant+company vía Scoped/ForOperationalScope en Infra — a
        // nivel de handler, verificamos que se invoca exactamente con el tenant/company del
        // contexto actual (nunca del body/query).
        fx.RetentionRepo
            .Setup(r => r.GetBySourceAsync(
                TenantId, CompanyId, RetentionSourceDocumentType.ExpenseDocument, SourceDocumentId,
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((RetentionDocument?)null);

        var result = await fx.Handler.Handle(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, SourceDocumentId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        fx.RetentionRepo.Verify(
            r => r.GetBySourceAsync(
                TenantId, CompanyId, RetentionSourceDocumentType.ExpenseDocument, SourceDocumentId,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    // ── 24) Devuelve null si no existe ─────────────────────────────────────

    [Fact]
    public async Task Devuelve_null_si_no_existe_retencion_activa_para_el_origen()
    {
        var fx = new Fixture();
        fx.RetentionRepo
            .Setup(r => r.GetBySourceAsync(
                TenantId, CompanyId, It.IsAny<RetentionSourceDocumentType>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((RetentionDocument?)null);

        var result = await fx.Handler.Handle(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    private sealed class Fixture
    {
        public Mock<IRetentionDocumentRepository> RetentionRepo { get; } = new();

        public GetRetentionBySourceHandler Handler =>
            new(
                RetentionRepo.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId)
            );

        public void SetupSource(RetentionDocument document) =>
            RetentionRepo
                .Setup(r => r.GetBySourceAsync(
                    TenantId, CompanyId, document.SourceDocumentType, document.SourceDocumentId,
                    It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(document);
    }
}
