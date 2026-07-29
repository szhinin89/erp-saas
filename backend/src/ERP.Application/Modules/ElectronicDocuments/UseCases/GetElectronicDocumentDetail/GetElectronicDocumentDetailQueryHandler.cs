using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;

public sealed class GetElectronicDocumentDetailQueryHandler
    : IRequestHandler<GetElectronicDocumentDetailQuery, Result<ElectronicDocumentDetailDto>>
{
    private const int TimelineTake = 20;

    private readonly IElectronicDocumentRepository _repository;
    private readonly ISourceDocumentSummaryProviderResolver _summaryResolver;
    private readonly ICompanyRepository _companyRepository;
    private readonly IAuditReader<ElectronicDocumentAudit> _auditReader;
    private readonly IAuditReader<ElectronicDocumentSriMessage> _sriMessageReader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentDetailQueryHandler(
        IElectronicDocumentRepository repository,
        ISourceDocumentSummaryProviderResolver summaryResolver,
        ICompanyRepository companyRepository,
        IAuditReader<ElectronicDocumentAudit> auditReader,
        IAuditReader<ElectronicDocumentSriMessage> sriMessageReader,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _summaryResolver = summaryResolver;
        _companyRepository = companyRepository;
        _auditReader = auditReader;
        _sriMessageReader = sriMessageReader;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentDetailDto>> Handle(
        GetElectronicDocumentDetailQuery query,
        CancellationToken cancellationToken
    )
    {
        var document = await _repository.GetByIdAsync(
            _currentTenant.TenantId,
            query.Id,
            cancellationToken
        );
        if (document is null)
            return Result<ElectronicDocumentDetailDto>.NotFound(
                "El documento electrónico no existe."
            );

        if (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
            return Result<ElectronicDocumentDetailDto>.NotFound(
                "El documento electrónico no existe."
            );

        var company = await _companyRepository.GetByIdForTenantAsync(
            document.CompanyId,
            _currentTenant.TenantId,
            cancellationToken
        );

        string? documentNumber = null;
        string? counterpartyName = null;
        var summaryProvider = _summaryResolver.Resolve(document.SourceModule);
        if (summaryProvider is not null)
        {
            var summaries = await summaryProvider.GetSummariesAsync(
                _currentTenant.TenantId,
                new[] { document.SourceEntityId },
                cancellationToken
            );
            if (summaries.TryGetValue(document.SourceEntityId, out var summary))
            {
                documentNumber = summary.DocumentNumber;
                counterpartyName = summary.CounterpartyName;
            }
        }

        var auditRecords = await ElectronicDocumentTimelineBuilder.FetchRecordsAsync(
            _auditReader,
            _currentTenant.TenantId,
            document.Id,
            TimelineTake,
            cancellationToken
        );
        var lastReason = auditRecords.FirstOrDefault()?.Reason;

        var diagnostic = await ElectronicDocumentDiagnosticAssembler.BuildAsync(
            document,
            auditRecords,
            _sriMessageReader,
            cancellationToken
        );

        var dto = new ElectronicDocumentDetailDto(
            document.Id,
            document.CompanyId,
            company?.TradeName ?? company?.LegalName ?? "—",
            company?.TaxIdentificationNumber ?? "—",
            document.DocumentType.ToString(),
            document.SourceModule,
            document.SourceEntityId,
            documentNumber,
            counterpartyName,
            document.CreatedAt,
            document.UpdatedAt,
            lastReason,
            diagnostic
        );

        return Result<ElectronicDocumentDetailDto>.Success(dto);
    }
}
