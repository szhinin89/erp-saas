using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Control de secuencial por punto de emisión y tipo de comprobante.
/// IMPORTANTE: Siempre hacer SELECT … FOR UPDATE antes de incrementar CurrentSeq
/// para evitar duplicados en entornos concurrentes.
/// </summary>
public class DocumentSequence
{
    public Guid    Id              { get; set; }
    public Guid    CompanyId       { get; set; }
    public Guid    EmissionPointId { get; set; }
    public string  DocTypeCode     { get; set; } = null!;
    public int     CurrentSeq      { get; set; } = 0;
    public DateTime UpdatedAt      { get; set; }

    public Company       Company       { get; set; } = null!;
    public EmissionPoint EmissionPoint { get; set; } = null!;
    public SriDocType    DocType       { get; set; } = null!;
}
