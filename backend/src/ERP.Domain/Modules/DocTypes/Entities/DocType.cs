namespace ERP.Domain.Modules.DocTypes.Entities;

/// <summary>
/// Catálogo interno global (SSOT) de tipos de documento/proceso del ERP — GASDOC, FACVEN, etc.
/// No confundir con <see cref="ERP.Domain.Modules.SriCatalogs.Entities.SriDocType"/>, que es el
/// catálogo oficial SRI. La relación opcional entre ambos vive en <see cref="DocTypeSriMap"/>.
/// Deliberadamente simple: sin flags de impacto contable/inventario/AP-AR — eso vive en la lógica
/// de cada módulo, no en el catálogo.
/// </summary>
public class DocType
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
