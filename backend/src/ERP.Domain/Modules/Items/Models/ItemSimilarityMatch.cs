namespace ERP.Domain.Modules.Items.Models;

/// <summary>Candidato de Item Matching (Purchase Reception) resuelto por similitud de texto (pg_trgm).</summary>
public sealed record ItemSimilarityMatch(
    Guid ItemId,
    string Sku,
    string ShortName,
    string Description,
    double Score
);
