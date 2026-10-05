using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BpContacts;

// ── Commands ──────────────────────────────────────────────────────────────────

/// <summary>
/// Crea un contacto del BP. Tenant-scoped (scope cambiado en Fase 4 — ADR-BP-02).
/// OtherDescription es obligatorio si ContactRole = Other.
/// LocationId debe pertenecer al mismo BusinessPartner (validado en handler).
/// </summary>
public sealed record CreateBpContactCommand(
    Guid BusinessPartnerId,
    string FirstName,
    ContactRole Role,
    Guid? LocationId = null,
    string? LastName = null,
    string? Position = null,
    string? Phone = null,
    string? Mobile = null,
    string? Email = null,
    string? Notes = null,
    bool IsPrimary = false,
    string? OtherDescription = null
) : IRequest<Result<BpContactDto>>, ITenantScopedRequest;

public sealed record UpdateBpContactCommand(
    Guid BusinessPartnerId,
    Guid ContactId,
    string FirstName,
    ContactRole Role,
    Guid? LocationId = null,
    string? LastName = null,
    string? Position = null,
    string? Phone = null,
    string? Mobile = null,
    string? Email = null,
    string? Notes = null,
    string? OtherDescription = null
) : IRequest<Result<BpContactDto>>, ITenantScopedRequest;

public sealed record SetPrimaryBpContactCommand(Guid BusinessPartnerId, Guid ContactId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

public sealed record DeactivateBpContactCommand(Guid BusinessPartnerId, Guid ContactId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

public sealed record ActivateBpContactCommand(Guid BusinessPartnerId, Guid ContactId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetBpContactsQuery(Guid BusinessPartnerId, bool? OnlyActive = true)
    : IRequest<Result<IReadOnlyList<BpContactDto>>>,
        ITenantScopedRequest;

public sealed record GetBpContactByIdQuery(Guid BusinessPartnerId, Guid ContactId)
    : IRequest<Result<BpContactDto>>,
        ITenantScopedRequest;

// ── Validators ────────────────────────────────────────────────────────────────

public sealed class CreateBpContactValidator : AbstractValidator<CreateBpContactCommand>
{
    public CreateBpContactValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(BusinessPartnerContact.FirstNameMaxLen);
        RuleFor(x => x.Role).IsInEnum().WithMessage("Rol de contacto inválido.");

        RuleFor(x => x.OtherDescription)
            .NotEmpty()
            .WithMessage("OtherDescription es obligatorio cuando ContactRole = Other.")
            .When(x => x.Role == ContactRole.Other);

        RuleFor(x => x.LastName)
            .MaximumLength(BusinessPartnerContact.LastNameMaxLen)
            .When(x => x.LastName is not null);
        RuleFor(x => x.Position)
            .MaximumLength(BusinessPartnerContact.PositionMaxLen)
            .When(x => x.Position is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Notes)
            .MaximumLength(BusinessPartnerContact.NotesMaxLen)
            .When(x => x.Notes is not null);
    }
}

public sealed class UpdateBpContactValidator : AbstractValidator<UpdateBpContactCommand>
{
    public UpdateBpContactValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(BusinessPartnerContact.FirstNameMaxLen);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.OtherDescription).NotEmpty().When(x => x.Role == ContactRole.Other);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

// ── Handlers ──────────────────────────────────────────────────────────────────

public sealed class CreateBpContactHandler
    : IRequestHandler<CreateBpContactCommand, Result<BpContactDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerContactRepository _contactRepo;
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public CreateBpContactHandler(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerContactRepository contactRepo,
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_bpRepo, _contactRepo, _locRepo, _ctx) = (bpRepo, contactRepo, locRepo, ctx);

    public async Task<Result<BpContactDto>> Handle(
        CreateBpContactCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // ZH-BP-NESTED-RESOURCE-OWNERSHIP-01: el bpId de la ruta debe ser un BP visible en el
        // scope (el repositorio aplica el query filter de tenant) — nunca crear hijos colgando
        // de un BP de otro tenant o inexistente.
        if (await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, cancellationToken) is null)
            return Result<BpContactDto>.NotFound("BusinessPartner no encontrado.");

        var locationError = await BpContactLocationGuard.ValidateAsync(
            _locRepo,
            cmd.BusinessPartnerId,
            cmd.LocationId,
            cancellationToken
        );
        if (locationError is not null)
            return Result<BpContactDto>.ValidationFailure(locationError);

        if (cmd.IsPrimary)
            await _contactRepo.ClearPrimaryAsync(cmd.BusinessPartnerId, cancellationToken);

        BusinessPartnerContact contact;
        try
        {
            contact = BusinessPartnerContact.Create(
                _ctx.TenantId,
                cmd.BusinessPartnerId,
                cmd.FirstName,
                cmd.Role,
                _ctx.UserId,
                cmd.LocationId,
                cmd.LastName,
                cmd.Position,
                cmd.Phone,
                cmd.Mobile,
                cmd.Email,
                cmd.Notes,
                cmd.IsPrimary,
                cmd.OtherDescription
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BpContactDto>.ValidationFailure(ex.Message);
        }

        await _contactRepo.AddAsync(contact, cancellationToken);
        await _contactRepo.SaveChangesAsync(cancellationToken);
        return Result<BpContactDto>.Success(BpContactDto.From(contact));
    }
}

public sealed class UpdateBpContactHandler
    : IRequestHandler<UpdateBpContactCommand, Result<BpContactDto>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public UpdateBpContactHandler(
        IBusinessPartnerContactRepository contactRepo,
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_contactRepo, _locRepo, _ctx) = (contactRepo, locRepo, ctx);

    public async Task<Result<BpContactDto>> Handle(
        UpdateBpContactCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var contact = await _contactRepo.GetByIdAsync(cmd.ContactId, cancellationToken);
        if (contact is null || contact.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<BpContactDto>.NotFound("Contacto no encontrado.");

        var locationError = await BpContactLocationGuard.ValidateAsync(
            _locRepo,
            cmd.BusinessPartnerId,
            cmd.LocationId,
            cancellationToken
        );
        if (locationError is not null)
            return Result<BpContactDto>.ValidationFailure(locationError);

        try
        {
            contact.Update(
                cmd.FirstName,
                cmd.Role,
                _ctx.UserId,
                cmd.LocationId,
                cmd.LastName,
                cmd.Position,
                cmd.Phone,
                cmd.Mobile,
                cmd.Email,
                cmd.Notes,
                cmd.OtherDescription
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BpContactDto>.ValidationFailure(ex.Message);
        }

        await _contactRepo.SaveChangesAsync(cancellationToken);
        return Result<BpContactDto>.Success(BpContactDto.From(contact));
    }
}

public sealed class SetPrimaryBpContactHandler
    : IRequestHandler<SetPrimaryBpContactCommand, Result<bool>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;
    private readonly IOperationalContext _ctx;

    public SetPrimaryBpContactHandler(
        IBusinessPartnerContactRepository contactRepo,
        IOperationalContext ctx
    ) => (_contactRepo, _ctx) = (contactRepo, ctx);

    public async Task<Result<bool>> Handle(
        SetPrimaryBpContactCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var contact = await _contactRepo.GetByIdAsync(cmd.ContactId, cancellationToken);
        if (contact is null || contact.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<bool>.NotFound("Contacto no encontrado.");

        await _contactRepo.ClearPrimaryAsync(contact.BusinessPartnerId, cancellationToken);

        contact.SetPrimary(_ctx.UserId);

        await _contactRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DeactivateBpContactHandler
    : IRequestHandler<DeactivateBpContactCommand, Result<bool>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;
    private readonly IOperationalContext _ctx;

    public DeactivateBpContactHandler(
        IBusinessPartnerContactRepository contactRepo,
        IOperationalContext ctx
    ) => (_contactRepo, _ctx) = (contactRepo, ctx);

    public async Task<Result<bool>> Handle(
        DeactivateBpContactCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var contact = await _contactRepo.GetByIdAsync(cmd.ContactId, cancellationToken);
        if (contact is null || contact.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<bool>.NotFound("Contacto no encontrado.");

        contact.Deactivate(_ctx.UserId);

        await _contactRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class ActivateBpContactHandler
    : IRequestHandler<ActivateBpContactCommand, Result<bool>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;
    private readonly IOperationalContext _ctx;

    public ActivateBpContactHandler(
        IBusinessPartnerContactRepository contactRepo,
        IOperationalContext ctx
    ) => (_contactRepo, _ctx) = (contactRepo, ctx);

    public async Task<Result<bool>> Handle(
        ActivateBpContactCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var contact = await _contactRepo.GetByIdAsync(cmd.ContactId, cancellationToken);
        if (contact is null || contact.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<bool>.NotFound("Contacto no encontrado.");

        contact.Activate(_ctx.UserId);

        await _contactRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetBpContactsHandler
    : IRequestHandler<GetBpContactsQuery, Result<IReadOnlyList<BpContactDto>>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;

    public GetBpContactsHandler(IBusinessPartnerContactRepository contactRepo) =>
        _contactRepo = contactRepo;

    public async Task<Result<IReadOnlyList<BpContactDto>>> Handle(
        GetBpContactsQuery q,
        CancellationToken cancellationToken
    )
    {
        var contacts = await _contactRepo.GetByBusinessPartnerAsync(
            q.BusinessPartnerId,
            q.OnlyActive,
            cancellationToken
        );
        return Result<IReadOnlyList<BpContactDto>>.Success(
            (IReadOnlyList<BpContactDto>)contacts.Select(BpContactDto.From).ToList()
        );
    }
}

public sealed class GetBpContactByIdHandler
    : IRequestHandler<GetBpContactByIdQuery, Result<BpContactDto>>
{
    private readonly IBusinessPartnerContactRepository _contactRepo;

    public GetBpContactByIdHandler(IBusinessPartnerContactRepository contactRepo) =>
        _contactRepo = contactRepo;

    public async Task<Result<BpContactDto>> Handle(
        GetBpContactByIdQuery q,
        CancellationToken cancellationToken
    )
    {
        var contact = await _contactRepo.GetByIdAsync(q.ContactId, cancellationToken);
        return contact is null || contact.BusinessPartnerId != q.BusinessPartnerId
            ? Result<BpContactDto>.NotFound("Contacto no encontrado.")
            : Result<BpContactDto>.Success(BpContactDto.From(contact));
    }
}

/// <summary>
/// ZH-BP-NESTED-RESOURCE-OWNERSHIP-01 — la LocationId opcional de un contacto debe ser una
/// ubicación del mismo BusinessPartner. Mismo mensaje si no existe o es de otro BP: no revela
/// existencia cross-parent.
/// </summary>
internal static class BpContactLocationGuard
{
    internal const string InvalidLocationMessage =
        "La ubicación indicada no pertenece a este BusinessPartner.";

    internal static async Task<string?> ValidateAsync(
        IBusinessPartnerLocationRepository locRepo,
        Guid businessPartnerId,
        Guid? locationId,
        CancellationToken cancellationToken
    )
    {
        if (locationId is not Guid id)
            return null;

        var location = await locRepo.GetByIdAsync(id, cancellationToken);
        return location is null || location.BusinessPartnerId != businessPartnerId
            ? InvalidLocationMessage
            : null;
    }
}
