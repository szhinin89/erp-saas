using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;

public sealed class CreateEmissionPointCommandHandler
    : IRequestHandler<CreateEmissionPointCommand, Result<EmissionPointDto>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly IEstablishmentRepository _establishments;
    private readonly ICurrentSubscriber       _subscriber;
    private readonly ICurrentCompany          _company;
    private readonly ICurrentUser             _user;

    public CreateEmissionPointCommandHandler(
        IEmissionPointRepository repo,
        IEstablishmentRepository establishments,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _repo           = repo;
        _establishments = establishments;
        _subscriber     = subscriber;
        _company        = company;
        _user           = user;
    }

    public async Task<Result<EmissionPointDto>> Handle(CreateEmissionPointCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;

        var estab = await _establishments.GetByIdAsync(subscriberId, command.EstablishmentId, ct);
        if (estab is null || !estab.IsActive)
            return Result<EmissionPointDto>.Failure("El establecimiento no existe o no está activo.");

        var code = command.Code.Trim().PadLeft(3, '0');
        var exists = await _repo.ExistsAsync(subscriberId, command.EstablishmentId, code, ct);
        if (exists)
            return Result<EmissionPointDto>.Failure($"Ya existe un punto de emisión con código {code} en este establecimiento.");

        if (command.IsDefault)
            await _repo.ClearDefaultExceptAsync(subscriberId, command.EstablishmentId, null, _user.UserId, ct);

        var entity = EmissionPoint.Create(
            subscriberId:    subscriberId,
            companyId:       companyId,
            establishmentId: command.EstablishmentId,
            code:            command.Code,
            name:            command.Name,
            isDefault:       command.IsDefault,
            createdBy:       _user.UserId);

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<EmissionPointDto>.Success(GetEmissionPointsByEstablishmentQueryHandler.ToDto(entity));
    }
}
