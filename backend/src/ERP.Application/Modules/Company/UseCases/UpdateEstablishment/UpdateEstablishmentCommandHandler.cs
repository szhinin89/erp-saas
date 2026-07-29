using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

public sealed class UpdateEstablishmentCommandHandler
    : IRequestHandler<UpdateEstablishmentCommand, Result<EstablishmentDto>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateEstablishmentCommandHandler(
        IEstablishmentRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<EstablishmentDto>> Handle(
        UpdateEstablishmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var entity = await _repo.GetByIdAsync(
            _currentTenant.TenantId,
            command.Id,
            cancellationToken
        );
        if (entity is null)
            return Result<EstablishmentDto>.Failure("Establecimiento no encontrado.");

        entity.Update(command.Name, command.Address, command.Phone, _user.UserId);
        entity.SetMain(command.IsMain, _user.UserId);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EstablishmentDto>.Success(
            GetEstablishmentsByBranchQueryHandler.ToDto(entity)
        );
    }
}
