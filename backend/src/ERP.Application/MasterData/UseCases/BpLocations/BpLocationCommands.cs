using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BpLocations;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateBpLocationCommand(
    Guid         BusinessPartnerId,
    string       Name,
    LocationType Type,
    string       Address,
    string?      ProvinceCode,
    string?      CantonCode,
    string?      ParishCode,
    string?      Phone,
    string?      Email,
    bool         IsPrimary)
    : IRequest<Result<BpLocationDto>>, ICompanyScopedRequest;

public sealed record UpdateBpLocationCommand(
    Guid         LocationId,
    string       Name,
    LocationType Type,
    string       Address,
    string?      ProvinceCode,
    string?      CantonCode,
    string?      ParishCode,
    string?      Phone,
    string?      Email)
    : IRequest<Result<BpLocationDto>>, ICompanyScopedRequest;

public sealed record SetPrimaryBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed record DeactivateBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed record ActivateBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetBpLocationsQuery(Guid BusinessPartnerId, bool? OnlyActive = true)
    : IRequest<Result<IReadOnlyList<BpLocationDto>>>, ICompanyScopedRequest;

public sealed record GetBpLocationByIdQuery(Guid LocationId)
    : IRequest<Result<BpLocationDto>>, ICompanyScopedRequest;

// ── Handlers ──────────────────────────────────────────────────────────────────

public sealed class CreateBpLocationHandler
    : IRequestHandler<CreateBpLocationCommand, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateBpLocationHandler(
        IBusinessPartnerLocationRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company, ICurrentUser user)
    { _repo = repo; _sub = sub; _company = company; _user = user; }

    public async Task<Result<BpLocationDto>> Handle(CreateBpLocationCommand cmd, CancellationToken ct)
    {
        try
        {
            if (cmd.IsPrimary)
                await _repo.ClearPrimaryAsync(_sub.SubscriberId, _company.CompanyId, cmd.BusinessPartnerId, ct);

            var loc = BusinessPartnerLocation.Create(
                _sub.SubscriberId, _company.CompanyId, cmd.BusinessPartnerId,
                cmd.Name, cmd.Type, cmd.Address, _user.UserId,
                cmd.ProvinceCode, cmd.CantonCode, cmd.ParishCode,
                cmd.Phone, cmd.Email, cmd.IsPrimary);

            await _repo.AddAsync(loc, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<BpLocationDto>.Success(BpLocationDto.From(loc));
        }
        catch (ArgumentException ex) { return Result<BpLocationDto>.Failure(ex.Message); }
    }
}

public sealed class UpdateBpLocationHandler
    : IRequestHandler<UpdateBpLocationCommand, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _repo;
    private readonly ICurrentUser _user;

    public UpdateBpLocationHandler(IBusinessPartnerLocationRepository repo, ICurrentUser user)
    { _repo = repo; _user = user; }

    public async Task<Result<BpLocationDto>> Handle(UpdateBpLocationCommand cmd, CancellationToken ct)
    {
        var loc = await _repo.GetByIdAsync(cmd.LocationId, ct);
        if (loc is null) return Result<BpLocationDto>.Failure("Ubicación no encontrada.");

        try
        {
            loc.Update(cmd.Name, cmd.Type, cmd.Address, _user.UserId,
                cmd.ProvinceCode, cmd.CantonCode, cmd.ParishCode, cmd.Phone, cmd.Email);
            await _repo.SaveChangesAsync(ct);
            return Result<BpLocationDto>.Success(BpLocationDto.From(loc));
        }
        catch (ArgumentException ex) { return Result<BpLocationDto>.Failure(ex.Message); }
    }
}

public sealed class SetPrimaryBpLocationHandler
    : IRequestHandler<SetPrimaryBpLocationCommand, Result<bool>>
{
    private readonly IBusinessPartnerLocationRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public SetPrimaryBpLocationHandler(
        IBusinessPartnerLocationRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company, ICurrentUser user)
    { _repo = repo; _sub = sub; _company = company; _user = user; }

    public async Task<Result<bool>> Handle(SetPrimaryBpLocationCommand cmd, CancellationToken ct)
    {
        var loc = await _repo.GetByIdAsync(cmd.LocationId, ct);
        if (loc is null) return Result<bool>.Failure("Ubicación no encontrada.");
        if (!loc.IsActive) return Result<bool>.Failure("No se puede marcar como principal una ubicación inactiva.");

        await _repo.ClearPrimaryAsync(_sub.SubscriberId, _company.CompanyId, loc.BusinessPartnerId, ct);
        loc.SetPrimary(_user.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class DeactivateBpLocationHandler
    : IRequestHandler<DeactivateBpLocationCommand, Result<bool>>
{
    private readonly IBusinessPartnerLocationRepository _repo;
    private readonly ICurrentUser _user;

    public DeactivateBpLocationHandler(IBusinessPartnerLocationRepository repo, ICurrentUser user)
    { _repo = repo; _user = user; }

    public async Task<Result<bool>> Handle(DeactivateBpLocationCommand cmd, CancellationToken ct)
    {
        var loc = await _repo.GetByIdAsync(cmd.LocationId, ct);
        if (loc is null) return Result<bool>.Failure("Ubicación no encontrada.");
        try { loc.Deactivate(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetBpLocationsHandler
    : IRequestHandler<GetBpLocationsQuery, Result<IReadOnlyList<BpLocationDto>>>
{
    private readonly IBusinessPartnerLocationRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;

    public GetBpLocationsHandler(
        IBusinessPartnerLocationRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company)
    { _repo = repo; _sub = sub; _company = company; }

    public async Task<Result<IReadOnlyList<BpLocationDto>>> Handle(GetBpLocationsQuery q, CancellationToken ct)
    {
        var locs = await _repo.GetByBusinessPartnerAsync(
            _sub.SubscriberId, _company.CompanyId, q.BusinessPartnerId, q.OnlyActive, ct);
        return Result<IReadOnlyList<BpLocationDto>>.Success(locs.Select(BpLocationDto.From).ToList());
    }
}

public sealed class GetBpLocationByIdHandler
    : IRequestHandler<GetBpLocationByIdQuery, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _repo;

    public GetBpLocationByIdHandler(IBusinessPartnerLocationRepository repo) => _repo = repo;

    public async Task<Result<BpLocationDto>> Handle(GetBpLocationByIdQuery q, CancellationToken ct)
    {
        var loc = await _repo.GetByIdAsync(q.LocationId, ct);
        return loc is null
            ? Result<BpLocationDto>.Failure("Ubicación no encontrada.")
            : Result<BpLocationDto>.Success(BpLocationDto.From(loc));
    }
}
