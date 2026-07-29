namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Resumen liviano de un documento de origen, solo para mostrar en el Monitor — nunca datos comerciales completos.</summary>
public sealed record SourceDocumentSummary(string? DocumentNumber, string? CounterpartyName);

/// <summary>
/// Resuelve el número de documento y la contraparte (cliente/proveedor) de un documento de origen,
/// para que el Monitor de Documentos Electrónicos nunca dependa de un módulo de negocio específico.
/// Cada módulo dueño de un <c>SourceModule</c> (p.ej. "Sales") registra su propia implementación —
/// el Monitor solo conoce esta interfaz, nunca un repositorio de Ventas/Compras/etc. directamente.
/// </summary>
public interface ISourceDocumentSummaryProvider
{
    /// <summary>Valor exacto de <c>ElectronicDocument.SourceModule</c> que esta implementación resuelve (p.ej. "Sales").</summary>
    string SourceModule { get; }

    Task<IReadOnlyDictionary<Guid, SourceDocumentSummary>> GetSummariesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken ct = default
    );
}
