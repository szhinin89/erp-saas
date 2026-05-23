using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.DisableBusinessPartner;

public sealed class DisableBusinessPartnerHandler
    : IRequestHandler<DisableBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _repo;
    private readonly ICurrentUser               _currentUser;

    public DisableBusinessPartnerHandler(IBusinessPartnerRepository repo, ICurrentUser currentUser)
    {
        _repo        = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DisableBusinessPartnerCommand command, CancellationToken ct)
    {
        var bp = await _repo.GetByIdAsync(command.Id, ct);
        if (bp is null)
            return Result<bool>.Failure("BusinessPartner no encontrado.");

        try { bp.Deactivate(_currentUser.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
