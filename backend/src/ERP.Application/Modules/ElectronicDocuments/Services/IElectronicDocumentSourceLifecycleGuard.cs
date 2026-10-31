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
/// Todo origen con guard serializa bajo ese lock el reclamo del envío (revalidación del origen y
/// persistencia antes de llamar al SRI) contra la anulación del origen
/// (<see cref="IElectronicDocumentSourceCancellation"/>). <see cref="UsesDispatchProtocol"/> decide si
/// además usa el protocolo completo de ADR-036 (estado Dispatching y reintentos bajo el gate).
/// </summary>
public interface IElectronicDocumentSourceLifecycleGuard
{
    /// <summary>Valor de <c>ElectronicDocument.SourceModule</c> que este guard gobierna.</summary>
    string SourceModule { get; }

    /// <summary>
    /// True (Retenciones, ADR-036): el reclamo se persiste como Dispatching (solo consulta manual) y
    /// todo reintento pasa por el gate. False (Ventas: Factura 01 / Nota de Crédito 04, R12 Fase A): el
    /// reclamo bajo lock persiste Signed y el resto del pipeline v1.0 no cambia (job de reintentos y
    /// consulta por clave de acceso, R11 y Caso #32 incluidos).
    /// </summary>
    bool UsesDispatchProtocol { get; }

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
