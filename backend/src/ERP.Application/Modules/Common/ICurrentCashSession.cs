namespace ERP.Application.Common;

/// <summary>
/// Expone la sesión de caja (<c>CashSession</c>) abierta del usuario autenticado en el request en
/// curso — contexto operativo POS, análogo a <see cref="ICurrentBranch"/> (ADR — Rediseño del
/// módulo de Caja, Fase 3). Puramente de lectura: nunca abre, cierra ni modifica una sesión.
/// </summary>
/// <remarks>
/// Todos los valores son <c>null</c>/<c>false</c> cuando no hay una sesión abierta que cumpla,
/// simultáneamente: pertenece al usuario autenticado, está en estado <c>Open</c>, y su
/// <c>BranchId</c> coincide con la sucursal activa (<see cref="ICurrentBranch"/>). Nunca lanza una
/// excepción por ausencia de sesión — bloquear el acceso es responsabilidad de un guard/pipeline
/// futuro (fuera de alcance de esta fase), no de este contexto de solo lectura.
/// </remarks>
public interface ICurrentCashSession
{
    Guid? CashSessionId { get; }

    /// <summary>Caja física/lógica de la sesión — proviene de <c>CashSession.CashRegisterId</c>, nunca resuelto en vivo contra el catálogo de cajas.</summary>
    Guid? CashRegisterId { get; }

    /// <summary>Punto de emisión efectivo del turno — snapshot de <c>CashSession.EmissionPointId</c> al momento de abrir, no el <c>EmissionPointId</c> vigente hoy en el <c>CashRegister</c>.</summary>
    Guid? EmissionPointId { get; }

    Guid? BranchId { get; }

    bool HasOpenSession { get; }

    /// <summary>
    /// Snapshot histórico (<c>CashSession.CashRegisterCodeSnapshot</c>) — se expone porque ya se
    /// materializa junto con el resto de la sesión al resolver el contexto; no representa una
    /// consulta adicional. Útil para UI POS ("Caja: CAJA-01") sin un segundo viaje al catálogo.
    /// </summary>
    string? CashRegisterCodeSnapshot { get; }

    /// <summary>Snapshot histórico (<c>CashSession.CashRegisterNameSnapshot</c>) — mismo criterio que <see cref="CashRegisterCodeSnapshot"/>.</summary>
    string? CashRegisterNameSnapshot { get; }
}
