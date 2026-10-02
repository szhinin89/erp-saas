namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Resultado de <see cref="IElectronicDocumentSourceLifecycleGuard.EvaluateAsync"/>.</summary>
/// <param name="AllowsProcessing">True solo si el documento de origen existe y todavía permite procesamiento electrónico.</param>
/// <param name="Reason">Motivo legible cuando <paramref name="AllowsProcessing"/> es false.</param>
public sealed record ElectronicDocumentSourceLifecycle(bool AllowsProcessing, string? Reason)
{
    public static ElectronicDocumentSourceLifecycle Allowed { get; } = new(true, null);

    public static ElectronicDocumentSourceLifecycle Denied(string reason) => new(false, reason);
}

/// <summary>
/// ADR-036 — gate SSOT "el documento de origen todavía permite procesamiento electrónico". Lo
/// implementa el módulo dueño del origen (p.ej. Retentions: solo <c>Issued</c>) y lo consumen
/// únicamente <see cref="ElectronicDocumentIssuer"/> y <see cref="ElectronicDocumentSourceCancellation"/>
/// (que toma el mismo lock desde la anulación del origen) — nunca jobs, controllers ni handlers.
/// ElectronicDocuments no referencia al módulo dueño: lo resuelve por <c>SourceModule</c>, con la
/// misma referencia débil de ADR-023.
///
/// Un origen con guard registrado usa además el protocolo seguro de despacho de ADR-036
/// (Dispatching persistido antes de llamar al SRI, sin reenvío ciego). Un origen sin guard
/// (Ventas/Notas de Crédito) conserva exactamente su comportamiento de ElectronicDocuments v1.0.
/// </summary>
public interface IElectronicDocumentSourceLifecycleGuard
{
    /// <summary>Valor de <c>ElectronicDocument.SourceModule</c> que este guard gobierna.</summary>
    string SourceModule { get; }

    /// <summary>
    /// Evalúa el estado ACTUAL del origen en la base de datos (nunca una instancia ya trackeada),
    /// filtrando por tenant y empresa. Con <paramref name="lockForUpdate"/> toma además
    /// <c>SELECT … FOR UPDATE</c> sobre la fila del origen — solo tiene efecto dentro de una
    /// transacción abierta, y es el lock que serializa "reclamar el envío" contra "anular el origen".
    /// </summary>
    Task<ElectronicDocumentSourceLifecycle> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        Guid sourceEntityId,
        bool lockForUpdate,
        CancellationToken ct = default
    );
}

public interface IElectronicDocumentSourceLifecycleGuardResolver
{
    /// <summary>El guard del módulo, o <c>null</c> si el módulo no registró ninguno (comportamiento v1.0).</summary>
    IElectronicDocumentSourceLifecycleGuard? Resolve(string sourceModule);
}

public sealed class ElectronicDocumentSourceLifecycleGuardResolver
    : IElectronicDocumentSourceLifecycleGuardResolver
{
    private readonly IReadOnlyDictionary<string, IElectronicDocumentSourceLifecycleGuard> _guards;

    public ElectronicDocumentSourceLifecycleGuardResolver(
        IEnumerable<IElectronicDocumentSourceLifecycleGuard> guards
    )
    {
        _guards = guards.ToDictionary(g => g.SourceModule);
    }

    public IElectronicDocumentSourceLifecycleGuard? Resolve(string sourceModule) =>
        _guards.TryGetValue(sourceModule, out var guard) ? guard : null;
}
