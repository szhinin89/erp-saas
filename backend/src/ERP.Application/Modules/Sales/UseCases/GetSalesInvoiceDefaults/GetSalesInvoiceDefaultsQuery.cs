using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto — se precarga siempre en el flujo de
/// creación de factura, que ya opera con sucursal activa. No cambia la resolución de
/// DefaultWarehouseId (sigue null; ver comentario del handler) — eso es una mejora de lógica
/// de negocio fuera de alcance de este cierre.
/// </summary>
public record GetSalesInvoiceDefaultsQuery
    : IRequest<Result<SalesInvoiceDefaultsDto>>,
        IBranchScopedRequest;
