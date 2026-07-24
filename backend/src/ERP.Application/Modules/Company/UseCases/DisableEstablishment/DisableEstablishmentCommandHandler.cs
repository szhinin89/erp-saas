using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.DisableEstablishment;

public sealed class DisableEstablishmentCommandHandler : IRequestHandler<DisableEstablishmentCommand, Result<bool>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant       _currentTenant;
    private readonly ICurrentUser             _user;

    public DisableEstablishmentCommandHandler(IEstablishmentRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo; _currentTenant = tenant; _user = user;
    }

    public async Task<Result<bool>> Handle(DisableEstablishmentCommand command, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(_currentTenant.TenantId, command.Id, cancellationToken);
        if (entity is null) return Result<bool>.Failure("Establecimiento no encontrado.");

        if (await _repo.HasActiveEmissionPointsAsync(_currentTenant.TenantId, command.Id, cancellationToken))
            return Result<bool>.Failure(
                "No se puede desactivar el establecimiento porque tiene puntos de emisión activos. " +
                "Desactive primero todos sus puntos de emisión.");

        try { entity.Disable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
