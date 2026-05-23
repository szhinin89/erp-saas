using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AddBusinessPartnerRole;

public sealed class AddBusinessPartnerRoleHandler
    : IRequestHandler<AddBusinessPartnerRoleCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICustomerProfileRepository _cpRepo;
    private readonly ISupplierProfileRepository _spRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public AddBusinessPartnerRoleHandler(
        IBusinessPartnerRepository bpRepo,
        ICustomerProfileRepository cpRepo,
        ISupplierProfileRepository spRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _bpRepo = bpRepo;
        _cpRepo = cpRepo;
        _spRepo = spRepo;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(AddBusinessPartnerRoleCommand command, CancellationToken ct)
    {
        var bp = await _bpRepo.GetByIdAsync(command.Id, ct);
        if (bp is null)
            return Result<bool>.NotFound("BusinessPartner no encontrado.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;
        var changed = false;

        if (command.AsCustomer)
        {
            var exists = await _cpRepo.ExistsForBusinessPartnerAsync(bp.Id, ct);
            if (!exists)
            {
                var cp = CustomerProfile.Create(subscriberId, bp.Id, userId);
                await _cpRepo.AddAsync(cp, ct);
                changed = true;
            }
        }

        if (command.AsSupplier)
        {
            var exists = await _spRepo.ExistsForBusinessPartnerAsync(bp.Id, ct);
            if (!exists)
            {
                var sp = SupplierProfile.Create(subscriberId, bp.Id, userId);
                await _spRepo.AddAsync(sp, ct);
                changed = true;
            }
        }

        if (changed)
            await _bpRepo.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
