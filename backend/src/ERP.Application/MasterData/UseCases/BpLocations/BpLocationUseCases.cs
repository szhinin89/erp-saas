using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentValidation;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BpLocations;

// ── Commands ──────────────────────────────────────────────────────────────────

/// <summary>
/// Crea una ubicación del BP. Tenant-scoped (scope cambiado en Fase 4 — ADR-BP-02).
/// Si IsPrimary=true, el handler limpia el flag en otras ubicaciones antes de crear.
/// OtherDescription es obligatorio si LocationType = Other (validado en dominio).
/// </summary>
public sealed record CreateBpLocationCommand(
    Guid BusinessPartnerId,
    string Name,
    LocationType Type,
    LocationPurpose Purpose,
    string AddressLine,
    string? ProvinceCode = null,
    string? CantonCode = null,
    string? ParishCode = null,
    string? Phone = null,
    string? Email = null,
    bool IsPrimary = false,
    string? OtherDescription = null
) : IRequest<Result<BpLocationDto>>, ITenantScopedRequest;

public sealed record UpdateBpLocationCommand(
    Guid LocationId,
    string Name,
    LocationType Type,
    LocationPurpose Purpose,
    string AddressLine,
    string? ProvinceCode = null,
    string? CantonCode = null,
    string? ParishCode = null,
    string? Phone = null,
    string? Email = null,
    string? OtherDescription = null
) : IRequest<Result<BpLocationDto>>, ITenantScopedRequest;

public sealed record SetPrimaryBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

public sealed record DeactivateBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

public sealed record ActivateBpLocationCommand(Guid LocationId)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetBpLocationsQuery(Guid BusinessPartnerId, bool? OnlyActive = true)
    : IRequest<Result<IReadOnlyList<BpLocationDto>>>,
        ITenantScopedRequest;

public sealed record GetBpLocationByIdQuery(Guid LocationId)
    : IRequest<Result<BpLocationDto>>,
        ITenantScopedRequest;

// ── Validators ────────────────────────────────────────────────────────────────

public sealed class CreateBpLocationValidator : AbstractValidator<CreateBpLocationCommand>
{
    public CreateBpLocationValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BusinessPartnerLocation.NameMaxLen);
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de ubicación inválido.");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(PhysicalAddress.AddressLineMaxLen);

        RuleFor(x => x.OtherDescription)
            .NotEmpty()
            .WithMessage("OtherDescription es obligatorio cuando LocationType = Other.")
            .When(x => x.Type == LocationType.Other);

        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);

        RuleFor(x => x.CantonCode)
            .NotEmpty()
            .WithMessage("CantonCode es obligatorio cuando se especifica ParishCode.")
            .When(x => x.ParishCode is not null);

        RuleFor(x => x.ProvinceCode)
            .NotEmpty()
            .WithMessage("ProvinceCode es obligatorio cuando se especifica CantonCode.")
            .When(x => x.CantonCode is not null);
    }
}

public sealed class UpdateBpLocationValidator : AbstractValidator<UpdateBpLocationCommand>
{
    public UpdateBpLocationValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BusinessPartnerLocation.NameMaxLen);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(PhysicalAddress.AddressLineMaxLen);
        RuleFor(x => x.OtherDescription).NotEmpty().When(x => x.Type == LocationType.Other);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
    }
}

// ── Handlers ──────────────────────────────────────────────────────────────────

public sealed class CreateBpLocationHandler
    : IRequestHandler<CreateBpLocationCommand, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public CreateBpLocationHandler(
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_locRepo, _ctx) = (locRepo, ctx);

    public async Task<Result<BpLocationDto>> Handle(
        CreateBpLocationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (cmd.IsPrimary)
            await _locRepo.ClearPrimaryAsync(cmd.BusinessPartnerId, cancellationToken);

        BusinessPartnerLocation loc;
        try
        {
            loc = BusinessPartnerLocation.Create(
                _ctx.TenantId,
                cmd.BusinessPartnerId,
                cmd.Name,
                cmd.Type,
                cmd.Purpose,
                cmd.AddressLine,
                _ctx.UserId,
                cmd.ProvinceCode,
                cmd.CantonCode,
                cmd.ParishCode,
                cmd.Phone,
                cmd.Email,
                cmd.IsPrimary,
                cmd.OtherDescription
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BpLocationDto>.ValidationFailure(ex.Message);
        }

        await _locRepo.AddAsync(loc, cancellationToken);
        await _locRepo.SaveChangesAsync(cancellationToken);
        return Result<BpLocationDto>.Success(BpLocationDto.From(loc));
    }
}

public sealed class UpdateBpLocationHandler
    : IRequestHandler<UpdateBpLocationCommand, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public UpdateBpLocationHandler(
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_locRepo, _ctx) = (locRepo, ctx);

    public async Task<Result<BpLocationDto>> Handle(
        UpdateBpLocationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var loc = await _locRepo.GetByIdAsync(cmd.LocationId, cancellationToken);
        if (loc is null)
            return Result<BpLocationDto>.NotFound("Ubicación no encontrada.");

        try
        {
            loc.Update(
                cmd.Name,
                cmd.Type,
                cmd.Purpose,
                cmd.AddressLine,
                _ctx.UserId,
                cmd.ProvinceCode,
                cmd.CantonCode,
                cmd.ParishCode,
                cmd.Phone,
                cmd.Email,
                cmd.OtherDescription
            );
        }
        catch (ArgumentException ex)
        {
            return Result<BpLocationDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<BpLocationDto>.ValidationFailure(ex.Message);
        }

        await _locRepo.SaveChangesAsync(cancellationToken);
        return Result<BpLocationDto>.Success(BpLocationDto.From(loc));
    }
}

public sealed class SetPrimaryBpLocationHandler
    : IRequestHandler<SetPrimaryBpLocationCommand, Result<bool>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public SetPrimaryBpLocationHandler(
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_locRepo, _ctx) = (locRepo, ctx);

    public async Task<Result<bool>> Handle(
        SetPrimaryBpLocationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var loc = await _locRepo.GetByIdAsync(cmd.LocationId, cancellationToken);
        if (loc is null)
            return Result<bool>.NotFound("Ubicación no encontrada.");

        await _locRepo.ClearPrimaryAsync(loc.BusinessPartnerId, cancellationToken);

        try
        {
            loc.SetPrimary(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _locRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class DeactivateBpLocationHandler
    : IRequestHandler<DeactivateBpLocationCommand, Result<bool>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public DeactivateBpLocationHandler(
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_locRepo, _ctx) = (locRepo, ctx);

    public async Task<Result<bool>> Handle(
        DeactivateBpLocationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var loc = await _locRepo.GetByIdAsync(cmd.LocationId, cancellationToken);
        if (loc is null)
            return Result<bool>.NotFound("Ubicación no encontrada.");

        // Verificar contactos activos antes de desactivar (Problema 7, Fase 4)
        var hasContacts = await _locRepo.HasActiveContactsAsync(cmd.LocationId, cancellationToken);
        if (hasContacts)
            return Result<bool>.ValidationFailure(
                "La ubicación tiene contactos activos. Reasigne o desactive los contactos antes de desactivar la ubicación."
            );

        try
        {
            loc.Deactivate(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _locRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class ActivateBpLocationHandler
    : IRequestHandler<ActivateBpLocationCommand, Result<bool>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;
    private readonly IOperationalContext _ctx;

    public ActivateBpLocationHandler(
        IBusinessPartnerLocationRepository locRepo,
        IOperationalContext ctx
    ) => (_locRepo, _ctx) = (locRepo, ctx);

    public async Task<Result<bool>> Handle(
        ActivateBpLocationCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var loc = await _locRepo.GetByIdAsync(cmd.LocationId, cancellationToken);
        if (loc is null)
            return Result<bool>.NotFound("Ubicación no encontrada.");

        try
        {
            loc.Activate(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _locRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetBpLocationsHandler
    : IRequestHandler<GetBpLocationsQuery, Result<IReadOnlyList<BpLocationDto>>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;

    public GetBpLocationsHandler(IBusinessPartnerLocationRepository locRepo) => _locRepo = locRepo;

    public async Task<Result<IReadOnlyList<BpLocationDto>>> Handle(
        GetBpLocationsQuery q,
        CancellationToken cancellationToken
    )
    {
        var locs = await _locRepo.GetByBusinessPartnerAsync(
            q.BusinessPartnerId,
            q.OnlyActive,
            cancellationToken
        );
        return Result<IReadOnlyList<BpLocationDto>>.Success(
            (IReadOnlyList<BpLocationDto>)locs.Select(BpLocationDto.From).ToList()
        );
    }
}

public sealed class GetBpLocationByIdHandler
    : IRequestHandler<GetBpLocationByIdQuery, Result<BpLocationDto>>
{
    private readonly IBusinessPartnerLocationRepository _locRepo;

    public GetBpLocationByIdHandler(IBusinessPartnerLocationRepository locRepo) =>
        _locRepo = locRepo;

    public async Task<Result<BpLocationDto>> Handle(
        GetBpLocationByIdQuery q,
        CancellationToken cancellationToken
    )
    {
        var loc = await _locRepo.GetByIdAsync(q.LocationId, cancellationToken);
        return loc is null
            ? Result<BpLocationDto>.NotFound("Ubicación no encontrada.")
            : Result<BpLocationDto>.Success(BpLocationDto.From(loc));
    }
}
