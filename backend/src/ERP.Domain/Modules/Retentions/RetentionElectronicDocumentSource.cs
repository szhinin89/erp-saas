namespace ERP.Domain.Modules.Retentions;

/// <summary>
/// Referencia débil (ADR-023) de una retención hacia su comprobante electrónico:
/// <c>ElectronicDocument.SourceModule = "Retentions"</c> y <c>SourceEntityId = RetentionDocument.Id</c>.
/// Fuente única del literal — registro, gate de ciclo de vida, anulación y recuperación lo usan.
/// </summary>
public static class RetentionElectronicDocumentSource
{
    public const string SourceModule = "Retentions";
}
