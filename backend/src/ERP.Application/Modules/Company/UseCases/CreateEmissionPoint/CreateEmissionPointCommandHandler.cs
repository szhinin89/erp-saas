using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;

public sealed class CreateEmissionPointCommandHandler
    : IRequestHandler<CreateEmissionPointCommand, Result<EmissionPointDto>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly IEstablishmentRepository _establishments;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateEmissionPointCommandHandler(
        IEmissionPointRepository repo,
        IEstablishmentRepository establishments,
        ICurrentTenant currentTenant,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _repo = repo;
        _establishments = establishments;
        _currentTenant = currentTenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<EmissionPointDto>> Handle(CreateEmissionPointCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _company.CompanyId;

        var estab = await _establishments.GetByIdAsync(tenantId, command.EstablishmentId, cancellationToken);
        if (estab is null || !estab.IsActive)
            return Result<EmissionPointDto>.Failure("El establecimiento no existe o no está activo.");

        var code = command.Code.Trim().PadLeft(3, '0');
        var exists = await _repo.ExistsAsync(tenantId, command.EstablishmentId, code, cancellationToken);
        if (exists)
            return Result<EmissionPointDto>.Failure($"Ya existe un punto de emisión con código {code} en este establecimiento.");

        if (command.IsDefault)
            await _repo.ClearDefaultExceptAsync(tenantId, command.EstablishmentId, null, _user.UserId, cancellationToken);

        var entity = EmissionPoint.Create(
            tenantId: tenantId,
            companyId: companyId,
            establishmentId: command.EstablishmentId,
            code: command.Code,
            name: command.Name,
            emissionType: command.EmissionType,
            isDefault: command.IsDefault,
            createdBy: _user.UserId);

        await _repo.AddAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EmissionPointDto>.Success(GetEmissionPointsByEstablishmentQueryHandler.ToDto(entity));
    }
}
