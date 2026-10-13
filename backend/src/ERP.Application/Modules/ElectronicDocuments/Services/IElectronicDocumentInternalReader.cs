using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>In-process reads for trusted consumers; never an HTTP authorization bypass.</summary>
public interface IElectronicDocumentInternalReader
{
    Task<Result<ElectronicDocumentDto?>> GetAsync(Guid tenantId, Guid companyId,
        string sourceModule, Guid sourceEntityId, CancellationToken ct = default);
    Task<Result<string>> GetXmlAsync(Guid tenantId, Guid companyId,
        string sourceModule, Guid sourceEntityId, ElectronicDocumentXmlVariant variant,
        CancellationToken ct = default);
}

/// <summary>Reuses the canonical read handlers after fail-closed verification of explicit scope.</summary>
public sealed class ElectronicDocumentInternalReader(
    IElectronicDocumentRepository repository, IFileStorage storage,
    ICurrentTenant tenant, ICurrentCompany company) : IElectronicDocumentInternalReader
{
    private bool Matches(Guid tenantId, Guid companyId) =>
        tenantId != Guid.Empty && companyId != Guid.Empty
        && tenantId == tenant.TenantId && company.HasCompanyContext && companyId == company.CompanyId;

    public Task<Result<ElectronicDocumentDto?>> GetAsync(Guid tenantId, Guid companyId,
        string sourceModule, Guid sourceEntityId, CancellationToken ct = default) =>
        Matches(tenantId, companyId)
            ? new GetElectronicDocumentQueryHandler(repository, tenant, company)
                .Handle(new GetElectronicDocumentQuery(sourceModule, sourceEntityId), ct)
            : Task.FromResult(Result<ElectronicDocumentDto?>.Forbidden("Alcance de lectura inválido."));

    public Task<Result<string>> GetXmlAsync(Guid tenantId, Guid companyId,
        string sourceModule, Guid sourceEntityId, ElectronicDocumentXmlVariant variant,
        CancellationToken ct = default) =>
        Matches(tenantId, companyId)
            ? new GetElectronicDocumentXmlQueryHandler(repository, storage, tenant, company)
                .Handle(new GetElectronicDocumentXmlQuery(sourceModule, sourceEntityId, variant), ct)
            : Task.FromResult(Result<string>.Forbidden("Alcance de lectura inválido."));
}
