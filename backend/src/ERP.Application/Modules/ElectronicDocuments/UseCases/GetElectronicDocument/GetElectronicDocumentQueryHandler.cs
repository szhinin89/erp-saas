using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocument;

public sealed class GetElectronicDocumentQueryHandler
    : IRequestHandler<GetElectronicDocumentQuery, Result<ElectronicDocumentDto?>>
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetElectronicDocumentQueryHandler(
        IElectronicDocumentRepository repository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentDto?>> Handle(
        GetElectronicDocumentQuery query,
        CancellationToken cancellationToken
    )
    {
        var document = await _repository.GetBySourceAsync(
            _currentTenant.TenantId,
            query.SourceModule,
            query.SourceEntityId,
            cancellationToken
        );

        // ERP-CORE-CLOSEOUT-07: GetBySourceAsync solo filtra por TenantId — sin este chequeo, el
        // pipeline de RIDE (ElectronicDocumentRideSourceXmlProvider) podía generar/servir el PDF
        // de la factura de otra empresa por sourceEntityId. Mismo patrón que
        // GetElectronicDocumentDetail/Timeline/RetryElectronicDocument.
        if (
            document is not null
            && _currentCompany.HasCompanyContext
            && document.CompanyId != _currentCompany.CompanyId
        )
            document = null;

        return Result<ElectronicDocumentDto?>.Success(
            document is null ? null : ElectronicDocumentMapper.ToDto(document)
        );
    }
}
