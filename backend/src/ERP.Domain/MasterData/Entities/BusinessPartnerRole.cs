using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Events;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: asignación de un rol a un BusinessPartner dentro del tenant.
///
/// SCOPE: ITenantScopedEntity.
/// DISEÑO: AR independiente de BusinessPartner. La invariante de unicidad (un solo rol
///         por tipo por BP) está garantizada por el índice UNIQUE de BD y
///         IDatabaseExceptionTranslator. Ver ADR-BP-10.
///
/// EXTENSIÓN: agregar un nuevo rol = nuevo valor en RoleType enum + (opcionalmente)
///            nueva tabla de config. CERO cambios en esta entidad. Ver ADR-BP-11.
///
/// ASIGNACIÓN: ver ADR-BP-12. Si el rol existe revocado, usar Reactivate(), no Create().
/// </summary>
public sealed class BusinessPartnerRole : AuditableEntity, ITenantScopedEntity
{
    public const int NotesMaxLen = 2000;

    public Guid BusinessPartnerId { get; private set; }
    public RoleType RoleType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }

    // ── Configs específicas por tipo de rol ───────────────────────────────────
    // Solo la config correspondiente al RoleType puede estar populated.
    // La validación de coherencia tipo↔config está en Create() y UpdateXxxConfig().
    public SupplierRoleConfig? SupplierConfig { get; private set; }
    public SupplierClassificationConfig? ClassificationConfig { get; private set; }
    public CarrierRoleConfig? CarrierConfig { get; private set; }
    public CustomerRoleConfig? CustomerConfig { get; private set; }

    private BusinessPartnerRole() { }

    /// <summary>
    /// Crea y asigna un rol a un BusinessPartner.
    ///
    /// La config es opcional en el momento de creación — puede actualizarse después
    /// con UpdateSupplierConfig() o UpdateCarrierConfig().
    ///
    /// PRECONDICIÓN (verificada en handler): el BP referenciado debe estar activo.
    /// PRECONDICIÓN (garantizada por BD): no puede existir otro rol del mismo tipo para este BP.
    /// </summary>
    public static BusinessPartnerRole Create(
        Guid tenantId,
        Guid businessPartnerId,
        RoleType roleType,
        Guid assignedBy,
        SupplierRoleConfig? supplierConfig = null,
        CarrierRoleConfig? carrierConfig = null,
        CustomerRoleConfig? customerConfig = null,
        SupplierClassificationConfig? classificationConfig = null
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException(
                "BusinessPartnerId es obligatorio.",
                nameof(businessPartnerId)
            );
        if (assignedBy == Guid.Empty)
            throw new ArgumentException("AssignedBy es obligatorio.", nameof(assignedBy));

        ValidateConfigCoherence(
            roleType,
            supplierConfig,
            carrierConfig,
            customerConfig,
            classificationConfig
        );

        var now = DateTime.UtcNow;
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            RoleType = roleType,
            IsActive = true,
            AssignedAt = now,
            AssignedBy = assignedBy,
            SupplierConfig = supplierConfig,
            ClassificationConfig = classificationConfig,
            CarrierConfig = carrierConfig,
            CustomerConfig = customerConfig,
        };
        role.SetCreated(assignedBy);
        role.RaiseDomainEvent(
            new BusinessPartnerRoleAssignedEvent
            {
                TenantId = tenantId,
                RoleId = role.Id,
                BusinessPartnerId = businessPartnerId,
                RoleType = roleType,
                AssignedBy = assignedBy,
            }
        );
        return role;
    }

    /// <summary>
    /// Reactiva un rol previamente revocado.
    /// Se usa en el flujo UPSERT de AssignRole: si el rol existe revocado → Reactivate.
    /// Ver ADR-BP-12.
    /// </summary>
    public void Reactivate(Guid reactivatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException($"El rol {RoleType} ya está activo.");
        if (reactivatedBy == Guid.Empty)
            throw new ArgumentException("ReactivatedBy es obligatorio.", nameof(reactivatedBy));

        IsActive = true;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = reactivatedBy;
        RevokedAt = null;
        RevokedBy = null;
        SetUpdated(reactivatedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleReactivatedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                ReactivatedBy = reactivatedBy,
            }
        );
    }

    /// <summary>
    /// Revoca el rol. El registro se conserva para auditoría histórica.
    ///
    /// ATENCIÓN: cuando el módulo de documentos exista, el handler debe verificar
    /// que no existan documentos activos (no-finales) para este BP en este rol.
    /// Ver ADR-BP-14.
    /// </summary>
    public void Revoke(Guid revokedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException($"El rol {RoleType} ya está revocado.");
        if (revokedBy == Guid.Empty)
            throw new ArgumentException("RevokedBy es obligatorio.", nameof(revokedBy));

        IsActive = false;
        RevokedAt = DateTime.UtcNow;
        RevokedBy = revokedBy;
        SetUpdated(revokedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleRevokedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                RevokedBy = revokedBy,
            }
        );
    }

    public void UpdateNotes(string? notes, Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar las notas de un rol revocado."
            );

        var n = notes?.Trim();
        if (n is { Length: 0 })
            n = null;
        if (n?.Length > NotesMaxLen)
            throw new ArgumentException(
                $"Las notas no pueden superar {NotesMaxLen} caracteres.",
                nameof(notes)
            );

        Notes = n;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Actualiza la configuración del rol Supplier.
    /// Solo aplica cuando RoleType = Supplier. Ver ADR-BP-11.
    /// </summary>
    public void UpdateSupplierConfig(SupplierRoleConfig config, Guid updatedBy)
    {
        if (RoleType != RoleType.Supplier)
            throw new InvalidOperationException(
                $"UpdateSupplierConfig solo aplica al rol Supplier. Rol actual: {RoleType}."
            );
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar la config de un rol revocado."
            );

        SupplierConfig = config ?? throw new ArgumentNullException(nameof(config));
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleConfigUpdatedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                UpdatedBy = updatedBy,
            }
        );
    }

    /// <summary>
    /// Actualiza la configuración del rol Carrier.
    /// Solo aplica cuando RoleType = Carrier. Ver ADR-BP-11.
    /// </summary>
    public void UpdateCarrierConfig(CarrierRoleConfig config, Guid updatedBy)
    {
        if (RoleType != RoleType.Carrier)
            throw new InvalidOperationException(
                $"UpdateCarrierConfig solo aplica al rol Carrier. Rol actual: {RoleType}."
            );
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar la config de un rol revocado."
            );

        CarrierConfig = config ?? throw new ArgumentNullException(nameof(config));
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleConfigUpdatedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                UpdatedBy = updatedBy,
            }
        );
    }

    /// <summary>
    /// Actualiza la configuración del rol Customer.
    /// Solo aplica cuando RoleType = Customer. Ver ADR-BP-11.
    /// </summary>
    public void UpdateCustomerConfig(CustomerRoleConfig config, Guid updatedBy)
    {
        if (RoleType != RoleType.Customer)
            throw new InvalidOperationException(
                $"UpdateCustomerConfig solo aplica al rol Customer. Rol actual: {RoleType}."
            );
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar la config de un rol revocado."
            );

        CustomerConfig = config ?? throw new ArgumentNullException(nameof(config));
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleConfigUpdatedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                UpdatedBy = updatedBy,
            }
        );
    }

    /// <summary>
    /// Actualiza la clasificación estratégica del rol Supplier.
    /// Solo aplica cuando RoleType = Supplier. Ver ADR-BP-11.
    /// </summary>
    public void UpdateClassificationConfig(SupplierClassificationConfig config, Guid updatedBy)
    {
        if (RoleType != RoleType.Supplier)
            throw new InvalidOperationException(
                $"UpdateClassificationConfig solo aplica al rol Supplier. Rol actual: {RoleType}."
            );
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar la config de un rol revocado."
            );

        ClassificationConfig = config ?? throw new ArgumentNullException(nameof(config));
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerRoleConfigUpdatedEvent
            {
                TenantId = TenantId,
                RoleId = Id,
                BusinessPartnerId = BusinessPartnerId,
                RoleType = RoleType,
                UpdatedBy = updatedBy,
            }
        );
    }

    // ── Validación de coherencia tipo↔config ─────────────────────────────────
    // ÚNICA instancia de condicional sobre RoleType en todo el sistema. Ver ADR-BP-11.
    private static void ValidateConfigCoherence(
        RoleType roleType,
        SupplierRoleConfig? supplierConfig,
        CarrierRoleConfig? carrierConfig,
        CustomerRoleConfig? customerConfig,
        SupplierClassificationConfig? classificationConfig = null
    )
    {
        if (supplierConfig is not null && roleType != RoleType.Supplier)
            throw new ArgumentException(
                $"SupplierConfig solo es válido para el rol Supplier. Rol recibido: {roleType}.",
                nameof(supplierConfig)
            );

        if (carrierConfig is not null && roleType != RoleType.Carrier)
            throw new ArgumentException(
                $"CarrierConfig solo es válido para el rol Carrier. Rol recibido: {roleType}.",
                nameof(carrierConfig)
            );

        if (customerConfig is not null && roleType != RoleType.Customer)
            throw new ArgumentException(
                $"CustomerConfig solo es válido para el rol Customer. Rol recibido: {roleType}.",
                nameof(customerConfig)
            );

        if (classificationConfig is not null && roleType != RoleType.Supplier)
            throw new ArgumentException(
                $"ClassificationConfig solo es válido para el rol Supplier. Rol recibido: {roleType}.",
                nameof(classificationConfig)
            );
    }
}
