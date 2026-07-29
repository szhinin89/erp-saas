using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.GetLineMatch;

/// <summary>
/// Estado de conciliación de una única línea de recepción, por su Id — reutilizado por la pantalla
/// de Compras (/purchases) para reconstruir el estado de Item Matching de una línea ya guardada
/// (PurchaseInvoiceDetail.PurchaseReceptionLineId) al reabrir una compra para editar, sin duplicar
/// el motor de sugerencias ni la lectura de PurchaseReceptionLine.
/// </summary>
public sealed record GetPurchaseReceptionLineMatchQuery(Guid PurchaseReceptionLineId)
    : IRequest<Result<PurchaseReceptionLineMatchDto>>,
        IBranchScopedRequest;
