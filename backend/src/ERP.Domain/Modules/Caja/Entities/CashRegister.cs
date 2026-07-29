using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>
/// Caja física/lógica operativa dentro de una sucursal (ADR — Rediseño del módulo de Caja).
/// Separa el concepto operativo "quién cobra, en qué caja, en qué sucursal" del concepto
/// tributario SRI <see cref="ERP.Domain.Modules.Company.Entities.EmissionPoint"/> — un punto de
/// emisión puede ser usado por varias cajas de la misma sucursal.
/// </summary>
public sealed class CashRegister : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int CodeMaxLen = 30;
    public const int NameMaxLen = 100;
    public const int NotesMaxLen = 500;

    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Sucursal propietaria (Branch Ownership Rule) — asignada exclusivamente al crear la caja
    /// desde <c>ICurrentBranch</c> del handler, nunca desde el cliente. Inmutable para siempre.
    /// </summary>
    public Guid BranchId { get; private set; }

    /// <summary>
    /// Punto de emisión SRI vigente para esta caja. Nullable mientras la caja no está configurada
    /// para facturar. Solo se muta mediante <see cref="ChangeEmissionPoint"/>.
    /// </summary>
    public Guid? EmissionPointId { get; private set; }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Notes { get; private set; }

    /// <summary>
    /// Bodega preseleccionada al iniciar una venta desde esta caja — conveniencia operativa del
    /// puesto, no identidad tributaria (a diferencia de <see cref="EmissionPointId"/>, se mantiene
    /// editable aunque la caja ya tenga historial). Solo se muta mediante
    /// <see cref="SetDefaultWarehouse"/>.
    /// </summary>
    public Guid? DefaultWarehouseId { get; private set; }

    /// <summary>
    /// Cliente preseleccionado al iniciar una venta desde esta caja (p. ej. "Consumidor Final",
    /// un <see cref="BusinessPartner"/> ordinario del catálogo — nunca un flag especial). Misma
    /// naturaleza que <see cref="DefaultWarehouseId"/>: conveniencia operativa, siempre editable.
    /// Solo se muta mediante <see cref="SetDefaultCustomer"/>.
    /// </summary>
    public Guid? DefaultCustomerId { get; private set; }

    // EF navigation — solo lectura, proyección de nombres para DTOs (nunca mutadas fuera de aquí).
    public Branch Branch { get; private set; } = null!;
    public EmissionPoint? EmissionPoint { get; private set; }
    public Warehouse? DefaultWarehouse { get; private set; }
    public BusinessPartner? DefaultCustomer { get; private set; }

    private CashRegister() { }

    public static CashRegister Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string code,
        string name,
        Guid createdBy,
        Guid? emissionPointId = null,
        string? notes = null,
        Guid? defaultWarehouseId = null,
        Guid? defaultCustomerId = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la caja es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la caja es obligatorio.", nameof(name));
        if (emissionPointId == Guid.Empty)
            throw new ArgumentException(
                "El punto de emisión no puede ser un Guid vacío.",
                nameof(emissionPointId)
            );
        if (defaultWarehouseId == Guid.Empty)
            throw new ArgumentException(
                "La bodega por defecto no puede ser un Guid vacío.",
                nameof(defaultWarehouseId)
            );
        if (defaultCustomerId == Guid.Empty)
            throw new ArgumentException(
                "El cliente por defecto no puede ser un Guid vacío.",
                nameof(defaultCustomerId)
            );

        var register = new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            Code = code.Trim(),
            Name = name.Trim(),
            EmissionPointId = emissionPointId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            DefaultWarehouseId = defaultWarehouseId,
            DefaultCustomerId = defaultCustomerId,
        };
        register.SetCreated(createdBy);
        return register;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Caja Principal), ver política de Bootstrap en <c>CLAUDE.md</c>.
    /// <see cref="ChangeEmissionPoint"/> permanece abierto.
    /// </summary>
    public static CashRegister CreateSystemSeeded(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string code,
        string name,
        Guid createdBy,
        Guid? emissionPointId = null,
        string? notes = null
    )
    {
        var register = Create(
            tenantId,
            companyId,
            branchId,
            code,
            name,
            createdBy,
            emissionPointId,
            notes
        );
        register.MarkAsSystemSeeded();
        return register;
    }

    /// <summary>
    /// Único mutador autorizado de <see cref="Name"/>/<see cref="Notes"/>. <see cref="Code"/> es
    /// inmutable post-creación (igual que <see cref="EmissionPoint.Code"/>); <see cref="BranchId"/>
    /// permanece inmutable para siempre (Branch Ownership Rule).
    /// </summary>
    public void Update(string name, string? notes, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la caja es obligatorio.", nameof(name));

        Name = name.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Único método autorizado para asignar o reasignar el punto de emisión de esta caja.
    /// Acepta <c>null</c> para dejar la caja sin punto de emisión configurado.
    /// </summary>
    public void ChangeEmissionPoint(Guid? emissionPointId, Guid updatedBy)
    {
        if (emissionPointId == Guid.Empty)
            throw new ArgumentException(
                "El punto de emisión no puede ser un Guid vacío.",
                nameof(emissionPointId)
            );

        EmissionPointId = emissionPointId;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Único método autorizado para asignar o quitar la bodega por defecto de esta caja. A
    /// diferencia de <see cref="ChangeEmissionPoint"/>, no está sujeto a ninguna regla de "caja
    /// usada" — es una conveniencia operativa, siempre editable.
    /// </summary>
    public void SetDefaultWarehouse(Guid? warehouseId, Guid updatedBy)
    {
        if (warehouseId == Guid.Empty)
            throw new ArgumentException(
                "La bodega por defecto no puede ser un Guid vacío.",
                nameof(warehouseId)
            );

        DefaultWarehouseId = warehouseId;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Único método autorizado para asignar o quitar el cliente por defecto de esta caja (p. ej.
    /// "Consumidor Final" — un <see cref="BusinessPartner"/> ordinario, nunca un flag especial).
    /// Siempre editable, igual que <see cref="SetDefaultWarehouse"/>.
    /// </summary>
    public void SetDefaultCustomer(Guid? customerId, Guid updatedBy)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException(
                "El cliente por defecto no puede ser un Guid vacío.",
                nameof(customerId)
            );

        DefaultCustomerId = customerId;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La caja", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
