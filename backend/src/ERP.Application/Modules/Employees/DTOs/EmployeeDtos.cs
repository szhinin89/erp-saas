namespace ERP.Application.Modules.Employees.DTOs;

/// <summary>Datos mínimos del BP, por proyección de solo lectura (P-05, EMP-BR-19).</summary>
public sealed record EmployeeBusinessPartnerDto(
    Guid Id,
    string? DisplayName,
    string? IdentificationType,
    string? IdentificationNumber
);

/// <summary>Referencia a un registro de catálogo laboral (cargo, profesión o especialidad).</summary>
public sealed record EmployeeCatalogRefDto(Guid Id, string Name, bool IsActive);

public sealed record EmployeePeriodDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? TerminationReason
);

/// <summary>
/// Ficha completa (contrato §9). <c>Assignments</c> e <c>IdentityUser</c> permanecen vacío/nulo
/// hasta EMP-E5 y EMP-E6.
/// </summary>
public sealed record EmployeeDto(
    Guid Id,
    Guid CompanyId,
    EmployeeBusinessPartnerDto BusinessPartner,
    bool IsActive,
    EmployeeCatalogRefDto? JobPosition,
    EmployeeCatalogRefDto? Profession,
    IReadOnlyList<EmployeeCatalogRefDto> Specialties,
    object? IdentityUser,
    IReadOnlyList<EmployeePeriodDto> Periods,
    IReadOnlyList<EmployeeBranchAssignmentDto> Assignments,
    uint RowVersion,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Asignación a sucursal con su historial (EMP-BR-11). Sucursal: identificador, nombre y código por
/// proyección de solo lectura.
/// </summary>
public sealed record EmployeeBranchAssignmentDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string? BranchCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    uint RowVersion
);

public sealed record EmployeeListItemDto(
    Guid Id,
    Guid CompanyId,
    EmployeeBusinessPartnerDto BusinessPartner,
    bool IsActive,
    EmployeeCatalogRefDto? JobPosition,
    EmployeeCatalogRefDto? Profession,
    uint RowVersion
);

public sealed record EmployeeCatalogEntryDto(Guid Id, string Name, bool IsActive, uint RowVersion);

public sealed record EmployeeListFilter(
    bool? IsActive,
    string? Search,
    Guid? JobPositionId,
    Guid? ProfessionId,
    Guid? SpecialtyId,
    Guid? BranchId = null,
    DateOnly? AsOf = null
);
