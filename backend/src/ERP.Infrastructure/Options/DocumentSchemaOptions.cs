namespace ERP.Infrastructure.Options;

public sealed class DocumentSchemaOptions
{
    public const string SectionName = "Documents";

    /// <summary>
    /// Si true, lectura/escritura de facturas de venta usa sales_document (esquema unificado).
    /// Requiere haber ejecutado scripts/sql/002_unified_documents_schema_and_migration.sql.
    /// </summary>
    public bool UseUnifiedSchema { get; set; }
}
