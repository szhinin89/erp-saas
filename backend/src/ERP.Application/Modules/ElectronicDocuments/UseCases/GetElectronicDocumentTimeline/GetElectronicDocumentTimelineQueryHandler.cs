using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentTimeline;

public sealed class GetElectronicDocumentTimelineQueryHandler
    : IRequestHandler<
        GetElectronicDocumentTimelineQuery,
        Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>
    >
{
    private const int TimelineTake = 20;

    private readonly IElectronicDocumentRepository _repository;
    private readonly IAuditReader<ElectronicDocumentAudit> _auditReader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentTimelineQueryHandler(
        IElectronicDocumentRepository repository,
        IAuditReader<ElectronicDocumentAudit> auditReader,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _auditReader = auditReader;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>> Handle(
        GetElectronicDocumentTimelineQuery query,
        CancellationToken cancellationToken
    )
    {
        var document = await _repository.GetByIdAsync(
            _currentTenant.TenantId,
            query.Id,
            cancellationToken
        );
        if (document is null)
            return Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>.NotFound(
                "El documento electrónico no existe."
            );

        if (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
            return Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>.NotFound(
                "El documento electrónico no existe."
            );

        var records = await ElectronicDocumentTimelineBuilder.FetchRecordsAsync(
            _auditReader,
            _currentTenant.TenantId,
            document.Id,
            TimelineTake,
            cancellationToken
        );

        return Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>.Success(
            ElectronicDocumentTimelineBuilder.Build(records)
        );
    }
}
