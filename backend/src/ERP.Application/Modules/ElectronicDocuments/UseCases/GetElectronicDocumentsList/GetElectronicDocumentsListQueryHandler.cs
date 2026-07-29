using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsList;

public sealed class GetElectronicDocumentsListQueryHandler
    : IRequestHandler<GetElectronicDocumentsListQuery, Result<ElectronicDocumentsListResponse>>
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly ISourceDocumentSummaryProviderResolver _summaryResolver;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentsListQueryHandler(
        IElectronicDocumentRepository repository,
        ISourceDocumentSummaryProviderResolver summaryResolver,
        ICompanyRepository companyRepository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany)
    {
        _repository = repository;
        _summaryResolver = summaryResolver;
        _companyRepository = companyRepository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentsListResponse>> Handle(
        GetElectronicDocumentsListQuery query, CancellationToken cancellationToken)
    {
        List<ElectronicDocumentState>? states = null;
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            states = new List<ElectronicDocumentState>();
            foreach (var raw in query.State.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<ElectronicDocumentState>(raw, true, out var parsedState))
                    return Result<ElectronicDocumentsListResponse>.ValidationFailure($"Estado '{raw}' inválido.");
                states.Add(parsedState);
            }
        }

        ElectronicDocumentType? documentType = null;
        if (!string.IsNullOrWhiteSpace(query.DocumentType))
        {
            if (!Enum.TryParse<ElectronicDocumentType>(query.DocumentType, true, out var parsedType))
                return Result<ElectronicDocumentsListResponse>.ValidationFailure($"Tipo de documento '{query.DocumentType}' inválido.");
            documentType = parsedType;
        }

        var companyId = _currentCompany.HasCompanyContext ? _currentCompany.CompanyId : (Guid?)null;

        var (items, total) = await _repository.GetPagedAsync(
            _currentTenant.TenantId,
            companyId,
            query.DateFrom,
            query.DateTo,
            states,
            documentType,
            query.Environment,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        // Resumen de origen (número + contraparte): un ISourceDocumentSummaryProvider por
        // SourceModule — el Monitor nunca conoce Sales/Purchases/etc. directamente.
        var summaries = new Dictionary<Guid, SourceDocumentSummary>();
        foreach (var group in items.GroupBy(x => x.SourceModule))
        {
            var provider = _summaryResolver.Resolve(group.Key);
            if (provider is null) continue;

            var sourceIds = group.Select(x => x.SourceEntityId).Distinct().ToList();
            var moduleSummaries = await provider.GetSummariesAsync(_currentTenant.TenantId, sourceIds, cancellationToken);
            foreach (var kv in moduleSummaries) summaries[kv.Key] = kv.Value;
        }

        var companyIds = items.Select(x => x.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetByIdsAsync(companyIds, cancellationToken);
        var companyNames = companies.ToDictionary(c => c.Id, c => c.TradeName ?? c.LegalName);

        var dtos = items.Select(x =>
        {
            summaries.TryGetValue(x.SourceEntityId, out var summary);
            companyNames.TryGetValue(x.CompanyId, out var companyName);

            return new ElectronicDocumentListItemDto(
                x.Id,
                x.CreatedAt,
                x.DocumentType.ToString(),
                summary?.DocumentNumber,
                x.CompanyId,
                companyName ?? "—",
                summary?.CounterpartyName,
                x.CurrentState.ToString(),
                x.Environment,
                x.AccessKey?.Value,
                x.RetryCount,
                x.UpdatedAt,
                x.LastError);
        }).ToList();

        return Result<ElectronicDocumentsListResponse>.Success(
            new ElectronicDocumentsListResponse(dtos, total, query.PageNumber, query.PageSize));
    }
}
