using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.EnableEstablishment;

public sealed class EnableEstablishmentCommandHandler : IRequestHandler<EnableEstablishmentCommand, Result<bool>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public EnableEstablishmentCommandHandler(IEstablishmentRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo; _currentTenant = tenant; _user = user;
    }

    public async Task<Result<bool>> Handle(EnableEstablishmentCommand command, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(_currentTenant.TenantId, command.Id, cancellationToken);
        if (entity is null) return Result<bool>.Failure("Establecimiento no encontrado.");
        try { entity.Enable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
