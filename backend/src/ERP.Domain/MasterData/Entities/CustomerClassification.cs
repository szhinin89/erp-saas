using ERP.Domain.Common;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Catálogo persistido de clasificación de BusinessPartner (CLASS-BP-CATALOGS-01): La clasificación tributaria/comercial de cliente.
/// Tenant+company-scoped, con bootstrap por empresa (CreateSystemSeeded) y backfill idempotente
/// para empresas existentes. Reemplaza el HashSet fijo CUSTOMER_CLASSIFICATIONS (frontend, sin HashSet previo).
/// Solo lectura desde el frontend en este bloque — CRUD administrativo queda fuera de alcance.
/// </summary>
public sealed class CustomerClassification : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity, IClassificationCatalogEntity
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 120;

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }

    private CustomerClassification() { }

    public static CustomerClassification Create(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        int sortOrder,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Trim().Length > CodeMaxLength)
            throw new ArgumentException(
                $"El código no puede superar {CodeMaxLength} caracteres.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLength)
            throw new ArgumentException(
                $"El nombre no puede superar {NameMaxLength} caracteres.",
                nameof(name)
            );

        var entity = new CustomerClassification
        {
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code.Trim(),
            Name = name.Trim(),
            SortOrder = sortOrder,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa / backfill: idéntica a <see cref="Create"/> pero
    /// marca el registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>).
    /// </summary>
    public static CustomerClassification CreateSystemSeeded(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        int sortOrder,
        Guid createdBy
    )
    {
        var entity = Create(tenantId, companyId, code, name, sortOrder, createdBy);
        entity.MarkAsSystemSeeded();
        return entity;
    }

    public void Update(string name, int sortOrder, Guid updatedBy)
    {
        this.EnsureEditable("La clasificación tributaria/comercial de cliente", "actualizarse");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLength)
            throw new ArgumentException(
                $"El nombre no puede superar {NameMaxLength} caracteres.",
                nameof(name)
            );

        Name = name.Trim();
        SortOrder = sortOrder;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La clasificación tributaria/comercial de cliente", "deshabilitarse");
        base.Disable(updatedBy);
    }
}
