using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

public sealed class UpdateEmissionPointCommandHandler
    : IRequestHandler<UpdateEmissionPointCommand, Result<EmissionPointDto>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentUser             _user;

    public UpdateEmissionPointCommandHandler(IEmissionPointRepository repo, ICurrentTenant currentTenant, ICurrentUser user)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
        _user          = user;
    }

    public async Task<Result<EmissionPointDto>> Handle(UpdateEmissionPointCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var entity   = await _repo.GetByIdAsync(command.Id, tenantId, cancellationToken);
        if (entity is null) return Result<EmissionPointDto>.Failure("Punto de emisión no encontrado.");

        if (command.IsDefault && !entity.IsDefault)
            await _repo.ClearDefaultExceptAsync(tenantId, entity.EstablishmentId, command.Id, _user.UserId, cancellationToken);

        entity.Update(command.Name, command.EmissionType, _user.UserId);
        entity.SetDefault(command.IsDefault, _user.UserId);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EmissionPointDto>.Success(GetEmissionPointsByEstablishmentQueryHandler.ToDto(entity));
    }
}
