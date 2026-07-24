namespace ERP.Domain.Configuration.Enums;

/// <summary>
/// Ámbito organizacional al que pertenece una configuración.
/// La jerarquía es: Company → Branch → (Establishment → EmissionPoint | Warehouse).
/// </summary>
public enum OrgScope
{
    Company        = 1,
    Branch         = 2,
    Establishment  = 3,
    EmissionPoint  = 4,
    Warehouse      = 5,
}
