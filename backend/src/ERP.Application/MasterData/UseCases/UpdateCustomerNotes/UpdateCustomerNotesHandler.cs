using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateCustomerNotes;

public sealed class UpdateCustomerNotesHandler
    : IRequestHandler<UpdateCustomerNotesCommand, Result<bool>>
{
    private readonly ICustomerProfileRepository _repo;
    private readonly ICurrentUser _currentUser;

    public UpdateCustomerNotesHandler(ICustomerProfileRepository repo, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateCustomerNotesCommand command, CancellationToken ct)
    {
        var cp = await _repo.GetByBusinessPartnerIdAsync(command.BusinessPartnerId, ct);
        if (cp is null)
            return Result<bool>.Failure("Perfil de cliente no encontrado para este BusinessPartner.");

        cp.UpdateNotes(command.Notes, _currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
