using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;

public sealed class EnableEmissionPointCommandHandler : IRequestHandler<EnableEmissionPointCommand, Result<bool>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant       _currentTenant;
    private readonly ICurrentUser             _user;

    public EnableEmissionPointCommandHandler(IEmissionPointRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo; _currentTenant = tenant; _user = user;
    }

    public async Task<Result<bool>> Handle(EnableEmissionPointCommand command, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(command.Id, _currentTenant.TenantId, cancellationToken);
        if (entity is null) return Result<bool>.Failure("Punto de emisión no encontrado.");
        try { entity.Enable(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
