using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocument;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// ERP-CORE-CLOSEOUT-07 — GetBySourceAsync solo filtra por TenantId; sin el chequeo explícito de
/// CompanyId, cualquier usuario del tenant podía consultar (y, vía RIDE, obtener el PDF de) el
/// documento electrónico de otra empresa por sourceEntityId. Mismo patrón ya aplicado en
/// GetElectronicDocumentDetailQueryHandler.
/// </summary>
public sealed class GetElectronicDocumentQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SourceModule = "Sales";

    private sealed class Fixture
    {
        public Mock<IElectronicDocumentRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Fixture(Guid activeCompanyId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(activeCompanyId);
            Company.Setup(c => c.HasCompanyContext).Returns(true);
        }

        public GetElectronicDocumentQueryHandler BuildHandler() =>
            new(Repo.Object, Tenant.Object, Company.Object);
    }

    private static ElectronicDocument CreateDocument(Guid companyId, Guid sourceEntityId) =>
        ElectronicDocument.Create(
            TenantId,
            companyId,
            ElectronicDocumentType.Invoice,
            SourceModule,
            sourceEntityId,
            UserId
        );

    [Fact]
    public async Task Documento_de_la_propia_empresa_se_devuelve()
    {
        var sourceEntityId = Guid.NewGuid();
        var document = CreateDocument(CompanyAId, sourceEntityId);
        var f = new Fixture(activeCompanyId: CompanyAId);
        f.Repo.Setup(r => r.GetBySourceAsync(TenantId, SourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await f.BuildHandler()
            .Handle(new GetElectronicDocumentQuery(SourceModule, sourceEntityId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Documento_de_otra_empresa_del_mismo_tenant_se_oculta_como_null()
    {
        var sourceEntityId = Guid.NewGuid();
        var document = CreateDocument(CompanyBId, sourceEntityId);
        var f = new Fixture(activeCompanyId: CompanyAId);
        f.Repo.Setup(r => r.GetBySourceAsync(TenantId, SourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await f.BuildHandler()
            .Handle(new GetElectronicDocumentQuery(SourceModule, sourceEntityId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeNull();
    }

    /// <summary>
    /// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — cross-tenant explícito: GetBySourceAsync solo se
    /// mockea para <see cref="TenantId"/> (equivalente al filtro EF fail-closed real); un actor
    /// de otro tenant nunca matchea ese Setup, aunque el CompanyId activo coincida por accidente.
    /// </summary>
    [Fact]
    public async Task Documento_de_otro_tenant_se_oculta_como_null()
    {
        var otherTenantId = Guid.NewGuid();
        var sourceEntityId = Guid.NewGuid();
        var document = CreateDocument(CompanyAId, sourceEntityId);
        var f = new Fixture(activeCompanyId: CompanyAId);
        f.Tenant.Setup(t => t.TenantId).Returns(otherTenantId);
        f.Repo.Setup(r => r.GetBySourceAsync(TenantId, SourceModule, sourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await f.BuildHandler()
            .Handle(new GetElectronicDocumentQuery(SourceModule, sourceEntityId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeNull();
    }
}
