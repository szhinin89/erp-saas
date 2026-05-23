using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateSupplierProfile;

public sealed class UpdateSupplierProfileHandler
    : IRequestHandler<UpdateSupplierProfileCommand, Result<bool>>
{
    private readonly ISupplierProfileRepository _repo;
    private readonly ICurrentUser _currentUser;

    public UpdateSupplierProfileHandler(ISupplierProfileRepository repo, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateSupplierProfileCommand command, CancellationToken ct)
    {
        var sp = await _repo.GetByBusinessPartnerIdAsync(command.BusinessPartnerId, ct);
        if (sp is null)
            return Result<bool>.Failure("Perfil de proveedor no encontrado para este BusinessPartner.");

        try
        {
            sp.Update(
                command.DefaultTaxSupportCode,
                command.DefaultRetentionVatCode,
                command.DefaultRetentionIncomeCode,
                command.PaymentTerms,
                _currentUser.UserId);
        }
        catch (ArgumentException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
