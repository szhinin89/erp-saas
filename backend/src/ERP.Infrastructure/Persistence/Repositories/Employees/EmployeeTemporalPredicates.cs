using System.Linq.Expressions;
using ERP.Domain.Modules.Employees.Entities;

namespace ERP.Infrastructure.Persistence.Repositories.Employees;

/// <summary>
/// Única definición de «cubre la fecha» para las consultas (fechas inclusivas, fin nulo = abierto): la usan el
/// filtro por sucursal de la lista (D-E5-4) y el directorio (EMP-BR-12). Se traducen a SQL.
/// </summary>
internal static class EmployeeTemporalPredicates
{
    public static Expression<Func<EmployeeEmploymentPeriod, bool>> PeriodCovers(DateOnly date) =>
        p => p.StartDate <= date && (p.EndDate == null || p.EndDate >= date);

    public static Expression<Func<EmployeeBranchAssignment, bool>> AssignmentCovers(DateOnly date) =>
        a => a.StartDate <= date && (a.EndDate == null || a.EndDate >= date);
}
