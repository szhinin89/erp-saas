using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — Company Scope (IDOR) del detalle de Monitor. Un
/// documento electrónico pertenece a una única empresa (CompanyId): Empresa A no debe poder leer
/// el detalle de un documento de Empresa B, sea del mismo tenant o de otro tenant, aunque conozca
/// su Id interno. Mismo patrón de guard ya aplicado en GetElectronicDocumentXml/Timeline/Retry
/// (ERP-CORE-CLOSEOUT-07).
/// </summary>
public sealed class GetElectronicDocumentDetailQueryHandlerTests
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

    private static Mock<ISourceDocumentSummaryProviderResolver> NoSummaryResolver()
    {
        var mock = new Mock<ISourceDocumentSummaryProviderResolver>();
        mock.Setup(r => r.Resolve(It.IsAny<string>()))
            .Returns((ISourceDocumentSummaryProvider?)null);
        return mock;
    }

    private static Mock<ICompanyRepository> NoCompanyRepository()
    {
        var mock = new Mock<ICompanyRepository>();
        mock.Setup(r =>
                r.GetByIdForTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Domain.Modules.Company.Entities.Company?)null);
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

    private static Mock<IAuditReader<ElectronicDocumentSriMessage>> EmptySriMessageReader()
    {
        var mock = new Mock<IAuditReader<ElectronicDocumentSriMessage>>();
        mock.Setup(r =>
                r.GetByEntityAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<ElectronicDocumentSriMessage>());
        return mock;
    }

    private static GetElectronicDocumentDetailQueryHandler NewHandler(
        Mock<IElectronicDocumentRepository> repository,
        Mock<ICurrentTenant> tenant,
        Mock<ICurrentCompany> company
    ) =>
        new(
            repository.Object,
            NoSummaryResolver().Object,
            NoCompanyRepository().Object,
            EmptyAuditReader().Object,
            EmptySriMessageReader().Object,
            tenant.Object,
            company.Object
        );

    [Fact]
    public async Task Detalle_de_la_propia_empresa_se_devuelve()
    {
        var document = NewDocument(CompanyAId);
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = NewHandler(repository, TenantContext(TenantId), CompanyContext(CompanyAId));

        var result = await handler.Handle(
            new GetElectronicDocumentDetailQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Id.Should().Be(document.Id);
        result.Value.CompanyId.Should().Be(CompanyAId);
    }

    [Fact]
    public async Task Detalle_de_otra_empresa_del_mismo_tenant_devuelve_NotFound()
    {
        var document = NewDocument(CompanyBId);
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = NewHandler(repository, TenantContext(TenantId), CompanyContext(CompanyAId));

        var result = await handler.Handle(
            new GetElectronicDocumentDetailQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Detalle_de_documento_de_otro_tenant_devuelve_NotFound_sin_cruzar_datos()
    {
        // El repositorio real solo devuelve filas cuyo TenantId coincide con el parámetro
        // recibido (equivalente al filtro EF global fail-closed) — aquí lo modelamos con Moq
        // configurando el mock únicamente para TenantId: una consulta hecha por un actor de
        // OtherTenantId nunca matchea el setup y Moq devuelve el default (null), igual que
        // pasaría con 0 filas en producción.
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
            new GetElectronicDocumentDetailQuery(document.Id),
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
