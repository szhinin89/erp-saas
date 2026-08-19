using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto — se precarga siempre en el flujo de
/// creación de factura, que ya opera con sucursal activa. CONFIG-FOUNDATION-P0-01: el contexto
/// de sucursal (ICurrentBranch, validado por BranchScopeBehavior antes de llegar al handler) se
/// usa ahora para resolver DefaultWarehouseId server-side — ver
/// GetSalesInvoiceDefaultsQueryHandler.ResolveDefaultWarehouseAsync.
/// </summary>
public record GetSalesInvoiceDefaultsQuery
    : IRequest<Result<SalesInvoiceDefaultsDto>>,
        IBranchScopedRequest;
