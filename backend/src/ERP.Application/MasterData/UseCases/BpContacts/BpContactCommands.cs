using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BpContacts;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateBpContactCommand(
    Guid        BusinessPartnerId,
    string      FirstName,
    ContactRole Role,
    Guid?       LocationId,
    string?     LastName,
    string?     Position,
    string?     Phone,
    string?     Mobile,
    string?     Email,
    string?     Notes,
    bool        IsPrimary)
    : IRequest<Result<BpContactDto>>, ICompanyScopedRequest;

public sealed record UpdateBpContactCommand(
    Guid        ContactId,
    string      FirstName,
    ContactRole Role,
    Guid?       LocationId,
    string?     LastName,
    string?     Position,
    string?     Phone,
    string?     Mobile,
    string?     Email,
    string?     Notes)
    : IRequest<Result<BpContactDto>>, ICompanyScopedRequest;

public sealed record SetPrimaryBpContactCommand(Guid ContactId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

public sealed record DeactivateBpContactCommand(Guid ContactId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetBpContactsQuery(Guid BusinessPartnerId, bool? OnlyActive = true)
    : IRequest<Result<IReadOnlyList<BpContactDto>>>, ICompanyScopedRequest;

public sealed record GetBpContactByIdQuery(Guid ContactId)
    : IRequest<Result<BpContactDto>>, ICompanyScopedRequest;

// ── Handlers ──────────────────────────────────────────────────────────────────

public sealed class CreateBpContactHandler
    : IRequestHandler<CreateBpContactCommand, Result<BpContactDto>>
{
    private readonly IBusinessPartnerContactRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateBpContactHandler(
        IBusinessPartnerContactRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company, ICurrentUser user)
    { _repo = repo; _sub = sub; _company = company; _user = user; }

    public async Task<Result<BpContactDto>> Handle(CreateBpContactCommand cmd, CancellationToken ct)
    {
        try
        {
            if (cmd.IsPrimary)
                await _repo.ClearPrimaryAsync(_sub.SubscriberId, _company.CompanyId, cmd.BusinessPartnerId, ct);

            var contact = BusinessPartnerContact.Create(
                _sub.SubscriberId, _company.CompanyId, cmd.BusinessPartnerId,
                cmd.FirstName, cmd.Role, _user.UserId,
                cmd.LocationId, cmd.LastName, cmd.Position,
                cmd.Phone, cmd.Mobile, cmd.Email, cmd.Notes, cmd.IsPrimary);

            await _repo.AddAsync(contact, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<BpContactDto>.Success(BpContactDto.From(contact));
        }
        catch (ArgumentException ex) { return Result<BpContactDto>.Failure(ex.Message); }
    }
}

public sealed class UpdateBpContactHandler
    : IRequestHandler<UpdateBpContactCommand, Result<BpContactDto>>
{
    private readonly IBusinessPartnerContactRepository _repo;
    private readonly ICurrentUser _user;

    public UpdateBpContactHandler(IBusinessPartnerContactRepository repo, ICurrentUser user)
    { _repo = repo; _user = user; }

    public async Task<Result<BpContactDto>> Handle(UpdateBpContactCommand cmd, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(cmd.ContactId, ct);
        if (c is null) return Result<BpContactDto>.Failure("Contacto no encontrado.");

        try
        {
            c.Update(cmd.FirstName, cmd.Role, _user.UserId,
                cmd.LocationId, cmd.LastName, cmd.Position,
                cmd.Phone, cmd.Mobile, cmd.Email, cmd.Notes);
            await _repo.SaveChangesAsync(ct);
            return Result<BpContactDto>.Success(BpContactDto.From(c));
        }
        catch (ArgumentException ex) { return Result<BpContactDto>.Failure(ex.Message); }
    }
}

public sealed class SetPrimaryBpContactHandler
    : IRequestHandler<SetPrimaryBpContactCommand, Result<bool>>
{
    private readonly IBusinessPartnerContactRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public SetPrimaryBpContactHandler(
        IBusinessPartnerContactRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company, ICurrentUser user)
    { _repo = repo; _sub = sub; _company = company; _user = user; }

    public async Task<Result<bool>> Handle(SetPrimaryBpContactCommand cmd, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(cmd.ContactId, ct);
        if (c is null) return Result<bool>.Failure("Contacto no encontrado.");
        if (!c.IsActive) return Result<bool>.Failure("No se puede marcar como principal un contacto inactivo.");

        await _repo.ClearPrimaryAsync(_sub.SubscriberId, _company.CompanyId, c.BusinessPartnerId, ct);
        c.SetPrimary(_user.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class DeactivateBpContactHandler
    : IRequestHandler<DeactivateBpContactCommand, Result<bool>>
{
    private readonly IBusinessPartnerContactRepository _repo;
    private readonly ICurrentUser _user;

    public DeactivateBpContactHandler(IBusinessPartnerContactRepository repo, ICurrentUser user)
    { _repo = repo; _user = user; }

    public async Task<Result<bool>> Handle(DeactivateBpContactCommand cmd, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(cmd.ContactId, ct);
        if (c is null) return Result<bool>.Failure("Contacto no encontrado.");
        try { c.Deactivate(_user.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetBpContactsHandler
    : IRequestHandler<GetBpContactsQuery, Result<IReadOnlyList<BpContactDto>>>
{
    private readonly IBusinessPartnerContactRepository _repo;
    private readonly ICurrentSubscriber _sub;
    private readonly ICurrentCompany _company;

    public GetBpContactsHandler(
        IBusinessPartnerContactRepository repo,
        ICurrentSubscriber sub, ICurrentCompany company)
    { _repo = repo; _sub = sub; _company = company; }

    public async Task<Result<IReadOnlyList<BpContactDto>>> Handle(GetBpContactsQuery q, CancellationToken ct)
    {
        var contacts = await _repo.GetByBusinessPartnerAsync(
            _sub.SubscriberId, _company.CompanyId, q.BusinessPartnerId, q.OnlyActive, ct);
        return Result<IReadOnlyList<BpContactDto>>.Success(contacts.Select(BpContactDto.From).ToList());
    }
}

public sealed class GetBpContactByIdHandler
    : IRequestHandler<GetBpContactByIdQuery, Result<BpContactDto>>
{
    private readonly IBusinessPartnerContactRepository _repo;

    public GetBpContactByIdHandler(IBusinessPartnerContactRepository repo) => _repo = repo;

    public async Task<Result<BpContactDto>> Handle(GetBpContactByIdQuery q, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(q.ContactId, ct);
        return c is null
            ? Result<BpContactDto>.Failure("Contacto no encontrado.")
            : Result<BpContactDto>.Success(BpContactDto.From(c));
    }
}
