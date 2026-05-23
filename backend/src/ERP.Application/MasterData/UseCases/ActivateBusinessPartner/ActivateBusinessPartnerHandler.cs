using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.ActivateBusinessPartner;

public sealed class ActivateBusinessPartnerHandler
    : IRequestHandler<ActivateBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _repo;
    private readonly ICurrentUser _currentUser;

    public ActivateBusinessPartnerHandler(IBusinessPartnerRepository repo, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ActivateBusinessPartnerCommand command, CancellationToken ct)
    {
        var bp = await _repo.GetByIdAsync(command.Id, ct);
        if (bp is null)
            return Result<bool>.Failure("BusinessPartner no encontrado.");

        try { bp.Activate(_currentUser.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
