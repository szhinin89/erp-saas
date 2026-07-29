using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

public sealed record DisableCashRegisterCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed class DisableCashRegisterHandler
    : IRequestHandler<DisableCashRegisterCommand, Result<bool>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly ICashRegisterUsageGuard _usageGuard;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DisableCashRegisterHandler(
        ICashRegisterRepository repo,
        ICashRegisterUsageGuard usageGuard,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _usageGuard = usageGuard;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(DisableCashRegisterCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        var entity = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (entity is null)
            return Result<bool>.NotFound("Caja no encontrada.");

        if (await _usageGuard.HasOpenSessionAsync(tid, entity.Id, ct))
        {
            return Result<bool>.ValidationFailure(
                "No se puede desactivar la caja: tiene una sesión de caja abierta. Ciérrela antes de desactivar la caja."
            );
        }

        try
        {
            entity.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
