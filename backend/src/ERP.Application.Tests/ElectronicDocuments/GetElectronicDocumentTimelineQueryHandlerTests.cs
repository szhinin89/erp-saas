using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentTimeline;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — Company Scope (IDOR) del timeline de Monitor. Mismo
/// patrón de guard que GetElectronicDocumentDetail/Xml/Retry (ERP-CORE-CLOSEOUT-07): un usuario
/// de la Empresa A nunca debe poder leer el timeline de auditoría de un documento de la Empresa
/// B, sea del mismo tenant o de otro tenant.
/// </summary>
public sealed class GetElectronicDocumentTimelineQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SourceModule = "Sales";

    private static ElectronicDocument NewDocument(Guid companyId) =>
        ElectronicDocument.Create(
            TenantId,
            companyId,
            ElectronicDocumentType.Invoice,
            SourceModule,
            Guid.NewGuid(),
            UserId
        );

    private static Mock<ICurrentCompany> CompanyContext(Guid companyId)
    {
        var mock = new Mock<ICurrentCompany>();
        mock.SetupGet(c => c.CompanyId).Returns(companyId);
        mock.SetupGet(c => c.HasCompanyContext).Returns(true);
        return mock;
    }

    private static Mock<ICurrentTenant> TenantContext(Guid tenantId)
    {
        var mock = new Mock<ICurrentTenant>();
        mock.SetupGet(t => t.TenantId).Returns(tenantId);
        return mock;
    }

    private static Mock<IAuditReader<ElectronicDocumentAudit>> EmptyAuditReader()
    {
        var mock = new Mock<IAuditReader<ElectronicDocumentAudit>>();
        mock.Setup(r =>
                r.GetByEntityAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<ElectronicDocumentAudit>());
        return mock;
    }

    private static GetElectronicDocumentTimelineQueryHandler NewHandler(
        Mock<IElectronicDocumentRepository> repository,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company
    ) => new(repository.Object, EmptyAuditReader().Object, tenant.Object, company.Object);

    [Fact]
    public async Task Timeline_de_la_propia_empresa_se_devuelve()
    {
        var document = NewDocument(CompanyAId);
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = NewHandler(repository, TenantContext(TenantId), CompanyContext(CompanyAId));

        var result = await handler.Handle(
            new GetElectronicDocumentTimelineQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task Timeline_de_otra_empresa_del_mismo_tenant_devuelve_NotFound()
    {
        var document = NewDocument(CompanyBId);
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = NewHandler(repository, TenantContext(TenantId), CompanyContext(CompanyAId));

        var result = await handler.Handle(
            new GetElectronicDocumentTimelineQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Timeline_de_documento_de_otro_tenant_devuelve_NotFound_sin_cruzar_datos()
    {
        var document = NewDocument(CompanyAId);
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = NewHandler(
            repository,
            TenantContext(OtherTenantId),
            CompanyContext(CompanyAId)
        );

        var result = await handler.Handle(
            new GetElectronicDocumentTimelineQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repository.Verify(
            r => r.GetByIdAsync(OtherTenantId, document.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
