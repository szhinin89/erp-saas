using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using System.Text.RegularExpressions;

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
        Guid tenantId,
        Guid? supplierId,
        string? supplierCode,
        string? supplierAuxCode,
        string description,
        int maxResults,
        CancellationToken cancellationToken = default
    );
}

/// Política de Item Matching
///
/// AutoMatch:
///   - SupplierCodeExactMatch
///   - SupplierAuxCodeExactMatch
///
/// Requieren confirmación del usuario:
///   - DescriptionNormalizedMatch
///   - DescriptionSimilarity
///
/// Objetivo:
/// Nunca vincular automáticamente un Item únicamente por similitud de texto.
/// La similitud solo sirve para proponer candidatos.
public sealed class ItemMatchFinder : IItemMatchFinder
{
    public const double MinSimilarityScore = 0.80; // valor por defecto de pg_trgm (similarity())
    public const decimal MinSuggestionScore = 75m;

    public const string ReasonSupplierCodeExact = "SupplierCodeExactMatch";
    public const string ReasonSupplierAuxCodeExact = "SupplierAuxCodeExactMatch";
    public const string ReasonDescriptionNormalized = "DescriptionNormalizedMatch";
    public const string ReasonDescriptionSimilarity = "DescriptionSimilarity";

    private static readonly Regex NonAlphanumeric = new(@"[^a-z0-9\s]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpaces = new(@"\s+", RegexOptions.Compiled);

    private readonly IItemRepository _itemRepo;

    public ItemMatchFinder(IItemRepository itemRepo) => _itemRepo = itemRepo;

    public async Task<IReadOnlyList<ItemMatchCandidateDto>> FindCandidatesAsync(
        Guid tenantId,
        Guid? supplierId,
        string? supplierCode,
        string? supplierAuxCode,
        string description,
        int maxResults,
        CancellationToken cancellationToken = default
    )
    {
        var candidates = new Dictionary<Guid, ItemMatchCandidateDto>();
        // 1. Código de proveedor exacto.
        if (supplierId is { } sId1 && !string.IsNullOrWhiteSpace(supplierCode))
        {
            var itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(
                sId1,
                supplierCode,
                tenantId,
                cancellationToken
            );

            if (itemId is { } id1)
            {
                await AddExactAsync(
                    candidates,
                    id1,
                    100m,
                    ReasonSupplierCodeExact,
                    tenantId,
                    cancellationToken
                );
            }
        }
        // 2. Código auxiliar exacto.
        if (supplierId is { } sId2 && !string.IsNullOrWhiteSpace(supplierAuxCode))
        {
            var itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(
                sId2,
                supplierAuxCode,
                tenantId,
                cancellationToken
            );

            if (itemId is { } id2 && !candidates.ContainsKey(id2))
            {
                await AddExactAsync(
                    candidates,
                    id2,
                    95m,
                    ReasonSupplierAuxCodeExact,
                    tenantId,
                    cancellationToken
                );
            }
        }
        /*
           REGLA IMPORTANTE:
           En compras el código del proveedor es la identidad del producto.
           Si existe código proveedor en el XML:
              - encontrado -> AutoMatch
              - no encontrado -> revisión manual
           Nunca usar descripción para reemplazar un código desconocido.
        */
        var hasSupplierCode =
            !string.IsNullOrWhiteSpace(supplierCode) || !string.IsNullOrWhiteSpace(supplierAuxCode);

        if (hasSupplierCode)
        {
            return candidates.Values.OrderByDescending(c => c.MatchScore).Take(maxResults).ToList();
        }

        // 3. Solo sin código proveedor:
        // búsqueda por descripción como ayuda manual.
        var normalizedTarget = Normalize(description);

        var similar = await _itemRepo.SearchBySimilarityAsync(
            description,
            tenantId,
            maxResults,
            MinSimilarityScore,
            cancellationToken
        );
        foreach (var match in similar)
        {
            if (candidates.ContainsKey(match.ItemId))
                continue;

            var isNormalizedEqual =
                Normalize(match.ShortName) == normalizedTarget
                || Normalize(match.Description) == normalizedTarget;

            var score = Math.Round((decimal)match.Score * 100, 2);

            // Los guards de calidad (solapamiento de palabras, piso de score) solo aplican a la
            // rama de similitud pura: una igualdad de descripción normalizada es una señal más
            // fuerte que el score crudo de pg_trgm y siempre resuelve a un score fijo (95m,
            // ver abajo), que por construcción ya supera MinSuggestionScore — evaluar el score
            // crudo antes de esa resolución filtraba candidatos normalizados-iguales legítimos
            // cuyo score pg_trgm crudo caía por debajo del piso pensado únicamente para
            // sugerencias por similitud pura.
            if (!isNormalizedEqual)
            {
                if (!HasEnoughWordsMatch(description, match.ShortName))
                    continue;

                if (score < MinSuggestionScore)
                    continue;
            }

            candidates[match.ItemId] = isNormalizedEqual
                ? new ItemMatchCandidateDto(
                    match.ItemId,
                    match.Sku,
                    match.ShortName,
                    match.Description,
                    95m,
                    ReasonDescriptionNormalized
                )
                : new ItemMatchCandidateDto(
                    match.ItemId,
                    match.Sku,
                    match.ShortName,
                    match.Description,
                    score,
                    ReasonDescriptionSimilarity
                );
        }

        return candidates.Values.OrderByDescending(c => c.MatchScore).Take(maxResults).ToList();
    }

    private async Task AddExactAsync(
        Dictionary<Guid, ItemMatchCandidateDto> candidates,
        Guid itemId,
        decimal score,
        string reason,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
        var item = await _itemRepo.GetByIdLightAsync(itemId, tenantId, cancellationToken);
        if (item is null)
            return;

        candidates[itemId] = new ItemMatchCandidateDto(
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            item.Code.Description,
            score,
            reason
        );
    }

    private static string Normalize(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        var noSpecial = NonAlphanumeric.Replace(lower, " ");
        return MultipleSpaces.Replace(noSpecial, " ").Trim();
    }

    private static bool HasEnoughWordsMatch(string source, string target)
    {
        var sourceWords = Normalize(source)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var targetWords = Normalize(target)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var common = sourceWords.Intersect(targetWords).Count();

        return common >= 2;
    }
}
