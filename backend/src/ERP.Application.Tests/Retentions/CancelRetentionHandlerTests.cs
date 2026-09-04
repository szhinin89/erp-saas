using ERP.Application.Common;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-APPLICATION-01C — cubre <see cref="CancelRetentionHandler"/>/<see cref="CancelRetentionValidator"/>.
/// Las guardas de negocio (no cancelar Draft, no cancelar dos veces, motivo obligatorio) viven en
/// <see cref="RetentionDocument.Cancel"/> — estos tests verifican que el handler las deja
/// propagarse como <c>Result.ValidationFailure</c>, sin reimplementarlas.
/// </summary>
public sealed class CancelRetentionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid SourceDocumentId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static RetentionDocument IssuedDocument(Guid branchId, Guid issuedBy)
    {
        var doc = RetentionDocument.Create(
            TenantId, CompanyId, branchId, RetentionSourceDocumentType.ExpenseDocument,
            SourceDocumentId, SupplierId, EmissionPointId, issuedBy
        );
        doc.AddLine(
            RetentionDocumentLine.Create(doc.Id, TenantId, RetentionTaxType.Vat, "725", 100m, 30m, 30m)
        );
        doc.Issue("001-001-000000001", new DateOnly(2026, 9, 3), issuedBy);
        doc.ClearDomainEvents();
        return doc;
    }

    private static RetentionDocument DraftDocument(Guid branchId, Guid createdBy) =>
        RetentionDocument.Create(
            TenantId, CompanyId, branchId, RetentionSourceDocumentType.ExpenseDocument,
            SourceDocumentId, SupplierId, EmissionPointId, createdBy
        );

    // ── 16) Cancela Issued con motivo ─────────────────────────────────────

    [Fact]
    public async Task Cancela_retencion_emitida_con_motivo()
    {
        var fx = new Fixture();
        var document = IssuedDocument(BranchId, UserId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(document.Id, "Error en el cálculo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RetentionStatus.Cancelled);
        result.Value.CancelReason.Should().Be("Error en el cálculo");
        fx.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 17) Rechaza sin motivo ─────────────────────────────────────────────

    [Fact]
    public void Validator_rechaza_motivo_vacio()
    {
        var result = new CancelRetentionValidator().Validate(
            new CancelRetentionCommand(Guid.NewGuid(), "")
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CancelRetentionCommand.Reason));
    }

    // ── 18) Rechaza cancelar Draft ────────────────────────────────────────

    [Fact]
    public async Task Rechaza_cancelar_retencion_en_Draft()
    {
        var fx = new Fixture();
        var document = DraftDocument(BranchId, UserId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(document.Id, "Motivo cualquiera"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ── 19) Rechaza cancelar Cancelled (dos veces) ────────────────────────

    [Fact]
    public async Task Rechaza_cancelar_retencion_ya_cancelada()
    {
        var fx = new Fixture();
        var document = IssuedDocument(BranchId, UserId);
        document.Cancel("Primera anulación", UserId);
        document.ClearDomainEvents();
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(document.Id, "Segunda anulación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ── 20) cancelledBy viene del usuario actual, no del body ─────────────

    [Fact]
    public async Task CancelledBy_viene_de_ICurrentUser_no_del_body()
    {
        var fx = new Fixture();
        var document = IssuedDocument(BranchId, UserId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.CancelledBy.Should().Be(UserId);
        result.Value.CancelledBy.Should().NotBe(OtherUserId);
    }

    // ── 21) Respeta scope tenant/company/branch ───────────────────────────

    [Fact]
    public async Task Retencion_de_otra_sucursal_falla_cerrado_con_NotFound()
    {
        var fx = new Fixture();
        var document = IssuedDocument(OtherBranchId, UserId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(document.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Retencion_inexistente_o_de_otro_tenant_devuelve_NotFound()
    {
        var fx = new Fixture();
        fx.RetentionRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionDocument?)null);

        var result = await fx.Handler.Handle(
            new CancelRetentionCommand(Guid.NewGuid(), "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    private sealed class Fixture
    {
        public Mock<IRetentionDocumentRepository> RetentionRepo { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();

        public CancelRetentionHandler Handler =>
            new(
                RetentionRepo.Object,
                Uow.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            );

        public void SetupDocument(RetentionDocument document) =>
            RetentionRepo
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
    }
}
