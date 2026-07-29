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

    public GetElectronicDocumentQueryHandler(
        IElectronicDocumentRepository repository,
        ICurrentTenant currentTenant
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
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

        return Result<ElectronicDocumentDto?>.Success(
            document is null ? null : ElectronicDocumentMapper.ToDto(document)
        );
    }
}
