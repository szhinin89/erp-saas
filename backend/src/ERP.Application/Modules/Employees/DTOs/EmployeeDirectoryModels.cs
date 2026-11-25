namespace ERP.Application.Modules.Employees.DTOs;

/// <summary>
/// Resumen laboral de una ficha para otros módulos (contrato <c>IEmployeeDirectory</c>). Solo lectura;
/// identificadores estables y nunca reutilizados. <c>IdentityUserId</c> es nulo sin vínculo o si IAM-V1-F1
/// protege la cuenta. <c>IsActive</c> indica un período abierto hoy según lo registrado: para una fecha se usa
/// <c>IsActiveOnAsync</c>. Cargo y profesión pueden ser nulos (P-01).
/// </summary>
public sealed record EmployeeSummary(
    Guid EmployeeId,
    Guid CompanyId,
    Guid BusinessPartnerId,
    string DisplayName,
    Guid? IdentityUserId,
    bool IsActive,
    Guid? JobPositionId,
    Guid? ProfessionId,
    IReadOnlyList<Guid> SpecialtyIds,
    IReadOnlyList<EmployeePeriodSummary> Periods,
    IReadOnlyList<EmployeeAssignmentSummary> Assignments
);

/// <summary>Período laboral (fechas inclusivas; fin nulo = abierto), sin datos de la baja.</summary>
public sealed record EmployeePeriodSummary(DateOnly StartDate, DateOnly? EndDate);

/// <summary>Asignación a sucursal (fechas inclusivas; fin nulo = abierta).</summary>
public sealed record EmployeeAssignmentSummary(
    Guid AssignmentId,
    Guid BranchId,
    DateOnly StartDate,
    DateOnly? EndDate
);

/// <summary>Filtro adicional de <c>ListAsync</c>: especialidad activa en la ficha.</summary>
public sealed record EmployeeDirectoryFilter(Guid? SpecialtyId = null);
