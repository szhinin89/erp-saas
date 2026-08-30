namespace ERP.Domain.Modules.DocTypes.Entities;

/// <summary>
/// Mapeo opcional de un <see cref="DocType"/> interno hacia el catálogo oficial
/// <see cref="ERP.Domain.Modules.SriCatalogs.Entities.SriDocType"/>. Varios <see cref="DocType"/>
/// pueden mapear al mismo código SRI (p. ej. NCVDEV y NCCDEV ambos a "04"). No todo
/// <see cref="DocType"/> tiene mapeo — documentos puramente internos (ASI, AJUINV) no aplican.
/// </summary>
public class DocTypeSriMap
{
    public string DocTypeCode { get; set; } = null!;
    public string SriDocTypeCode { get; set; } = null!;
}
