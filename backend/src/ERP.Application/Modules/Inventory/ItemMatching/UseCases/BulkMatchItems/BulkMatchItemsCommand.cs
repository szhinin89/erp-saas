using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;

/// <summary>Vinculación masiva de líneas de recepción a Items existentes — Item Matching.</summary>
public sealed record BulkMatchItemsCommand(IReadOnlyList<BulkMatchItemEntry> Matches)
    : IRequest<Result<BulkMatchItemsResultDto>>,
        IBranchScopedRequest;
