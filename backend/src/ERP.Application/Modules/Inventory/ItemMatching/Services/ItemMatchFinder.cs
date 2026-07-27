using System.Text.RegularExpressions;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Modules.Inventory.ItemMatching.Services;

public interface IItemMatchFinder
{
    /// <summary>
    /// Motor de Item Matching (Purchase Reception): resuelve candidatos del catálogo para una
    /// línea, en orden de confianza — código de proveedor exacto, código auxiliar exacto,
    /// descripción normalizada, similitud de texto (pg_trgm). Nunca infiere impuestos ni crea
    /// Items — solo propone/resuelve <c>ItemId</c>.
    /// </summary>
    Task<IReadOnlyList<ItemMatchCandidateDto>> FindCandidatesAsync(
        Guid tenantId, Guid? supplierId, string? supplierCode, string? supplierAuxCode,
        string description, int maxResults, CancellationToken cancellationToken = default);
}

public sealed class ItemMatchFinder : IItemMatchFinder
{
    public const double MinSimilarityScore = 0.35; // valor por defecto de pg_trgm (similarity())

    public const string ReasonSupplierCodeExact = "SupplierCodeExactMatch";
    public const string ReasonSupplierAuxCodeExact = "SupplierAuxCodeExactMatch";
    public const string ReasonDescriptionNormalized = "DescriptionNormalizedMatch";
    public const string ReasonDescriptionSimilarity = "DescriptionSimilarity";

    private static readonly Regex NonAlphanumeric = new(@"[^a-z0-9\s]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpaces = new(@"\s+", RegexOptions.Compiled);

    private readonly IItemRepository _itemRepo;

    public ItemMatchFinder(IItemRepository itemRepo) => _itemRepo = itemRepo;

    public async Task<IReadOnlyList<ItemMatchCandidateDto>> FindCandidatesAsync(
        Guid tenantId, Guid? supplierId, string? supplierCode, string? supplierAuxCode,
        string description, int maxResults, CancellationToken cancellationToken = default)
    {
        var candidates = new Dictionary<Guid, ItemMatchCandidateDto>();

        // 1. Código de proveedor exacto.
        if (supplierId is { } sId1 && !string.IsNullOrWhiteSpace(supplierCode))
        {
            var itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(sId1, supplierCode, tenantId, cancellationToken);
            if (itemId is { } id1)
                await AddExactAsync(candidates, id1, 100m, ReasonSupplierCodeExact, tenantId, cancellationToken);
        }

        // 2. Código auxiliar exacto.
        if (supplierId is { } sId2 && !string.IsNullOrWhiteSpace(supplierAuxCode))
        {
            var itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(sId2, supplierAuxCode, tenantId, cancellationToken);
            if (itemId is { } id2 && !candidates.ContainsKey(id2))
                await AddExactAsync(candidates, id2, 95m, ReasonSupplierAuxCodeExact, tenantId, cancellationToken);
        }

        // 3-4. Descripción normalizada + similitud (pg_trgm) — un único query de similitud acota
        // el catálogo; la igualdad exacta normalizada sobre ese resultado sube el score sin
        // necesidad de un segundo recorrido completo del catálogo.
        var normalizedTarget = Normalize(description);
        var similar = await _itemRepo.SearchBySimilarityAsync(description, tenantId, maxResults, MinSimilarityScore, cancellationToken);

        foreach (var match in similar)
        {
            if (candidates.ContainsKey(match.ItemId))
                continue;

            var isNormalizedEqual = Normalize(match.ShortName) == normalizedTarget || Normalize(match.Description) == normalizedTarget;
            candidates[match.ItemId] = isNormalizedEqual
                ? new ItemMatchCandidateDto(match.ItemId, match.Sku, match.ShortName, match.Description, 95m, ReasonDescriptionNormalized)
                : new ItemMatchCandidateDto(match.ItemId, match.Sku, match.ShortName, match.Description, Math.Round((decimal)match.Score * 100, 2), ReasonDescriptionSimilarity);
        }

        return candidates.Values
            .OrderByDescending(c => c.MatchScore)
            .Take(maxResults)
            .ToList();
    }

    private async Task AddExactAsync(
        Dictionary<Guid, ItemMatchCandidateDto> candidates, Guid itemId, decimal score, string reason,
        Guid tenantId, CancellationToken cancellationToken)
    {
        var item = await _itemRepo.GetByIdLightAsync(itemId, tenantId, cancellationToken);
        if (item is null)
            return;

        candidates[itemId] = new ItemMatchCandidateDto(item.Id, item.Code.SKU, item.Code.ShortName, item.Code.Description, score, reason);
    }

    private static string Normalize(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        var noSpecial = NonAlphanumeric.Replace(lower, " ");
        return MultipleSpaces.Replace(noSpecial, " ").Trim();
    }
}
