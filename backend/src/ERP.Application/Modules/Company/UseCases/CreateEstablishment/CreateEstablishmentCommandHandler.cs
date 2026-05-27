using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.CreateEstablishment;

public sealed class CreateEstablishmentCommandHandler
    : IRequestHandler<CreateEstablishmentCommand, Result<EstablishmentDto>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly IBranchRepository        _branches;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentCompany          _company;
    private readonly ICurrentUser             _user;

    public CreateEstablishmentCommandHandler(
        IEstablishmentRepository repo,
        IBranchRepository branches,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _repo       = repo;
        _branches   = branches;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public async Task<Result<EstablishmentDto>> Handle(CreateEstablishmentCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;

        var branch = await _branches.GetByIdAsync(subscriberId, command.BranchId, ct);
        if (branch is null || !branch.IsActive)
            return Result<EstablishmentDto>.Failure("La sucursal no existe o no está activa.");

        var code = command.Code.Trim().PadLeft(3, '0');
        var exists = await _repo.ExistsAsync(subscriberId, command.BranchId, code, ct);
        if (exists)
            return Result<EstablishmentDto>.Failure($"Ya existe un establecimiento con código {code} en esta sucursal.");

        var entity = Establishment.Create(
            subscriberId: subscriberId,
            branchId:     command.BranchId,
            companyId:    companyId,
            code:         command.Code,
            name:         command.Name,
            address:      command.Address,
            phone:        command.Phone,
            isMain:       command.IsMain,
            createdBy:    _user.UserId);

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<EstablishmentDto>.Success(GetEstablishmentsByBranchQueryHandler.ToDto(entity));
    }
}
