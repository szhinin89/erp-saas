using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

public sealed record ApplySalesDiscountCommand(Guid InvoiceId, decimal DiscountPct)
    : IRequest<Result<SalesInvoiceDto>>,
        IBranchScopedRequest;

public sealed class ApplySalesDiscountHandler
    : IRequestHandler<ApplySalesDiscountCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;
    private readonly IOperationalPreferencesResolver _preferences;

    public ApplySalesDiscountHandler(
        ISalesInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
        _preferences = preferences;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        ApplySalesDiscountCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // CONFIG-DYNAMIC-OPERATIONS-01 (sales.pos.allow_manual_discount / max_discount_percent):
        // este endpoint es la acción explícita de "aplicar descuento manual" del cajero — punto de
        // enforcement principal, distinto de descuentos ya resueltos por pricing automático.
        var preferences = await _preferences.ResolveAsync(ct);
        if (!preferences.SalesPos.AllowManualDiscount && cmd.DiscountPct != 0m)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(
                "Esta empresa no permite aplicar descuentos manuales."
            );
        }

        if (
            preferences.SalesPos.MaxDiscountPercent > 0m
            && cmd.DiscountPct > preferences.SalesPos.MaxDiscountPercent
        )
        {
            return Result<SalesInvoiceDto>.ValidationFailure(
                $"El descuento máximo permitido es {preferences.SalesPos.MaxDiscountPercent}%."
            );
        }

        try
        {
            inv.ApplyGlobalDiscount(cmd.DiscountPct, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}
