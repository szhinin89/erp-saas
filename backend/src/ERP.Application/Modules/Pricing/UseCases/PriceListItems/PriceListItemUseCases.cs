using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Pricing.UseCases.PriceListItems;

// ── Commands & Queries ──────────────────────────────────────────────────

/// <summary>
/// Reemplaza el conjunto completo de listas de precios a las que pertenece un ítem.
/// Puramente administrativo — no crea ni modifica ninguna PricingRule ni precio.
/// </summary>
public sealed record SetItemPriceListsCommand(Guid ItemId, IReadOnlyList<Guid> PriceListIds)
    : IRequest<Result<IReadOnlyList<Guid>>>,
        ICompanyScopedRequest;

public sealed record GetItemPriceListsQuery(Guid ItemId)
    : IRequest<Result<IReadOnlyList<Guid>>>,
        ICompanyScopedRequest;

/// <summary>
/// Responsabilidad única: qué ítems pertenecen a una PriceList (identidad + precio base).
/// Dirección inversa de <see cref="GetItemPriceListsQuery"/>. No trae reglas ni excepciones
/// — para eso, el consumidor combina esta respuesta con <c>GetPricingRulesQuery</c> existente
/// (composición en Application/frontend, nunca un DTO que mezcle ambos agregados).
/// </summary>
public sealed record GetItemsAssignedToPriceListQuery(Guid PriceListId)
    : IRequest<Result<IReadOnlyList<PriceListAssignedItemDto>>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class SetItemPriceListsCommandValidator : AbstractValidator<SetItemPriceListsCommand>
{
    public SetItemPriceListsCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.PriceListIds).NotNull();
        RuleForEach(x => x.PriceListIds).NotEmpty();
        RuleFor(x => x.PriceListIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("No se puede asignar la misma lista de precios más de una vez.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class SetItemPriceListsHandler
    : IRequestHandler<SetItemPriceListsCommand, Result<IReadOnlyList<Guid>>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IPriceListRepository _priceListRepo;
    private readonly IPriceListItemRepository _assignmentRepo;
    private readonly IPricingRuleRepository _ruleRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public SetItemPriceListsHandler(
        IItemRepository itemRepo,
        IPriceListRepository priceListRepo,
        IPriceListItemRepository assignmentRepo,
        IPricingRuleRepository ruleRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _itemRepo = itemRepo;
        _priceListRepo = priceListRepo;
        _assignmentRepo = assignmentRepo;
        _ruleRepo = ruleRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        SetItemPriceListsCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var item = await _itemRepo.GetByIdLightAsync(cmd.ItemId, tid, ct);
        if (item is null)
            return Result<IReadOnlyList<Guid>>.NotFound("Ítem no encontrado.");

        var requestedIds = cmd.PriceListIds.Distinct().ToList();
        if (requestedIds.Count > 0)
        {
            var activeLists = await _priceListRepo.GetAllAsync(tid, true, null, ct);
            var activeIds = activeLists.Select(pl => pl.Id).ToHashSet();
            var invalid = requestedIds.Where(id => !activeIds.Contains(id)).ToList();
            if (invalid.Count > 0)
                return Result<IReadOnlyList<Guid>>.ValidationFailure(
                    "Una o más listas de precios seleccionadas no existen o no están activas."
                );
        }

        var existing = await _assignmentRepo.GetByItemAsync(tid, cmd.ItemId, ct);
        var existingByList = existing.ToDictionary(a => a.PriceListId);

        foreach (
            var assignment in existing.Where(a =>
                a.IsActive && !requestedIds.Contains(a.PriceListId)
            )
        )
        {
            assignment.Disable(_u.UserId);

            // El ítem deja de pertenecer administrativamente a esta lista — cualquier
            // PricingRule (excepción) que tuviera para (PriceListId, ItemId) queda huérfana
            // si no se deshabilita también: PricingResolver no consulta PriceListItem, así
            // que la excepción seguiría aplicando si esa combinación se resolviera de forma
            // directa. Se deshabilita para que el comportamiento real coincida con la
            // intención administrativa de "ya no pertenece a esta lista".
            var orphanedRule = await _ruleRepo.FindByKeyAsync(
                tid,
                assignment.PriceListId,
                cmd.ItemId,
                ct
            );
            if (orphanedRule is { IsActive: true })
                orphanedRule.Disable(_u.UserId);
        }

        foreach (var priceListId in requestedIds)
        {
            if (existingByList.TryGetValue(priceListId, out var assignment))
            {
                if (!assignment.IsActive)
                    assignment.Enable(_u.UserId);
            }
            else
            {
                var created = PriceListItem.Create(
                    tid,
                    _c.CompanyId,
                    priceListId,
                    cmd.ItemId,
                    _u.UserId
                );
                await _assignmentRepo.AddAsync(created, ct);
            }
        }

        await _assignmentRepo.SaveChangesAsync(ct);
        return Result<IReadOnlyList<Guid>>.Success(requestedIds);
    }
}

public sealed class GetItemPriceListsHandler
    : IRequestHandler<GetItemPriceListsQuery, Result<IReadOnlyList<Guid>>>
{
    private readonly IPriceListItemRepository _assignmentRepo;
    private readonly ICurrentTenant _t;

    public GetItemPriceListsHandler(IPriceListItemRepository assignmentRepo, ICurrentTenant t)
    {
        _assignmentRepo = assignmentRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        GetItemPriceListsQuery q,
        CancellationToken ct
    )
    {
        var assignments = await _assignmentRepo.GetByItemAsync(_t.TenantId, q.ItemId, ct);
        var activeListIds = assignments.Where(a => a.IsActive).Select(a => a.PriceListId).ToList();
        return Result<IReadOnlyList<Guid>>.Success(activeListIds);
    }
}

public sealed class GetItemsAssignedToPriceListHandler
    : IRequestHandler<
        GetItemsAssignedToPriceListQuery,
        Result<IReadOnlyList<PriceListAssignedItemDto>>
    >
{
    private readonly IPriceListItemRepository _assignmentRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ICurrentTenant _t;

    public GetItemsAssignedToPriceListHandler(
        IPriceListItemRepository assignmentRepo,
        IItemRepository itemRepo,
        ICurrentTenant t
    )
    {
        _assignmentRepo = assignmentRepo;
        _itemRepo = itemRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<PriceListAssignedItemDto>>> Handle(
        GetItemsAssignedToPriceListQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;

        // 2 queries totales, sin importar cuántos ítems tenga la lista (nunca N+1).
        var assignments = await _assignmentRepo.GetByPriceListAsync(tenantId, q.PriceListId, ct);
        var itemIds = assignments.Select(a => a.ItemId).ToList();

        var items = await _itemRepo.GetByIdsLightAsync(itemIds, tenantId, ct);

        var rows = items
            .Select(i => new PriceListAssignedItemDto(
                i.Id,
                i.Code.SKU,
                i.Code.ShortName,
                i.BaseSalePrice
            ))
            .OrderBy(r => r.Sku)
            .ToList();

        return Result<IReadOnlyList<PriceListAssignedItemDto>>.Success(rows);
    }
}
