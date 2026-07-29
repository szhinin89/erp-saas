using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Pricing.UseCases.PricingRules;

// ── Commands & Queries ──────────────────────────────────────────────────

/// <summary>
/// Crea una excepción nueva o actualiza el valor de una ya activa. NUNCA reactiva en silencio
/// una excepción deshabilitada — si la clave (PriceListId, ItemId, ItemVariantId) coincide con
/// una fila inactiva, devuelve <see cref="PricingRuleSetStatus.ExistsInactive"/> sin tocar la
/// base de datos; la UI decide explícitamente si reactivarla vía <see cref="EnablePricingRuleCommand"/>.
/// </summary>
public sealed record SetPricingRuleCommand(
    Guid PriceListId,
    Guid ItemId,
    PricingRuleType RuleType,
    decimal RuleValue
) : IRequest<Result<PricingRuleSetResultDto>>, ICompanyScopedRequest;

public sealed record RemovePricingRuleCommand(Guid Id)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

/// <summary>Reactivación explícita — nunca se dispara automáticamente desde SetPricingRuleCommand.</summary>
public sealed record EnablePricingRuleCommand(Guid PriceListId, Guid ItemId)
    : IRequest<Result<PricingRuleDto>>,
        ICompanyScopedRequest;

public sealed record GetPricingRulesQuery(Guid? PriceListId = null, Guid? ItemId = null)
    : IRequest<Result<IReadOnlyList<PricingRuleDto>>>,
        ICompanyScopedRequest;

public sealed record GetPricingRuleByIdQuery(Guid Id)
    : IRequest<Result<PricingRuleDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class SetPricingRuleCommandValidator : AbstractValidator<SetPricingRuleCommand>
{
    public SetPricingRuleCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.RuleType).IsInEnum();
        RuleFor(x => x.RuleValue)
            .GreaterThanOrEqualTo(0)
            .When(x => x.RuleType == PricingRuleType.FixedPrice)
            .WithMessage("El precio fijo no puede ser negativo.");
    }
}

public sealed class EnablePricingRuleCommandValidator : AbstractValidator<EnablePricingRuleCommand>
{
    public EnablePricingRuleCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class SetPricingRuleHandler
    : IRequestHandler<SetPricingRuleCommand, Result<PricingRuleSetResultDto>>
{
    private readonly IPricingRuleRepository _repo;
    private readonly IPriceListRepository _plRepo;
    private readonly IPriceListItemRepository _assignmentRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public SetPricingRuleHandler(
        IPricingRuleRepository repo,
        IPriceListRepository plRepo,
        IPriceListItemRepository assignmentRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _plRepo = plRepo;
        _assignmentRepo = assignmentRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PricingRuleSetResultDto>> Handle(
        SetPricingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var pl = await _plRepo.GetByIdAsync(tid, cmd.PriceListId, ct);
        if (pl is null)
            return Result<PricingRuleSetResultDto>.NotFound("Lista de precios no encontrada.");

        // Búsqueda por clave SIN filtrar IsActive — el índice único de BD tampoco distingue
        // activo/inactivo, así que hay que saber si "ya existe deshabilitada" antes de insertar.
        var existing = await _repo.FindByKeyAsync(tid, cmd.PriceListId, cmd.ItemId, ct);

        try
        {
            if (existing is null)
            {
                // Invariante: no puede existir una PricingRule sin una PriceListItem activa que
                // la respalde — evita reglas huérfanas si el ítem nunca fue asignado a la lista
                // o si la asignación fue desactivada (ver SetItemPriceListsHandler, cascada C3).
                var assignment = await _assignmentRepo.FindByKeyAsync(
                    tid,
                    cmd.PriceListId,
                    cmd.ItemId,
                    ct
                );
                if (assignment is not { IsActive: true })
                    return Result<PricingRuleSetResultDto>.ValidationFailure(
                        "El ítem no está asignado a esta lista de precios (o la asignación está deshabilitada)."
                    );

                var rule = PricingRule.Create(
                    tid,
                    _c.CompanyId,
                    cmd.PriceListId,
                    cmd.ItemId,
                    cmd.RuleType,
                    cmd.RuleValue,
                    _u.UserId
                );
                await _repo.AddAsync(rule, ct);
                await _repo.SaveChangesAsync(ct);
                return Result<PricingRuleSetResultDto>.Success(
                    new PricingRuleSetResultDto(PricingRuleSetStatus.Created, rule.Id)
                );
            }

            if (!existing.IsActive)
            {
                // NO se reactiva en silencio ni se intenta un INSERT duplicado — la UI debe
                // pedir confirmación explícita y llamar a EnablePricingRuleCommand. Se expone
                // el valor de la excepción deshabilitada para que la UI pueda compararlo
                // contra el precio base actual del ítem antes de confirmar (evita reactivar
                // silenciosamente un precio fijo obsoleto).
                return Result<PricingRuleSetResultDto>.Success(
                    new PricingRuleSetResultDto(
                        PricingRuleSetStatus.ExistsInactive,
                        existing.Id,
                        existing.RuleType.ToString(),
                        existing.RuleValue
                    )
                );
            }

            // Ya activa: se preserva la edición de valor (comportamiento existente, sin cambios).
            // La auditoría (old/new tipados) la registra PricingRuleAuditHandler al reaccionar
            // al PricingRuleUpdatedEvent que UpdateRule() levanta — no se escribe aquí.
            existing.UpdateRule(cmd.RuleType, cmd.RuleValue, _u.UserId);
            await _repo.SaveChangesAsync(ct);
            return Result<PricingRuleSetResultDto>.Success(
                new PricingRuleSetResultDto(PricingRuleSetStatus.AlreadyActive, existing.Id)
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PricingRuleSetResultDto>.ValidationFailure(ex.Message);
        }
    }
}

public sealed class EnablePricingRuleHandler
    : IRequestHandler<EnablePricingRuleCommand, Result<PricingRuleDto>>
{
    private readonly IPricingRuleRepository _repo;
    private readonly IPriceListItemRepository _assignmentRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public EnablePricingRuleHandler(
        IPricingRuleRepository repo,
        IPriceListItemRepository assignmentRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _assignmentRepo = assignmentRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PricingRuleDto>> Handle(
        EnablePricingRuleCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var rule = await _repo.FindByKeyAsync(tid, cmd.PriceListId, cmd.ItemId, ct);
        if (rule is null)
            return Result<PricingRuleDto>.NotFound(
                "No existe una excepción deshabilitada para reactivar con esa clave."
            );

        // Invariante: no se puede reactivar una excepción cuya PriceListItem ya no está activa
        // — evita que una regla vuelva a producir efecto sin una asignación vigente que la respalde.
        var assignment = await _assignmentRepo.FindByKeyAsync(tid, cmd.PriceListId, cmd.ItemId, ct);
        if (assignment is not { IsActive: true })
            return Result<PricingRuleDto>.ValidationFailure(
                "El ítem no está asignado a esta lista de precios (o la asignación está deshabilitada); no se puede reactivar la excepción."
            );

        // La auditoría la registra PricingRuleAuditHandler al reaccionar a PricingRuleEnabledEvent.
        try
        {
            rule.Enable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PricingRuleDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PricingRuleDto>.Success(Map.ToDto(rule));
    }
}

public sealed class RemovePricingRuleHandler
    : IRequestHandler<RemovePricingRuleCommand, Result<bool>>
{
    private readonly IPricingRuleRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public RemovePricingRuleHandler(IPricingRuleRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(RemovePricingRuleCommand cmd, CancellationToken ct)
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (rule is null)
            return Result<bool>.NotFound("Regla no encontrada.");

        // La auditoría la registra PricingRuleAuditHandler al reaccionar a PricingRuleDisabledEvent.
        try
        {
            rule.Disable(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetPricingRulesHandler
    : IRequestHandler<GetPricingRulesQuery, Result<IReadOnlyList<PricingRuleDto>>>
{
    private readonly IPricingRuleRepository _repo;
    private readonly IAuditReader<PricingRuleAudit> _auditReader;
    private readonly ICurrentTenant _t;

    public GetPricingRulesHandler(
        IPricingRuleRepository repo,
        IAuditReader<PricingRuleAudit> auditReader,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _auditReader = auditReader;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<PricingRuleDto>>> Handle(
        GetPricingRulesQuery q,
        CancellationToken ct
    )
    {
        if (q.PriceListId is null && q.ItemId is null)
            return Result<IReadOnlyList<PricingRuleDto>>.ValidationFailure(
                "Debe indicar priceListId o itemId."
            );

        IReadOnlyList<PricingRule> items;
        if (q.PriceListId.HasValue)
            items = await _repo.GetByPriceListAsync(_t.TenantId, q.PriceListId.Value, ct);
        else
            items = await _repo.GetByItemAsync(_t.TenantId, q.ItemId!.Value, ct);

        // Batch: una sola consulta para "última auditoría" de todas las reglas, en vez de
        // un query por fila (ver IAuditReader.GetLastByEntityIdsAsync).
        var ruleIds = items.Select(r => r.Id).ToArray();
        var lastAuditByRule = await _auditReader.GetLastByEntityIdsAsync(_t.TenantId, ruleIds, ct);

        var withAudit = new List<PricingRuleDto>(items.Count);
        foreach (var rule in items)
        {
            var dto = Map.ToDto(rule);
            lastAuditByRule.TryGetValue(rule.Id, out var lastAudit);
            withAudit.Add(
                dto with
                {
                    LastModifiedAt = lastAudit?.OccurredAtUtc ?? rule.UpdatedAt ?? rule.CreatedAt,
                    LastModifiedByName = lastAudit?.UserName,
                }
            );
        }

        return Result<IReadOnlyList<PricingRuleDto>>.Success(withAudit);
    }
}

public sealed class GetPricingRuleByIdHandler
    : IRequestHandler<GetPricingRuleByIdQuery, Result<PricingRuleDto>>
{
    private readonly IPricingRuleRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPricingRuleByIdHandler(IPricingRuleRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PricingRuleDto>> Handle(
        GetPricingRuleByIdQuery q,
        CancellationToken ct
    )
    {
        var rule = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return rule is null
            ? Result<PricingRuleDto>.NotFound("No encontrada.")
            : Result<PricingRuleDto>.Success(Map.ToDto(rule));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static PricingRuleDto ToDto(PricingRule p) =>
        new(
            p.Id,
            p.PriceListId,
            p.ItemId,
            p.RuleType.ToString(),
            p.RuleValue,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt
        );
}
