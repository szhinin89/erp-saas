using ERP.Application.Modules.Employees.DTOs;

namespace ERP.API.Contracts;

/// <summary>
/// Cuerpo de <c>PATCH /employees/{id}</c>. Una propiedad omitida no se modifica; enviada como
/// <c>null</c> elimina la referencia (D-3). El identificador de la ficha viene de la ruta.
/// </summary>
public sealed record UpdateEmployeeRequest(
    uint RowVersion,
    OptionalField<Guid?> JobPositionId = default,
    OptionalField<Guid?> ProfessionId = default
);

/// <summary>Cuerpo de <c>PUT /employees/{id}/specialties</c>: el conjunto de especialidades activas.</summary>
public sealed record SetEmployeeSpecialtiesRequest(uint RowVersion, IReadOnlyList<Guid> SpecialtyIds);

/// <summary>Cuerpo del alta de un registro de catálogo laboral.</summary>
public sealed record EmployeeCatalogEntryRequest(string Name);

/// <summary>Cuerpo del renombre: versión leída (<c>rowVersion</c>) y nuevo nombre.</summary>
public sealed record UpdateEmployeeCatalogEntryRequest(uint RowVersion, string Name);

/// <summary>Cuerpo de activar o inactivar: versión leída (<c>rowVersion</c>).</summary>
public sealed record EmployeeCatalogVersionRequest(uint RowVersion);
