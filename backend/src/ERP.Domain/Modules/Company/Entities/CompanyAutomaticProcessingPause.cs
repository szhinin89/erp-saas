using System.Linq.Expressions;
using ERP.Domain.Modules.Company.Enums;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>Motivo por el que una empresa pausa los procesos automáticos con efecto externo (ADR-048).</summary>
[Flags]
public enum CompanyPauseReason
{
    None = 0,

    /// <summary><see cref="CompanyOperationalStatus.Suspended"/>.</summary>
    Suspended = 1,

    /// <summary>Deshabilitada administrativamente (<c>IsActive=false</c>).</summary>
    Inactive = 2,
}

/// <summary>
/// ADR-048 — única definición de «empresa pausada para procesos automáticos»: <c>IsActive=false</c> o
/// <c>OperationalStatus=Suspended</c>. <see cref="CompanyOperationalStatus.PendingSetup"/> NO pausa (conserva su
/// comportamiento). La empresa reanuda cuando ambos estados se restablecen. La misma regla en memoria
/// (<see cref="ReasonOf"/>) y en SQL (<see cref="IsPaused"/>).
/// </summary>
public static class CompanyAutomaticProcessingPause
{
    public static Expression<Func<Company, bool>> IsPaused { get; } =
        c => !c.IsActive || c.OperationalStatus == CompanyOperationalStatus.Suspended;

    public static CompanyPauseReason ReasonOf(bool isActive, CompanyOperationalStatus operationalStatus)
    {
        var reason = CompanyPauseReason.None;
        if (!isActive)
            reason |= CompanyPauseReason.Inactive;
        if (operationalStatus == CompanyOperationalStatus.Suspended)
            reason |= CompanyPauseReason.Suspended;
        return reason;
    }
}
