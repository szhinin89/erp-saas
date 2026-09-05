using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;

public sealed class ConfigureDocumentSequenceCommandHandler
    : IRequestHandler<ConfigureDocumentSequenceCommand, Result<DocumentSequenceDto>>
{
    private readonly IDocumentSequenceRepository _sequenceRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly ISriDocTypeCatalogResolver _docTypeResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public ConfigureDocumentSequenceCommandHandler(
        IDocumentSequenceRepository sequenceRepo,
        IEmissionPointRepository emissionPointRepo,
        ISriDocTypeCatalogResolver docTypeResolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _sequenceRepo = sequenceRepo;
        _emissionPointRepo = emissionPointRepo;
        _docTypeResolver = docTypeResolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<DocumentSequenceDto>> Handle(
        ConfigureDocumentSequenceCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var docTypeCode = command.DocTypeCode.Trim();

        // ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A — GetByIdForCompanyAsync valida CompanyId
        // explícitamente en el predicado (defensa adicional a los query filters globales, no
        // reemplazo): esta secuencia gobierna la numeración SRI de cada documento emitido por este
        // punto de emisión, así que confiar únicamente en el filtro global sería un único punto de
        // falla para un flujo crítico.
        var emissionPoint = await _emissionPointRepo.GetByIdForCompanyAsync(
            tenantId,
            companyId,
            command.EmissionPointId,
            cancellationToken
        );
        if (emissionPoint is null)
            return Result<DocumentSequenceDto>.NotFound("Punto de emisión no encontrado.");

        if (!await _docTypeResolver.IsActiveElectronicDocTypeAsync(docTypeCode, cancellationToken))
            return Result<DocumentSequenceDto>.ValidationFailure(
                $"El tipo de comprobante SRI '{docTypeCode}' no está activo o habilitado en el catálogo."
            );

        var sequence = await _sequenceRepo.GetByEmissionPointAndDocTypeAsync(
            command.EmissionPointId,
            docTypeCode,
            cancellationToken
        );

        var isNew = sequence is null;
        sequence ??= DocumentSequence.Create(
            tenantId,
            companyId,
            command.EmissionPointId,
            docTypeCode
        );

        try
        {
            sequence.ConfigureNextNumber(command.NextNumber);
        }
        catch (InvalidOperationException ex)
        {
            // Secuencia ya usada — ajuste restringido fuera de alcance de esta fase.
            return Result<DocumentSequenceDto>.Conflict(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result<DocumentSequenceDto>.ValidationFailure(ex.Message);
        }

        if (isNew)
            await _sequenceRepo.AddAsync(sequence, cancellationToken);

        await _sequenceRepo.SaveChangesAsync(cancellationToken);

        return Result<DocumentSequenceDto>.Success(
            new DocumentSequenceDto(
                sequence.EmissionPointId,
                sequence.DocTypeCode,
                sequence.CurrentSeq,
                sequence.HasBeenUsed,
                sequence.UpdatedAt
            )
        );
    }
}
