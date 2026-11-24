using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Services;

namespace ERP.Domain.Modules.Employees.Entities;

/// <summary>
/// Base de los tres catálogos laborales por empresa (DEC-E5): nombre, valor normalizado con la
/// única regla <see cref="EmployeeNameNormalizer"/> y activación/inactivación sin borrado físico
/// (EMP-BR-15). Cada catálogo es una tabla propia.
/// </summary>
public abstract class EmployeeCatalogEntry : AuditableEntity, ICompanyOperationalEntity
{
    public const int MaxNameLength = 150;

    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public abstract EmployeeCatalogType CatalogType { get; }

    protected void Initialize(Guid tenantId, Guid companyId, string name, Guid createdBy)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var (clean, normalized) = Prepare(name);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        Name = clean;
        NormalizedName = normalized;
        IsActive = true;
        SetCreated(createdBy);
        RaiseDomainEvent(
            new EmployeeCatalogChangedEvent(
                tenantId,
                companyId,
                CatalogType,
                Id,
                EmployeeCatalogChangedEvent.CreatedAction,
                null,
                clean,
                true
            )
        );
    }

    public void Rename(string name, Guid updatedBy)
    {
        var (clean, normalized) = Prepare(name);
        if (string.Equals(clean, Name, StringComparison.Ordinal))
            return;

        var oldName = Name;
        Name = clean;
        NormalizedName = normalized;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new EmployeeCatalogChangedEvent(
                TenantId,
                CompanyId,
                CatalogType,
                Id,
                EmployeeCatalogChangedEvent.RenamedAction,
                oldName,
                clean,
                IsActive
            )
        );
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            throw new DomainRuleViolationException("El registro ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new EmployeeCatalogChangedEvent(
                TenantId,
                CompanyId,
                CatalogType,
                Id,
                EmployeeCatalogChangedEvent.DeactivatedAction,
                Name,
                Name,
                false
            )
        );
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new DomainRuleViolationException("El registro ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new EmployeeCatalogChangedEvent(
                TenantId,
                CompanyId,
                CatalogType,
                Id,
                EmployeeCatalogChangedEvent.ActivatedAction,
                Name,
                Name,
                true
            )
        );
    }

    private static (string Clean, string Normalized) Prepare(string name)
    {
        var normalized = EmployeeNameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        var clean = string.Join(
            ' ',
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        if (clean.Length > MaxNameLength)
            throw new ArgumentException(
                $"El nombre no puede superar {MaxNameLength} caracteres.",
                nameof(name)
            );
        return (clean, normalized);
    }
}
