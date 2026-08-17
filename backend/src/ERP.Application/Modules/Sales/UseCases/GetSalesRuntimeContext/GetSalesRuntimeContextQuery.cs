using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesRuntimeContext;

/// <summary>
/// Contexto agregado que Ventas (y futuras pantallas: POS, facturación rápida, pedidos, app
/// móvil) consultan una sola vez al cargar la pantalla — evita múltiples servicios sueltos y
/// hardcodes de política fiscal en frontend. Branch-scoped: mismo requisito que
/// GetSalesInvoiceDefaultsQuery, ya que Ventas siempre opera con sucursal activa.
/// </summary>
public sealed record GetSalesRuntimeContextQuery
    : IRequest<Result<SalesRuntimeContextDto>>,
        IBranchScopedRequest;
