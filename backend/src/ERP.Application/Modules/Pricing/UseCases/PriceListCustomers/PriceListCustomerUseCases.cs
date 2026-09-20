using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Pricing.UseCases.PriceListCustomers;

// ── Queries & Commands ──────────────────────────────────────────────────

/// <summary>Clientes actualmente asignados (activos) a una PriceList — identidad + nombre para mostrar.</summary>
public sealed record GetPriceListCustomersQuery(Guid PriceListId)
    : IRequest<Result<IReadOnlyList<PriceListCustomerDto>>>,
        ICompanyScopedRequest;

/// <summary>
/// Asigna un cliente a una PriceList. Regla de negocio: como máximo UNA PriceList ACTIVA por
/// (Tenant, Company, Customer) — nunca se rompe con un error técnico de constraint; el conflicto
/// se detecta antes de escribir y se expone como <see cref="PriceListCustomerAssignStatus.Conflict"/>.
/// <see cref="ConfirmSwitch"/> en true reenvía la misma intención ya confirmada por el usuario:
/// desactiva la relación anterior y activa/crea la nueva en una sola transacción.
/// </summary>
public sealed record AssignCustomerToPriceListCommand(
    Guid PriceListId,
    Guid CustomerId,
    bool ConfirmSwitch = false
) : IRequest<Result<PriceListCustomerAssignResultDto>>, ICompanyScopedRequest;

/// <summary>Quita (desactiva) la asignación de un cliente a una PriceList — nunca borrado físico.</summary>
public sealed record DisablePriceListCustomerCommand(Guid PriceListId, Guid CustomerId)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class AssignCustomerToPriceListCommandValidator
    : AbstractValidator<AssignCustomerToPriceListCommand>
{
    public AssignCustomerToPriceListCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public sealed class DisablePriceListCustomerCommandValidator
    : AbstractValidator<DisablePriceListCustomerCommand>
{
    public DisablePriceListCustomerCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPriceListCustomersHandler
    : IRequestHandler<GetPriceListCustomersQuery, Result<IReadOnlyList<PriceListCustomerDto>>>
{
    private readonly IPriceListCustomerRepository _assignments;
    private readonly IPriceListRepository _priceLists;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly ICurrentTenant _t;

    public GetPriceListCustomersHandler(
        IPriceListCustomerRepository assignments,
        IPriceListRepository priceLists,
        IBusinessPartnerRepository businessPartners,
        ICurrentTenant t
    )
    {
        _assignments = assignments;
        _priceLists = priceLists;
        _businessPartners = businessPartners;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<PriceListCustomerDto>>> Handle(
        GetPriceListCustomersQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var priceList = await _priceLists.GetByIdAsync(tenantId, q.PriceListId, ct);
        if (priceList is null)
            return Result<IReadOnlyList<PriceListCustomerDto>>.NotFound("Lista de precios no encontrada.");

        // 2 queries totales, sin importar cuántos clientes tenga la lista (nunca N+1) — mismo
        // patrón que GetItemsAssignedToPriceListHandler.
        var assignments = await _assignments.GetByPriceListAsync(tenantId, q.PriceListId, ct);
        var displayInfo = await _businessPartners.GetDisplayInfoByIdsAsync(
            assignments.Select(a => a.CustomerId),
            ct
        );

        var rows = assignments
            .Select(a =>
            {
                displayInfo.TryGetValue(a.CustomerId, out var info);
                return new PriceListCustomerDto(
                    a.CustomerId,
                    info?.DisplayName ?? a.CustomerId.ToString(),
                    info?.IdentificationNumber
                );
            })
            .OrderBy(r => r.CustomerName)
            .ToList();

        return Result<IReadOnlyList<PriceListCustomerDto>>.Success(rows);
    }
}

public sealed class AssignCustomerToPriceListHandler
    : IRequestHandler<AssignCustomerToPriceListCommand, Result<PriceListCustomerAssignResultDto>>
{
    private readonly IPriceListCustomerRepository _assignments;
    private readonly IPriceListRepository _priceLists;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public AssignCustomerToPriceListHandler(
        IPriceListCustomerRepository assignments,
        IPriceListRepository priceLists,
        IBusinessPartnerRepository businessPartners,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _assignments = assignments;
        _priceLists = priceLists;
        _businessPartners = businessPartners;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PriceListCustomerAssignResultDto>> Handle(
        AssignCustomerToPriceListCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;

        var targetList = await _priceLists.GetByIdAsync(tenantId, cmd.PriceListId, ct);
        if (targetList is null)
            return Result<PriceListCustomerAssignResultDto>.NotFound("Lista de precios no encontrada.");

        // CustomerId existente y perteneciente al Tenant — IBusinessPartnerRepository filtra
        // por tenant vía global query filter (fail-closed), nunca se pasa el tenantId manual.
        var customer = await _businessPartners.GetByIdAsync(cmd.CustomerId, ct);
        if (customer is null)
            return Result<PriceListCustomerAssignResultDto>.NotFound("Cliente no encontrado.");

        // Como máximo una relación ACTIVA por (Tenant, Company, Customer) — se busca ANTES de
        // escribir para nunca fallar con el constraint de BD como un error técnico.
        var existingForCustomer = await _assignments.GetByCustomerAsync(tenantId, cmd.CustomerId, ct);
        var currentActive = existingForCustomer.FirstOrDefault(a => a.IsActive);

        // Reactivación/creación puntual para ESTA lista concreta (independiente de si el cliente
        // tiene otra activa) — se necesita para saber si hay que reactivar una fila deshabilitada
        // en vez de duplicarla.
        var existingForThisList = existingForCustomer.FirstOrDefault(a => a.PriceListId == cmd.PriceListId);

        if (currentActive is not null && currentActive.PriceListId == cmd.PriceListId)
            // Ya asignado exactamente a esta lista — idempotente, sin cambios.
            return Result<PriceListCustomerAssignResultDto>.Success(
                new PriceListCustomerAssignResultDto(PriceListCustomerAssignStatus.AlreadyActive)
            );

        if (currentActive is not null && !cmd.ConfirmSwitch)
        {
            // Tiene otra lista activa y el usuario todavía no confirmó el cambio — no se escribe
            // nada, se expone el conflicto para que la UI pida confirmación explícita.
            var conflictingList = await _priceLists.GetByIdAsync(tenantId, currentActive.PriceListId, ct);
            return Result<PriceListCustomerAssignResultDto>.Success(
                new PriceListCustomerAssignResultDto(
                    PriceListCustomerAssignStatus.Conflict,
                    currentActive.PriceListId,
                    conflictingList?.Name
                )
            );
        }

        // Desde aquí: o no tenía ninguna lista activa, o el usuario ya confirmó el cambio —
        // desactivar la anterior (si existe) y activar/crear la nueva, todo en UN SOLO
        // SaveChangesAsync (transacción atómica: si algo falla, nada queda persistido).
        if (currentActive is not null && currentActive.PriceListId != cmd.PriceListId)
            currentActive.Disable(_u.UserId);

        if (existingForThisList is not null)
        {
            if (!existingForThisList.IsActive)
                existingForThisList.Enable(_u.UserId);
        }
        else
        {
            var created = PriceListCustomer.Create(
                tenantId,
                _c.CompanyId,
                cmd.PriceListId,
                cmd.CustomerId,
                _u.UserId
            );
            await _assignments.AddAsync(created, ct);
        }

        await _assignments.SaveChangesAsync(ct);

        return Result<PriceListCustomerAssignResultDto>.Success(
            new PriceListCustomerAssignResultDto(
                currentActive is not null
                    ? PriceListCustomerAssignStatus.Switched
                    : PriceListCustomerAssignStatus.Assigned
            )
        );
    }
}

public sealed class DisablePriceListCustomerHandler
    : IRequestHandler<DisablePriceListCustomerCommand, Result<bool>>
{
    private readonly IPriceListCustomerRepository _assignments;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public DisablePriceListCustomerHandler(
        IPriceListCustomerRepository assignments,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _assignments = assignments;
        _t = t;
        _u = u;
    }

    public async Task<Result<bool>> Handle(DisablePriceListCustomerCommand cmd, CancellationToken ct)
    {
        var existing = await _assignments.FindByKeyAsync(_t.TenantId, cmd.PriceListId, cmd.CustomerId, ct);
        if (existing is not { IsActive: true })
            return Result<bool>.NotFound("El cliente no está asignado (activo) a esta lista de precios.");

        existing.Disable(_u.UserId);
        await _assignments.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
