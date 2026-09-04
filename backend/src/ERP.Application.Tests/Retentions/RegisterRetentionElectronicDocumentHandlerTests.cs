using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-SRI-MANUAL-REGISTER-04E — cubre <see cref="RegisterRetentionElectronicDocumentHandler"/>:
/// valida existencia/tenant/company/estado ANTES de delegar en
/// <see cref="IElectronicDocumentIssuer.RegisterAsync"/> — nunca firma/envía/consulta autorización
/// por su cuenta.
/// </summary>
public sealed class RegisterRetentionElectronicDocumentHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SourceDocumentId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static RetentionDocument DraftDocument(Guid companyId) =>
        RetentionDocument.Create(
            TenantId,
            companyId,
            BranchId,
            RetentionSourceDocumentType.ExpenseDocument,
            SourceDocumentId,
            SupplierId,
            EmissionPointId,
            UserId
        );

    private static RetentionDocument IssuedDocument(Guid companyId)
    {
        var doc = DraftDocument(companyId);
        doc.AddLine(
            RetentionDocumentLine.Create(
                doc.Id,
                TenantId,
                RetentionTaxType.Vat,
                "725",
                "Retención IVA 725",
                100m,
                30m,
                30m
            )
        );
        doc.Issue("001-001-000000001", new DateOnly(2026, 9, 4), UserId);
        return doc;
    }

    private sealed class Fixture
    {
        public Mock<IRetentionDocumentRepository> RetentionRepo { get; } = new();
        public Mock<IElectronicDocumentIssuer> Issuer { get; } = new();

        public RegisterRetentionElectronicDocumentHandler Handler =>
            new(
                RetentionRepo.Object,
                Issuer.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            );

        public void SetupRetention(RetentionDocument? document) =>
            RetentionRepo
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
    }

    private static ElectronicDocumentDto SampleDto(Guid electronicDocumentId) =>
        new(
            electronicDocumentId,
            "Retention",
            "Retentions",
            SourceDocumentId,
            "Signed",
            new string('1', 49),
            null,
            null,
            0,
            null,
            DateTime.UtcNow,
            null
        );

    [Fact]
    public void Validator_rejects_an_empty_retention_id()
    {
        var validator = new RegisterRetentionElectronicDocumentValidator();

        var result = validator.Validate(new RegisterRetentionElectronicDocumentCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_retention()
    {
        var fx = new Fixture();
        fx.SetupRetention(null);

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La retención no existe.");
        fx.Issuer.Verify(
            i => i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_retention_from_another_company()
    {
        var fx = new Fixture();
        fx.SetupRetention(IssuedDocument(OtherCompanyId));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La retención no existe.");
        fx.Issuer.Verify(
            i => i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_rejects_a_draft_retention_without_calling_the_issuer()
    {
        var fx = new Fixture();
        fx.SetupRetention(DraftDocument(CompanyId));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
        fx.Issuer.Verify(
            i => i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_rejects_a_cancelled_retention_without_calling_the_issuer()
    {
        var fx = new Fixture();
        var document = IssuedDocument(CompanyId);
        document.Cancel("Anulación de prueba.", UserId);
        fx.SetupRetention(document);

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
        fx.Issuer.Verify(
            i => i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_calls_the_issuer_with_the_correct_reference_for_an_issued_retention()
    {
        var fx = new Fixture();
        var document = IssuedDocument(CompanyId);
        fx.SetupRetention(document);
        var expectedDto = SampleDto(Guid.NewGuid());
        RegisterElectronicDocumentRequest? captured = null;
        fx.Issuer
            .Setup(i =>
                i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<RegisterElectronicDocumentRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<ElectronicDocumentDto>.Success(expectedDto));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(expectedDto);
        captured.Should().NotBeNull();
        captured!.DocumentType.Should().Be(ElectronicDocumentType.Retention);
        captured.SourceModule.Should().Be("Retentions");
        captured.SourceEntityId.Should().Be(document.Id);
        captured.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task Handle_propagates_a_failure_from_the_issuer()
    {
        var fx = new Fixture();
        fx.SetupRetention(IssuedDocument(CompanyId));
        fx.Issuer
            .Setup(i =>
                i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ElectronicDocumentDto>.Failure(
                    "No hay un validador de esquema registrado para el tipo de documento 'Retention'."
                )
            );

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("validador de esquema");
    }

    [Fact]
    public async Task Handle_preserves_the_issuer_idempotency_when_it_returns_conflict()
    {
        // RegisterAsync ya es idempotente (Conflict si ya existe un ElectronicDocument en un
        // estado posterior a Draft/Failed) — el handler no agrega ni interfiere con esa lógica.
        var fx = new Fixture();
        fx.SetupRetention(IssuedDocument(CompanyId));
        fx.Issuer
            .Setup(i =>
                i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ElectronicDocumentDto>.Conflict(
                    "Ya existe un documento electrónico registrado para este documento de origen."
                )
            );

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("CONFLICT");
    }
}
