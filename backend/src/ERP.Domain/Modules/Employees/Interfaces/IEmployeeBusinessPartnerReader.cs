namespace ERP.Domain.Modules.Employees.Interfaces;

/// <summary>Estado del BP relevante para el alta de un empleado (EMP-BR-02), leído sin seguimiento.</summary>
public sealed record EmployeeBpEligibility(
    Guid BusinessPartnerId,
    bool IsActive,
    bool HasActiveEmployeeRole,
    string IdentificationType
);

/// <summary>
/// Lectura de solo lectura del BP desde Empleados (P-05, D-5). Employees no edita BusinessPartner
/// ni duplica sus datos personales.
/// </summary>
public interface IEmployeeBusinessPartnerReader
{
    /// <summary>
    /// Lectura fresca (sin seguimiento) del BP del tenant, o null si no existe o es de otro tenant.
    /// </summary>
    Task<EmployeeBpEligibility?> GetEligibilityAsync(
        Guid tenantId,
        Guid businessPartnerId,
        CancellationToken ct = default
    );
}
