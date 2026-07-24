using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;

namespace ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;

/// <summary>
/// SupplierId es opcional: si se indica, resuelve el código de proveedor específico
/// (<c>ItemSupplierCode</c>), única fuente de códigos de compra. Sin proveedor, o sin
/// código registrado para ese proveedor, <c>SupplierCode</c> queda en null.
/// </summary>
public sealed record GetPurchaseItemContextQuery(Guid ItemId, Guid WarehouseId, Guid? SupplierId = null)
    : IRequest<Result<PurchaseItemContextDto>>, IBranchScopedRequest;
