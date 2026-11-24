using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Período laboral de una ficha (EMP-T-06). Fechas de negocio inclusivas (<see cref="DateOnly"/>,
/// ADR-034). Un período abierto tiene <see cref="EndDate"/> nulo. Ningún período se edita ni se
/// borra (P-02); la baja y la reincorporación pertenecen a EMP-E5.
/// </summary>
public sealed class EmployeeEmploymentPeriod : AuditableEntity, ICompanyOperationalEntity
{
    public const int MaxTerminationReasonLength = 500;

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? TerminationReason { get; private set; }

    private EmployeeEmploymentPeriod() { }

    internal static EmployeeEmploymentPeriod Open(
        Guid tenantId,
        Guid companyId,
        Guid employeeId,
        DateOnly startDate,
        Guid createdBy
    )
    {
        if (startDate == default)
            throw new ArgumentException("La fecha de ingreso es obligatoria.", nameof(startDate));

        var period = new EmployeeEmploymentPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            StartDate = startDate,
        };
        period.SetCreated(createdBy);
        return period;
    }
}
