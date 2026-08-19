using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
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
/// la indicada — único punto de escritura para Configuración → Ventas (settings/company), evita
/// que la UI de Settings tenga que reenviar Name/CurrencyCode/etc. como en <see cref="UpdatePriceListCommand"/>.
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
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreatePriceListHandler(
        IPriceListRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
    }

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
                cmd.IsDefault,
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
        await _repo.AddAsync(pl, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<PriceListDto>.Success(Map.ToDto(pl));
    }
}

public sealed class UpdatePriceListHandler
    : IRequestHandler<UpdatePriceListCommand, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdatePriceListHandler(IPriceListRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

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
                cmd.IsDefault,
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
        await _repo.SaveChangesAsync(ct);
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
        try
        {
            pl.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
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
        try
        {
            pl.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class SetDefaultPriceListHandler
    : IRequestHandler<SetDefaultPriceListCommand, Result<PriceListDto>>
{
    private readonly IPriceListRepository _repo;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public SetDefaultPriceListHandler(
        IPriceListRepository repo,
        IConfigurationChangeLogger changeLogger,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _changeLogger = changeLogger;
        _t = t;
        _u = u;
    }

    public async Task<Result<PriceListDto>> Handle(
        SetDefaultPriceListCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var target = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (target is null)
            return Result<PriceListDto>.NotFound("Lista de precios no encontrada.");
        if (!target.IsActive)
            return Result<PriceListDto>.ValidationFailure(
                "No se puede marcar como predeterminada una lista deshabilitada."
            );

        if (!target.IsDefault)
        {
            var current = await _repo.GetDefaultAsync(tid, ct);
            if (current is not null && current.Id != target.Id)
            {
                current.SetDefault(false, _u.UserId);
                await LogFlagChangeAsync(current.Id, target.CompanyId, true, false, ct);
            }
            target.SetDefault(true, _u.UserId);
            await LogFlagChangeAsync(target.Id, target.CompanyId, false, true, ct);
            await _repo.SaveChangesAsync(ct);
        }

        return Result<PriceListDto>.Success(Map.ToDto(target));
    }

    // CONFIG-FOUNDATION-P2-01: registra tanto la lista que se desmarca como la que se marca —
    // el flip completo, no solo el lado "nuevo". Se agrega al mismo DbContext que _repo
    // (via IConfigurationChangeLogger), así viaja en la misma transacción que SaveChangesAsync.
    private Task LogFlagChangeAsync(
        Guid priceListId,
        Guid companyId,
        bool oldValue,
        bool newValue,
        CancellationToken ct
    ) =>
        _changeLogger.LogAsync(
            new ConfigurationChangeLogEntry(
                TenantId: _t.TenantId,
                CompanyId: companyId,
                Scope: OrgScope.Company,
                ScopeId: companyId,
                Key: null,
                EntityType: "PriceList",
                EntityId: priceListId,
                FieldName: "IsDefault",
                OldValue: oldValue ? "true" : "false",
                NewValue: newValue ? "true" : "false",
                ValueType: ConfigurationChangeValueType.Bool,
                ChangedBy: _u.UserId,
                Source: ConfigurationChangeSource.Api
            ),
            ct
        );
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
