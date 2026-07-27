using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.FindItemMatches;

/// <summary>
/// Lista las líneas persistidas de un documento de recepción con su estado de conciliación y, para
/// las que aún no tienen Item resuelto, las sugerencias del motor de Item Matching.
/// </summary>
public sealed record FindItemMatchesQuery(Guid PurchaseReceptionDocumentId)
    : IRequest<Result<IReadOnlyList<PurchaseReceptionLineMatchDto>>>, IBranchScopedRequest;
