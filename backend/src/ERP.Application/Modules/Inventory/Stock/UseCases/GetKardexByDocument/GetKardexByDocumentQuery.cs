using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByDocument;

/// <summary>
/// Movimientos de Kardex generados por un documento origen específico. Exacta por diseño —
/// el número de documento se resuelve siempre en su dominio propietario (Purchases/Sales/...)
/// antes de llegar aquí; este query nunca matchea por texto libre (StockMovement.Reference
/// no es la fuente de verdad para localizar documentos).
/// </summary>
public sealed record GetKardexByDocumentQuery(Guid SourceDocId, string SourceDocType)
    : IRequest<Result<IReadOnlyList<StockMovementDto>>>,
        IBranchScopedRequest;
