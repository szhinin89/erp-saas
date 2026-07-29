using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDiagnosticBySource;

public sealed class GetElectronicDocumentDiagnosticBySourceQueryHandler
    : IRequestHandler<GetElectronicDocumentDiagnosticBySourceQuery, Result<ElectronicDocumentDiagnosticDto>>
{
    private const int TimelineTake = 20;

    private readonly IElectronicDocumentRepository _repository;
    private readonly IAuditReader<ElectronicDocumentAudit> _auditReader;
    private readonly IAuditReader<ElectronicDocumentSriMessage> _sriMessageReader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentDiagnosticBySourceQueryHandler(
        IElectronicDocumentRepository repository,
        IAuditReader<ElectronicDocumentAudit> auditReader,
        IAuditReader<ElectronicDocumentSriMessage> sriMessageReader,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany)
    {
        _repository = repository;
        _auditReader = auditReader;
        _sriMessageReader = sriMessageReader;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentDiagnosticDto>> Handle(
        GetElectronicDocumentDiagnosticBySourceQuery query, CancellationToken cancellationToken)
    {
        var document = await _repository.GetBySourceAsync(
            _currentTenant.TenantId, query.SourceModule, query.SourceEntityId, cancellationToken);
        if (document is null)
            return Result<ElectronicDocumentDiagnosticDto>.NotFound(
                "El documento de origen no tiene un documento electrónico registrado.");

        if (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
            return Result<ElectronicDocumentDiagnosticDto>.NotFound(
                "El documento de origen no tiene un documento electrónico registrado.");

        var auditRecords = await ElectronicDocumentTimelineBuilder.FetchRecordsAsync(
            _auditReader, _currentTenant.TenantId, document.Id, TimelineTake, cancellationToken);

        var diagnostic = await ElectronicDocumentDiagnosticAssembler.BuildAsync(
            document, auditRecords, _sriMessageReader, cancellationToken);

        return Result<ElectronicDocumentDiagnosticDto>.Success(diagnostic);
    }
}
