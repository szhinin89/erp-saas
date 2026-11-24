using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="Employee"/> (EMP-BR-20). Append-only. El actor vive solo en
/// <see cref="AuditActor"/>; aquí únicamente el contexto laboral y un cambio tipado como texto.
/// </summary>
public sealed class EmployeeAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public const int MaxValueLength = 200;
    public const int MaxFieldLength = 60;

    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public string? ChangedField { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }

    private EmployeeAudit() { }

    public static EmployeeAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid employeeId,
        Guid businessPartnerId,
        string action,
        string? changedField = null,
        string? oldValue = null,
        string? newValue = null,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new EmployeeAudit
        {
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            ChangedField = Truncate(changedField, MaxFieldLength),
            OldValue = Truncate(oldValue, MaxValueLength),
            NewValue = Truncate(newValue, MaxValueLength),
        };
        audit.SetCommon(actor, employeeId, action, reason);
        return audit;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value
        : value[..max];
}
