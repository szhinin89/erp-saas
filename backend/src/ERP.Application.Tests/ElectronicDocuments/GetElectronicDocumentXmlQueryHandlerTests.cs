using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Moq;
using System.Text;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// ERP-CORE-CLOSEOUT-07 — GetBySourceAsync solo filtra por TenantId; sin el chequeo explícito de
/// CompanyId, cualquier usuario del tenant podía leer el XML comercial completo (borrador/
/// firmado/autorizado) de otra empresa por sourceEntityId. Mismo patrón ya aplicado en
/// GetElectronicDocumentDetailQueryHandler.
/// </summary>
public sealed class GetElectronicDocumentXmlQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SourceModule = "Sales";

    private sealed class Fixture
    {
        public Mock<IElectronicDocumentRepository> Repo { get; } = new();
        public Mock<IFileStorage> FileStorage { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Fixture(Guid activeCompanyId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(activeCompanyId);
            Company.Setup(c => c.HasCompanyContext).Returns(true);
        }

        public GetElectronicDocumentXmlQueryHandler BuildHandler() =>
            new(Repo.Object, FileStorage.Object, Tenant.Object, Company.Object);
    }

    private static ElectronicDocument CreateDocumentWithDraftXml(Guid companyId, Guid sourceEntityId)
    {
        var document = ElectronicDocument.Create(
            TenantId,
            companyId,
            ElectronicDocumentType.Invoice,
            SourceModule,
            sourceEntityId,
            UserId
        );
        document.MarkXmlGenerated("edocs/draft.xml", "1.0.0", "2.31", UserId);
        return document;
    }

    [Fact]
    public async Task Xml_de_la_propia_empresa_se_devuelve()
    {
        var sourceEntityId = Guid.NewGuid();
        var document = CreateDocumentWithDraftXml(CompanyAId, sourceEntityId);
        var f = new Fixture(activeCompanyId: CompanyAId);
        f.Repo.Setup(r => r.GetBySourceAsync(TenantId, SourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        f.FileStorage.Setup(s => s.GetAsync(document.XmlDraftPath!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<xml/>")));

        var result = await f.BuildHandler()
            .Handle(
                new GetElectronicDocumentXmlQuery(SourceModule, sourceEntityId, ElectronicDocumentXmlVariant.Draft),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be("<xml/>");
    }

    [Fact]
    public async Task Xml_de_otra_empresa_del_mismo_tenant_devuelve_NotFound_sin_leer_storage()
    {
        var sourceEntityId = Guid.NewGuid();
        var document = CreateDocumentWithDraftXml(CompanyBId, sourceEntityId);
        var f = new Fixture(activeCompanyId: CompanyAId);
        f.Repo.Setup(r => r.GetBySourceAsync(TenantId, SourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await f.BuildHandler()
            .Handle(
                new GetElectronicDocumentXmlQuery(SourceModule, sourceEntityId, ElectronicDocumentXmlVariant.Draft),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.FileStorage.Verify(
            s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
