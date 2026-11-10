using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Pricing.UseCases.PriceLists;

// ── Commands & Queries ──────────────────────────────────────────────────

/// <summary>Campos comunes a Create/Update usados por PriceListCommonRules — evita duplicar las mismas reglas FluentValidation en ambos validadores.</summary>
public interface IPriceListFields
{
    string Name { get; }
    string CurrencyCode { get; }
    DateOnly? ValidFrom { get; }
    DateOnly? ValidUntil { get; }
    PricingRuleType? RuleType { get; }
    decimal? RuleValue { get; }
}

public sealed record CreatePriceListCommand(
    string Code,
    string Name,
    string CurrencyCode,
    bool IsDefault,
    DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null,
    PricingRuleType? RuleType = null,
    decimal? RuleValue = null
) : IRequest<Result<PriceListDto>>, ICompanyScopedRequest, IPriceListFields;

public sealed record UpdatePriceListCommand(
    Guid Id,
    string Name,
    string CurrencyCode,
    bool IsDefault,
    DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null,
    PricingRuleType? RuleType = null,
    decimal? RuleValue = null
) : IRequest<Result<PriceListDto>>, ICompanyScopedRequest, IPriceListFields;

public sealed record EnablePriceListCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record DisablePriceListCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

public sealed record GetPriceListByIdQuery(Guid Id)
    : IRequest<Result<PriceListDto>>,
        ICompanyScopedRequest;

public sealed record GetPriceListsQuery(bool? IsActive = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<PriceListDto>>>,
        ICompanyScopedRequest;

/// <summary>
/// Reasigna la lista predeterminada de forma atómica: desmarca la actual (si existe otra) y marca
/// la indicada, sin reenviar Name/CurrencyCode/etc. como en <see cref="UpdatePriceListCommand"/>.
/// Comparte reglas y auditoría con Configuración → Ventas vía <see cref="IPriceListDefaultAssigner"/>.
/// </summary>
public sealed record SetDefaultPriceListCommand(Guid Id)
    : IRequest<Result<PriceListDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

file static class PriceListCommonRules
{
    public static void Apply<T>(AbstractValidator<T> v)
        where T : IPriceListFields
    {
        v.RuleFor(x => x.Name).NotEmpty().MaximumLength(PriceList.MaxNameLength);
        v.RuleFor(x => x.CurrencyCode).NotEmpty().Length(PriceList.CurrencyCodeLen);
        v.RuleFor(x => x.ValidUntil)
            .GreaterThanOrEqualTo(x => x.ValidFrom)
            .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue)
            .WithMessage("La fecha fin no puede ser anterior a la fecha inicio.");
        v.RuleFor(x => x.RuleType).IsInEnum().When(x => x.RuleType.HasValue);
        v.RuleFor(x => x.RuleValue)
            .NotNull()
            .When(x => x.RuleType.HasValue)
            .WithMessage(
                "El valor de la regla general es obligatorio cuando se especifica un tipo de regla."
            );
    }
}

public sealed class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(PriceList.MaxCodeLength);
        PriceListCommonRules.Apply(this);
    }
}

public sealed class UpdatePriceListCommandValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        PriceListCommonRules.Apply(this);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreatePriceListHandler
    : IRequestHandler<CreatePriceListCommand, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPriceListDefaultAssigner _defaultAssigner;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreatePriceListHandler(
        IPriceListRepository repo,
        IUnitOfWork unitOfWork,
        IPriceListDefaultAssigner defaultAssigner,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _defaultAssigner = defaultAssigner;
        _t = t;
        _c = c;
        _u = u;
    }

    // SALES-PREFERENCES-INTEGRITY-01: se conserva la regla (rechazar si ya hay otra predeterminada
    // activa); la bandera se aplica solo vía IPriceListDefaultAssigner — desmarca listas
    // deshabilitadas que la conservaban y audita el cambio, en la misma transacción del alta.

    public async Task<Result<PriceListDto>> Handle(CreatePriceListCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        if (await _repo.CodeExistsAsync(tid, cmd.Code, null, ct))
            return Result<PriceListDto>.Conflict($"Código '{cmd.Code}' duplicado.");
        if (cmd.IsDefault && await _repo.DefaultExistsAsync(tid, null, ct))
            return Result<PriceListDto>.ValidationFailure(
                "Ya existe una lista de precios predeterminada activa."
            );

        PriceList pl;
        try
        {
            pl = PriceList.Create(
                tid,
                _c.CompanyId,
                cmd.Code,
                cmd.Name,
                cmd.CurrencyCode,
                isDefault: false,
                _u.UserId,
                cmd.ValidFrom,
                cmd.ValidUntil,
                cmd.RuleType,
                cmd.RuleValue
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PriceListDto>.ValidationFailure(ex.Message);
        }
        await _unitOfWork.ExecuteInTransactionAsync(
            async c =>
            {
                await _repo.AddAsync(pl, c);
                if (!cmd.IsDefault)
                    return;
                await _unitOfWork.SaveChangesAsync(c); // la lista debe existir antes de marcarla
                await _defaultAssigner.AssignAsync(tid, pl.Id, _u.UserId, c);
            },
            ct
        );
        return Result<PriceListDto>.Success(Map.ToDto(pl));
    }
}

public sealed class UpdatePriceListHandler
    : IRequestHandler<UpdatePriceListCommand, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPriceListDefaultAssigner _defaultAssigner;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdatePriceListHandler(
        IPriceListRepository repo,
        IUnitOfWork unitOfWork,
        IPriceListDefaultAssigner defaultAssigner,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _defaultAssigner = defaultAssigner;
        _t = t;
        _u = u;
    }

    // SALES-PREFERENCES-INTEGRITY-01: mismo contrato; si IsDefault cambia, la bandera se aplica vía
    // IPriceListDefaultAssigner (auditada, sin dejar otra lista con la marca) en la misma
    // transacción que el resto de la edición. Una lista deshabilitada no puede marcarse.

    public async Task<Result<PriceListDto>> Handle(UpdatePriceListCommand cmd, CancellationToken ct)
    {
        var pl = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pl is null)
            return Result<PriceListDto>.NotFound("Lista de precios no encontrada.");
        if (cmd.IsDefault && await _repo.DefaultExistsAsync(_t.TenantId, cmd.Id, ct))
            return Result<PriceListDto>.ValidationFailure(
                "Ya existe otra lista de precios predeterminada activa."
            );

        try
        {
            pl.Update(
                cmd.Name,
                cmd.CurrencyCode,
                pl.IsDefault,
                _u.UserId,
                cmd.ValidFrom,
                cmd.ValidUntil,
                cmd.RuleType,
                cmd.RuleValue
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PriceListDto>.ValidationFailure(ex.Message);
        }

        var rejected = false;
        await _unitOfWork.ExecuteInTransactionAsync(
            async c =>
            {
                if (cmd.IsDefault == pl.IsDefault)
                    return;
                // true → esta lista; false → quitar la predeterminada (sin lista, aplica el PVP).
                var assignment = await _defaultAssigner.AssignAsync(
                    _t.TenantId,
                    cmd.IsDefault ? pl.Id : null,
                    _u.UserId,
                    c
                );
                if (assignment.IsSuccess)
                    return;
                rejected = true;
                _unitOfWork.ClearChangeTracker(); // descarta también la edición de campos
            },
            ct
        );
        if (rejected)
            return Result<PriceListDto>.ValidationFailure(PriceListDefaultMessages.Inactive);
        return Result<PriceListDto>.Success(Map.ToDto(pl));
    }
}

public sealed class EnablePriceListHandler : IRequestHandler<EnablePriceListCommand, Result<bool>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public EnablePriceListHandler(IPriceListRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(EnablePriceListCommand cmd, CancellationToken ct)
    {
        var pl = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pl is null)
            return Result<bool>.NotFound("No encontrada.");
        pl.Enable(_u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class DisablePriceListHandler : IRequestHandler<DisablePriceListCommand, Result<bool>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DisablePriceListHandler(IPriceListRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(DisablePriceListCommand cmd, CancellationToken ct)
    {
        var pl = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pl is null)
            return Result<bool>.NotFound("No encontrada.");
        pl.Disable(_u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class SetDefaultPriceListHandler
    : IRequestHandler<SetDefaultPriceListCommand, Result<PriceListDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPriceListDefaultAssigner _defaultAssigner;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public SetDefaultPriceListHandler(
        IUnitOfWork unitOfWork,
        IPriceListDefaultAssigner defaultAssigner,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _unitOfWork = unitOfWork;
        _defaultAssigner = defaultAssigner;
        _t = t;
        _u = u;
    }

    // SALES-PREFERENCES-INTEGRITY-01: las reglas del flip y su auditoría viven solo en
    // IPriceListDefaultAssigner (compartido con Configuración → Ventas), en una transacción sin cambios parciales.
    public async Task<Result<PriceListDto>> Handle(
        SetDefaultPriceListCommand cmd,
        CancellationToken ct
    )
    {
        PriceListDefaultAssignment assignment = null!;
        await _unitOfWork.ExecuteInTransactionAsync(
            async c => assignment = await _defaultAssigner.AssignAsync(_t.TenantId, cmd.Id, _u.UserId, c),
            ct
        );
        switch (assignment.Error)
        {
            case PriceListDefaultAssignmentError.NotFound:
                return Result<PriceListDto>.NotFound(PriceListDefaultMessages.NotFound);
            case PriceListDefaultAssignmentError.Inactive:
                return Result<PriceListDto>.ValidationFailure(PriceListDefaultMessages.Inactive);
        }

        return Result<PriceListDto>.Success(Map.ToDto(assignment.Default!));
    }
}

public sealed class GetPriceListByIdHandler
    : IRequestHandler<GetPriceListByIdQuery, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPriceListByIdHandler(IPriceListRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PriceListDto>> Handle(GetPriceListByIdQuery q, CancellationToken ct)
    {
        var pl = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return pl is null
            ? Result<PriceListDto>.NotFound("No encontrada.")
            : Result<PriceListDto>.Success(Map.ToDto(pl));
    }
}

public sealed class GetPriceListsHandler
    : IRequestHandler<GetPriceListsQuery, Result<IReadOnlyList<PriceListDto>>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPriceListsHandler(IPriceListRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<PriceListDto>>> Handle(
        GetPriceListsQuery q,
        CancellationToken ct
    )
    {
        var all = await _repo.GetAllAsync(_t.TenantId, q.IsActive, q.Search, ct);
        var dtos = all.Select(Map.ToDto).ToList();
        return Result<IReadOnlyList<PriceListDto>>.Success(dtos);
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static PriceListDto ToDto(PriceList p) =>
        new(
            p.Id,
            p.Code,
            p.Name,
            p.CurrencyCode,
            p.IsDefault,
            p.ValidFrom,
            p.ValidUntil,
            p.RuleType?.ToString(),
            p.RuleValue,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt
        );
}
