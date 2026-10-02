using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036 D-9) — XML y RIDE de una retención se autorizan por
/// su documento ORIGEN, server-side, con el <see cref="RetentionSourceAccess"/> real detrás de los
/// handlers reales: Compra → <c>purchases.view</c>; Gasto → <c>expenses.documents.view</c>. Sin
/// acceso, 404 (fail-closed) y nunca se genera XML/PDF.
/// </summary>
public sealed class RetentionSourceAccessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly string[] PurchasesUser = [PurchasePermissions.View];
    private static readonly string[] ExpensesUser = [ExpensePermissions.DocumentsView];

    private sealed class Fixture
    {
        public Mock<IRetentionDocumentRepository> Retentions { get; } = new();
        public Mock<IRetentionElectronicDocumentXmlService> XmlService { get; } = new();
        public Mock<IRetentionRidePdfService> PdfService { get; } = new();
        public RetentionDocument Retention { get; }

        public Fixture(RetentionSourceDocumentType sourceType, string[] granted, Guid? companyId = null)
        {
            Retention = RetentionDocument.Create(
                TenantId,
                companyId ?? CompanyId,
                Guid.NewGuid(),
                sourceType,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                UserId
            );
            Retentions
                .Setup(r => r.GetByIdAsync(TenantId, Retention.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Retention);
            XmlService
                .Setup(s => s.GenerateXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Result<ElectronicDocumentXml>.Success(
                        new ElectronicDocumentXml(
                            "<comprobanteRetencion/>",
                            "UTF-8",
                            "2.0.0",
                            ElectronicDocumentType.Retention,
                            "1",
                            new string('1', 49),
                            DateTime.UtcNow
                        )
                    )
                );
            PdfService
                .Setup(s =>
                    s.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Result<byte[]>.Success([1, 2, 3]));

            var authorizer = new Mock<IRuntimePermissionAuthorizer>();
            authorizer
                .Setup(a => a.IsAuthorizedAsync(It.IsAny<string>(), UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, Guid _, string _, CancellationToken _) => granted.Contains(key));
            Access = new RetentionSourceAccess(
                authorizer.Object,
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                Tenant,
                Company,
                Retentions.Object
            );
        }

        private ICurrentTenant Tenant { get; } = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        private ICurrentCompany Company { get; } = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        private RetentionSourceAccess Access { get; }

        public Task<Result<ElectronicDocumentXml>> GetXmlAsync() =>
            new GenerateRetentionXmlHandler(XmlService.Object, Tenant, Company, Access)
                .Handle(new GenerateRetentionXmlQuery(Retention.Id), CancellationToken.None);

        public Task<Result<byte[]>> GetRideAsync() =>
            new GenerateRetentionRidePdfHandler(XmlService.Object, PdfService.Object, Tenant, Company, Access)
                .Handle(new GenerateRetentionRidePdfQuery(Retention.Id), CancellationToken.None);

        public void VerifyNothingGenerated()
        {
            XmlService.Verify(
                s => s.GenerateXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
            PdfService.Verify(
                s => s.GeneratePdfAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }
    }

    // ── 15. Usuario de Compras ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Usuario_de_Compras_ve_XML_y_RIDE_de_la_retencion_de_una_compra()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, PurchasesUser);

        (await fx.GetXmlAsync()).IsSuccess.Should().BeTrue();
        (await fx.GetRideAsync()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_de_Compras_no_obtiene_XML_ni_RIDE_de_la_retencion_de_un_gasto()
    {
        var fx = new Fixture(RetentionSourceDocumentType.ExpenseDocument, PurchasesUser);

        (await fx.GetXmlAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.GetRideAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyNothingGenerated();
    }

    // ── 16. Usuario de Gastos ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Usuario_de_Gastos_ve_XML_y_RIDE_de_la_retencion_de_un_gasto()
    {
        var fx = new Fixture(RetentionSourceDocumentType.ExpenseDocument, ExpensesUser);

        (await fx.GetXmlAsync()).IsSuccess.Should().BeTrue();
        (await fx.GetRideAsync()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_de_Gastos_no_obtiene_XML_ni_RIDE_de_la_retencion_de_una_compra()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, ExpensesUser);

        (await fx.GetXmlAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.GetRideAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyNothingGenerated();
    }

    // ── Fail-closed ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Retencion_de_origen_Manual_se_deniega_aun_con_todos_los_permisos()
    {
        var fx = new Fixture(
            RetentionSourceDocumentType.Manual,
            [PurchasePermissions.View, ExpensePermissions.DocumentsView]
        );

        (await fx.GetXmlAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyNothingGenerated();
    }

    [Fact]
    public async Task Retencion_de_otra_empresa_se_deniega_aun_con_permiso_del_origen()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, PurchasesUser, companyId: Guid.NewGuid());

        (await fx.GetXmlAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyNothingGenerated();
    }
}
