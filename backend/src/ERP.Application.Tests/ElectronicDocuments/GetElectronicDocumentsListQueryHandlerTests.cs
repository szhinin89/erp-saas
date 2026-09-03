using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsList;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — Monitor de Documentos Electrónicos: el listado paginado
/// debe acotarse siempre a la empresa activa del usuario (nunca cross-company ni companyId=null),
/// delegando el filtro real a <see cref="IElectronicDocumentRepository.GetPagedAsync"/> vía el
/// mismo <c>ICurrentCompany</c> que ya usan Detail/Xml/Timeline/Retry.
/// </summary>
public sealed class GetElectronicDocumentsListQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();

    private static Mock<ICurrentCompany> CompanyContext(Guid companyId)
    {
        var mock = new Mock<ICurrentCompany>();
        mock.SetupGet(c => c.CompanyId).Returns(companyId);
        mock.SetupGet(c => c.HasCompanyContext).Returns(true);
        return mock;
    }

    private static Mock<ICurrentTenant> TenantContext()
    {
        var mock = new Mock<ICurrentTenant>();
        mock.SetupGet(t => t.TenantId).Returns(TenantId);
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
                r.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<Domain.Modules.Company.Entities.Company>());
        return mock;
    }

    [Fact]
    public async Task Listado_del_Monitor_solo_pide_al_repositorio_documentos_de_la_empresa_activa()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    CompanyAId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Array.Empty<ElectronicDocument>(), 0));

        var handler = new GetElectronicDocumentsListQueryHandler(
            repository.Object,
            NoSummaryResolver().Object,
            NoCompanyRepository().Object,
            TenantContext().Object,
            CompanyContext(CompanyAId).Object
        );

        var result = await handler.Handle(
            new GetElectronicDocumentsListQuery(null, null, null, null, null, null, 1, 25),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Items.Should().BeEmpty();
        // El único Setup que Moq matchea exige exactamente CompanyAId (nunca null ni otra
        // empresa) — cualquier otra combinación habría devuelto el default de Moq (tupla vacía
        // sin excepción) y este Verify habría fallado igual, pero deja explícito que el handler
        // nunca pide "todas las empresas del tenant".
        repository.Verify(
            r =>
                r.GetPagedAsync(
                    TenantId,
                    CompanyAId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Listado_nunca_pide_companyId_null_cuando_hay_contexto_de_empresa_activo()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r =>
                r.GetPagedAsync(
                    TenantId,
                    It.IsAny<Guid?>(),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Array.Empty<ElectronicDocument>(), 0));

        var handler = new GetElectronicDocumentsListQueryHandler(
            repository.Object,
            NoSummaryResolver().Object,
            NoCompanyRepository().Object,
            TenantContext().Object,
            CompanyContext(CompanyAId).Object
        );

        await handler.Handle(
            new GetElectronicDocumentsListQuery(null, null, null, null, null, null, 1, 25),
            CancellationToken.None
        );

        // companyId=null en GetPagedAsync significa "todas las empresas del tenant" (uso
        // legítimo solo del job Hangfire cross-company) — el Monitor, con contexto de empresa
        // activo, nunca debe invocarlo así.
        repository.Verify(
            r =>
                r.GetPagedAsync(
                    TenantId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    1,
                    25,
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
