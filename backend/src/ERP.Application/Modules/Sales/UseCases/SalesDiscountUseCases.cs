using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

public sealed record ApplySalesDiscountCommand(Guid InvoiceId, decimal DiscountPct)
    : IRequest<Result<SalesInvoiceDto>>, IBranchScopedRequest;

public sealed class ApplySalesDiscountHandler
    : IRequestHandler<ApplySalesDiscountCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ApplySalesDiscountHandler(ISalesInvoiceRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<SalesInvoiceDto>> Handle(ApplySalesDiscountCommand cmd, CancellationToken ct)
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null) return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        try { inv.ApplyGlobalDiscount(cmd.DiscountPct, _u.UserId); }
        catch (InvalidOperationException ex)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}
