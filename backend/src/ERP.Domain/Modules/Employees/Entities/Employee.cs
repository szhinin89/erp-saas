using ERP.Domain.Common;
using ERP.Domain.Exceptions;
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

    /// <summary>Período abierto (sin fecha fin), si la ficha está activa (INV-04, INV-06).</summary>
    public EmployeeEmploymentPeriod? OpenPeriod => _periods.SingleOrDefault(p => p.EndDate is null);

    /// <summary>Fecha fin del último período cerrado (la reincorporación debe ser posterior, INV-13).</summary>
    public DateOnly? LastEndDate => _periods.Where(p => p.EndDate is not null).Max(p => p.EndDate);

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

    /// <summary>
    /// Baja (EMP-BR-08): cierra el período abierto, acorta las asignaciones vigentes a la fecha de baja y
    /// deja la ficha inactiva (INV-06, INV-09). Las precondiciones con código de negocio las valida el caso
    /// de uso; aquí se defienden los invariantes. Las asignaciones ya terminadas no se modifican.
    /// </summary>
    public void Terminate(
        DateOnly endDate,
        string? reason,
        IReadOnlyCollection<EmployeeBranchAssignment> assignments,
        Guid updatedBy
    )
    {
        var open = OpenPeriod ?? throw new DomainRuleViolationException("La ficha ya está dada de baja.");
        if (assignments.Any(a => a.StartDate > endDate))
            throw new DomainRuleViolationException("Existen asignaciones posteriores a la fecha de baja.");

        open.Close(endDate, reason, updatedBy);
        foreach (var assignment in assignments.Where(a => a.EndDate is null || a.EndDate > endDate))
        {
            var oldEnd = assignment.EndDate;
            assignment.TruncateTo(endDate, updatedBy);
            RaiseAssignmentEvent(EmployeeBranchAssignmentChangedEvent.ClosedAction, assignment, oldEnd);
        }
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new EmployeeTerminatedEvent(TenantId, CompanyId, Id, BusinessPartnerId, endDate, reason));
    }

    /// <summary>
    /// Reincorporación (EMP-BR-09): abre un período nuevo estrictamente posterior al último fin; no reabre
    /// períodos ni asignaciones anteriores (INV-12, INV-13) y conserva cargo, profesión y especialidades.
    /// </summary>
    public EmployeeEmploymentPeriod Rehire(DateOnly startDate, Guid updatedBy)
    {
        if (OpenPeriod is not null)
            throw new DomainRuleViolationException("La ficha ya está activa.");
        if (startDate == default || (LastEndDate is { } last && startDate <= last))
            throw new DomainRuleViolationException("La reincorporación debe ser posterior a la baja anterior.");

        var period = EmployeeEmploymentPeriod.Open(TenantId, CompanyId, Id, startDate, updatedBy);
        _periods.Add(period);
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new EmployeeRehiredEvent(TenantId, CompanyId, Id, BusinessPartnerId, startDate));
        return period;
    }

    /// <summary>Registra en la auditoría la creación de una asignación a sucursal.</summary>
    public void RecordAssignmentCreated(EmployeeBranchAssignment assignment) =>
        RaiseAssignmentEvent(EmployeeBranchAssignmentChangedEvent.AssignedAction, assignment, null);

    /// <summary>Registra en la auditoría el cierre de una asignación (<paramref name="oldEnd"/> previo).</summary>
    public void RecordAssignmentClosed(EmployeeBranchAssignment assignment, DateOnly? oldEnd) =>
        RaiseAssignmentEvent(EmployeeBranchAssignmentChangedEvent.ClosedAction, assignment, oldEnd);

    private void RaiseAssignmentEvent(string action, EmployeeBranchAssignment a, DateOnly? oldEnd) =>
        RaiseDomainEvent(
            new EmployeeBranchAssignmentChangedEvent(
                TenantId, CompanyId, Id, BusinessPartnerId, action, a.BranchId, a.StartDate, oldEnd, a.EndDate));

    /// <summary>
    /// Vincula un usuario ERP a la ficha (EMP-BR-13). La elegibilidad (membresía, no global, unicidad) la
    /// valida el caso de uso; no reemplaza un vínculo existente (D-E6-2). No concede acceso.
    /// </summary>
    public void LinkUser(Guid identityUserId, Guid updatedBy)
    {
        if (identityUserId == Guid.Empty)
            throw new ArgumentException("identityUserId requerido.", nameof(identityUserId));
        if (IdentityUserId is not null)
            throw new DomainRuleViolationException("La ficha ya tiene un usuario vinculado.");

        IdentityUserId = identityUserId;
        SetUpdated(updatedBy);
        RaiseUserLink(EmployeeUserLinkChangedEvent.LinkedAction, null, identityUserId);
    }

    /// <summary>Quita el vínculo (EMP-BR-14). No toca la cuenta, la membresía ni los permisos.</summary>
    public void UnlinkUser(Guid updatedBy)
    {
        var previous = IdentityUserId
            ?? throw new DomainRuleViolationException("La ficha no tiene un usuario vinculado.");

        IdentityUserId = null;
        SetUpdated(updatedBy);
        RaiseUserLink(EmployeeUserLinkChangedEvent.UnlinkedAction, previous, null);
    }

    private void RaiseUserLink(string action, Guid? oldUserId, Guid? newUserId) =>
        RaiseDomainEvent(
            new EmployeeUserLinkChangedEvent(
                TenantId, CompanyId, Id, BusinessPartnerId, action, oldUserId, newUserId));
}
