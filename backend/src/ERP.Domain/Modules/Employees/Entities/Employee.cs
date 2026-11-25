using ERP.Domain.Common;
using ERP.Domain.Modules.Employees.Events;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Ficha laboral de un BusinessPartner en una empresa (DEC-E1): una ficha por
/// <c>(CompanyId, BusinessPartnerId)</c>. No duplica nombre, identificación ni contactos del BP
/// (DEC-E3). <see cref="IsActive"/> indica únicamente que existe un período laboral abierto
/// (INV-06); «activo en la fecha D» se evalúa con los rangos de los períodos.
/// </summary>
public sealed class Employee : AuditableEntity, ICompanyOperationalEntity
{
    private readonly List<EmployeeEmploymentPeriod> _periods = new();

    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid? IdentityUserId { get; private set; }
    public Guid? JobPositionId { get; private set; }
    public Guid? ProfessionId { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<EmployeeEmploymentPeriod> Periods => _periods.AsReadOnly();

    private Employee() { }

    /// <summary>
    /// Crea la ficha y abre su primer período (INV-06, EMP-BR-07). Cargo y profesión son
    /// opcionales (P-01). <c>CompanyId</c> solo se asigna aquí y es inmutable (INV-03).
    /// </summary>
    public static Employee Create(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        DateOnly startDate,
        Guid createdBy,
        Guid? jobPositionId = null,
        Guid? professionId = null
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException(
                "El BusinessPartner es obligatorio.",
                nameof(businessPartnerId)
            );
        if (startDate == default)
            throw new ArgumentException("La fecha de ingreso es obligatoria.", nameof(startDate));
        if (jobPositionId == Guid.Empty)
            throw new ArgumentException("jobPositionId inválido.", nameof(jobPositionId));
        if (professionId == Guid.Empty)
            throw new ArgumentException("professionId inválido.", nameof(professionId));

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            JobPositionId = jobPositionId,
            ProfessionId = professionId,
            IsActive = true,
        };
        employee.SetCreated(createdBy);
        employee._periods.Add(
            EmployeeEmploymentPeriod.Open(tenantId, companyId, employee.Id, startDate, createdBy)
        );
        employee.RaiseDomainEvent(
            new EmployeeCreatedEvent(tenantId, companyId, employee.Id, businessPartnerId, startDate)
        );
        return employee;
    }

    /// <summary>Cambia el cargo (null lo elimina, P-01). Sin efecto si no cambia.</summary>
    public void ChangeJobPosition(Guid? jobPositionId, Guid updatedBy)
    {
        if (jobPositionId == Guid.Empty)
            throw new ArgumentException("jobPositionId inválido.", nameof(jobPositionId));
        if (jobPositionId == JobPositionId)
            return;

        var old = JobPositionId;
        JobPositionId = jobPositionId;
        SetUpdated(updatedBy);
        RaiseFieldChanged(EmployeeFieldChangedEvent.JobPositionField, old?.ToString(), jobPositionId?.ToString());
    }

    /// <summary>Cambia la profesión (null la elimina, P-01). Sin efecto si no cambia.</summary>
    public void ChangeProfession(Guid? professionId, Guid updatedBy)
    {
        if (professionId == Guid.Empty)
            throw new ArgumentException("professionId inválido.", nameof(professionId));
        if (professionId == ProfessionId)
            return;

        var old = ProfessionId;
        ProfessionId = professionId;
        SetUpdated(updatedBy);
        RaiseFieldChanged(EmployeeFieldChangedEvent.ProfessionField, old?.ToString(), professionId?.ToString());
    }

    /// <summary>
    /// Registra en la auditoría que una especialidad se asignó o retiró (EMP-BR-06). El vínculo
    /// (<see cref="EmployeeSpecialty"/>) cambia por separado; este método deja el rastro en la ficha.
    /// </summary>
    public void RecordSpecialtyChange(Guid specialtyId, bool assigned, Guid updatedBy)
    {
        SetUpdated(updatedBy);
        RaiseFieldChanged(
            EmployeeFieldChangedEvent.SpecialtyField,
            assigned ? null : specialtyId.ToString(),
            assigned ? specialtyId.ToString() : null
        );
    }

    /// <summary>
    /// Marca la ficha como modificada: fuerza el UPDATE que comprueba el token de concurrencia
    /// (<c>xmin</c>) aunque la edición no cambie columnas de la ficha (p. ej. solo especialidades).
    /// </summary>
    public void Touch(Guid updatedBy) => SetUpdated(updatedBy);

    private void RaiseFieldChanged(string field, string? oldValue, string? newValue) =>
        RaiseDomainEvent(
            new EmployeeFieldChangedEvent(
                TenantId,
                CompanyId,
                Id,
                BusinessPartnerId,
                field,
                oldValue,
                newValue
            )
        );
}
