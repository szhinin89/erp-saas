using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Application.Modules.Caja.UseCases;

public sealed record EnableCashRegisterCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed class EnableCashRegisterHandler : IRequestHandler<EnableCashRegisterCommand, Result<bool>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public EnableCashRegisterHandler(ICashRegisterRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo; _t = t; _u = u;
    }

    public async Task<Result<bool>> Handle(EnableCashRegisterCommand cmd, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (entity is null) return Result<bool>.NotFound("Caja no encontrada.");

        try { entity.Enable(_u.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
