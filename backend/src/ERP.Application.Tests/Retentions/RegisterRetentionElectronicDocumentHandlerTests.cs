using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — <see cref="RegisterRetentionElectronicDocumentHandler"/> es
/// la acción de RECUPERACIÓN controlada: valida existencia/tenant/company y el permiso del documento
/// ORIGEN (ver y operar) y delega en el mismo <see cref="RetentionElectronicTransmission"/> que la
/// transmisión automática. El estado de la retención (solo Issued procesa) lo decide el gate SSOT del
/// emisor — cubierto contra PostgreSQL real en ERP.Infrastructure.Tests, no aquí.
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

    private static RetentionDocument IssuedDocument(
        Guid companyId,
        RetentionSourceDocumentType sourceType = RetentionSourceDocumentType.ExpenseDocument
    )
    {
        var doc = RetentionDocument.Create(
            TenantId,
            companyId,
            BranchId,
            sourceType,
            SourceDocumentId,
            SupplierId,
            EmissionPointId,
            UserId
        );
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
        public HashSet<string> Granted { get; } = new();

        public RegisterRetentionElectronicDocumentHandler Handler
        {
            get
            {
                var authorizer = new Mock<IRuntimePermissionAuthorizer>();
                authorizer
                    .Setup(a =>
                        a.IsAuthorizedAsync(
                            It.IsAny<string>(),
                            UserId,
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()
                        )
                    )
                    .ReturnsAsync((string key, Guid _, string _, CancellationToken _) => Granted.Contains(key));
                var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
                var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
                var user = Mock.Of<ICurrentUser>(u => u.UserId == UserId);
                return new(
                    new RetentionElectronicTransmission(
                        Issuer.Object,
                        NullLogger<RetentionElectronicTransmission>.Instance
                    ),
                    new RetentionSourceAccess(authorizer.Object, user, tenant, company, RetentionRepo.Object),
                    tenant,
                    company,
                    user
                );
            }
        }

        public void SetupRetention(RetentionDocument? document) =>
            RetentionRepo
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

        public void VerifyIssuerNeverCalled() =>
            Issuer.Verify(
                i => i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    private static ElectronicDocumentDto SampleDto(Guid electronicDocumentId) =>
        new(
            electronicDocumentId,
            "Retention",
            "Retentions",
            SourceDocumentId,
            "Dispatching",
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
        fx.Granted.UnionWith([ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsConfirm]);
        fx.SetupRetention(null);

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyIssuerNeverCalled();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_retention_from_another_company()
    {
        var fx = new Fixture();
        fx.Granted.UnionWith([ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsConfirm]);
        fx.SetupRetention(IssuedDocument(OtherCompanyId));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyIssuerNeverCalled();
    }

    [Fact]
    public async Task Handle_returns_not_found_when_the_user_cannot_view_the_origin()
    {
        var fx = new Fixture();
        // Usuario de Compras frente a una retención de Gasto: sus permisos no dan acceso.
        fx.Granted.UnionWith([PurchasePermissions.View, PurchasePermissions.Update]);
        fx.SetupRetention(IssuedDocument(CompanyId, RetentionSourceDocumentType.ExpenseDocument));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyIssuerNeverCalled();
    }

    [Fact]
    public async Task Handle_returns_forbidden_when_the_user_can_view_but_not_operate_the_origin()
    {
        var fx = new Fixture();
        fx.Granted.Add(PurchasePermissions.View);
        fx.SetupRetention(IssuedDocument(CompanyId, RetentionSourceDocumentType.PurchaseInvoice));

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        fx.VerifyIssuerNeverCalled();
    }

    [Theory]
    [InlineData(RetentionSourceDocumentType.PurchaseInvoice, PurchasePermissions.View, PurchasePermissions.Update)]
    [InlineData(RetentionSourceDocumentType.ExpenseDocument, ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsConfirm)]
    public async Task Handle_delegates_to_the_single_transmission_entry_with_the_origin_permissions(
        RetentionSourceDocumentType sourceType,
        string viewPermission,
        string operatePermission
    )
    {
        var fx = new Fixture();
        fx.Granted.UnionWith([viewPermission, operatePermission]);
        var document = IssuedDocument(CompanyId, sourceType);
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
    public async Task Handle_propagates_the_gate_rejection_from_the_issuer()
    {
        var fx = new Fixture();
        fx.Granted.UnionWith([ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsConfirm]);
        fx.SetupRetention(IssuedDocument(CompanyId));
        fx.Issuer
            .Setup(i =>
                i.RegisterAsync(It.IsAny<RegisterElectronicDocumentRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ElectronicDocumentDto>.ValidationFailure(
                    "La retención está anulada: su comprobante electrónico no puede procesarse.",
                    ApiResponseCodes.ElectronicDocuments.SourceNotProcessable
                )
            );

        var result = await fx.Handler.Handle(
            new RegisterRetentionElectronicDocumentCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
    }

    [Fact]
    public async Task Handle_preserves_the_issuer_idempotency_when_it_returns_conflict()
    {
        var fx = new Fixture();
        fx.Granted.UnionWith([ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsConfirm]);
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
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }
}
