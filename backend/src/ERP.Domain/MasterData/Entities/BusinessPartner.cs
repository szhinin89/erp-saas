using ERP.Domain.Common;
using ERP.Domain.MasterData.Events;
using ERP.Domain.MasterData.ValueObjects;

namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Aggregate Root: identidad fiscal de un tercero dentro del tenant.
///
/// SCOPE: ITenantScopedEntity — compartido entre todas las Companies del tenant.
///
/// CONTIENE: TaxIdentification, PersonName, LegalEntityTypeCode, CountryCode, IsActive.
/// NO CONTIENE: Email, Phone, LegalRepresentativeName (→ BusinessPartnerContact),
///              roles (→ BusinessPartnerRole AR independiente),
///              condiciones comerciales (→ CompanyBpTradingSettings).
///
/// EXTENSIÓN DE ROLES: ver BusinessPartnerRole. PROHIBIDO agregar IsCustomer/IsSupplier aquí.
/// </summary>
public sealed class BusinessPartner : AuditableEntity, ITenantScopedEntity, ISystemSeeded
{
    public const int CountryCodeLen = 2;

    public TaxIdentification Identification { get; private set; } = null!;
    public PersonName Name { get; private set; } = null!;
    public int LegalEntityTypeCode { get; private set; }
    public string? CountryCode { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Ver <see cref="ISystemSeeded"/>. <see cref="BusinessPartner"/> no deriva de
    /// <c>MasterEntity</c> (aggregate root de identidad fiscal, no un catálogo simple), por lo que
    /// implementa el flag directamente en vez de heredarlo.
    /// </summary>
    public bool IsSystemSeeded { get; private set; }

    private BusinessPartner() { }

    public static BusinessPartner Create(
        Guid tenantId,
        string identificationType,
        string identificationNumber,
        int legalEntityTypeCode,
        string legalName,
        Guid createdBy,
        string? tradeName = null,
        string? countryCode = null
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy es obligatorio.", nameof(createdBy));

        var identification = TaxIdentification.Create(
             identificationType,
             identificationNumber
         );

        identification.ValidateLegalEntityCompatibility(
            legalEntityTypeCode
        );

        var bp = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Identification = identification,
            Name = PersonName.Create(legalName, tradeName),
            LegalEntityTypeCode = legalEntityTypeCode,
            CountryCode = NormalizeCountryCode(countryCode),
            IsActive = true,
        };
        bp.SetCreated(createdBy);
        bp.RaiseDomainEvent(
            new BusinessPartnerCreatedEvent
            {
                TenantId = tenantId,
                BusinessPartnerId = bp.Id,
                IdentificationType = bp.Identification.Type,
                IdentificationNumber = bp.Identification.Number,
                LegalName = bp.Name.LegalName,
                CreatedBy = createdBy,
            }
        );
        return bp;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="IsSystemSeeded"/>), bloqueando
    /// <see cref="UpdateIdentification"/> y <see cref="Deactivate"/> — Tipo A (Consumidor Final: RUC
    /// genérico mandatado por el SRI, no un dato de negocio editable), ver política de Bootstrap en
    /// <c>CLAUDE.md</c>. <see cref="UpdateProfile"/> permanece abierto (nombre/país son cosméticos).
    /// </summary>
    public static BusinessPartner CreateSystemSeeded(
        Guid tenantId,
        string identificationType,
        string identificationNumber,
        int legalEntityTypeCode,
        string legalName,
        Guid createdBy,
        string? tradeName = null,
        string? countryCode = null
    )
    {
        var bp = Create(
            tenantId,
            identificationType,
            identificationNumber,
            legalEntityTypeCode,
            legalName,
            createdBy,
            tradeName,
            countryCode
        );
        bp.IsSystemSeeded = true;
        return bp;
    }

    /// <summary>
    /// Actualiza nombre legal/comercial, naturaleza jurídica y país.
    /// No modifica la identificación fiscal — use UpdateIdentification() para eso.
    /// </summary>
    public void UpdateProfile(
        string legalName,
        int legalEntityTypeCode,
        Guid updatedBy,
        string? tradeName = null,
        string? countryCode = null
    )
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede actualizar un BusinessPartner inactivo."
            );

        Name = PersonName.Create(legalName, tradeName);
        LegalEntityTypeCode = legalEntityTypeCode;
        CountryCode = NormalizeCountryCode(countryCode);
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerProfileUpdatedEvent
            {
                TenantId = TenantId,
                BusinessPartnerId = Id,
                LegalName = Name.LegalName,
                UpdatedBy = updatedBy,
            }
        );
    }

    /// <summary>
    /// Cambia la identificación fiscal. Operación de alto impacto: emite evento de auditoría.
    /// ATENCIÓN: cuando el módulo de documentos exista, verificar que no haya documentos
    /// en estados no-finales antes de permitir este cambio. Ver ADR-BP-14.
    /// </summary>
    public void UpdateIdentification(string type, string number, Guid updatedBy)
    {
        this.EnsureEditable("La identificación tributaria de este tercero", "modificarse");
        if (!IsActive)
            throw new InvalidOperationException(
                "No se puede modificar la identificación de un BusinessPartner inactivo."
            );

        var oldType = Identification.Type;
        var oldNumber = Identification.Number;

        Identification = TaxIdentification.Create(type, number);
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerIdentificationChangedEvent
            {
                TenantId = TenantId,
                BusinessPartnerId = Id,
                OldType = oldType,
                OldNumber = oldNumber,
                NewType = Identification.Type,
                NewNumber = Identification.Number,
                ChangedBy = updatedBy,
            }
        );
    }

    public void Deactivate(Guid updatedBy)
    {
        this.EnsureEditable("Este tercero", "desactivarse");
        if (!IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está inactivo.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerDeactivatedEvent
            {
                TenantId = TenantId,
                BusinessPartnerId = Id,
                DeactivatedBy = updatedBy,
            }
        );
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("El BusinessPartner ya está activo.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new BusinessPartnerActivatedEvent
            {
                TenantId = TenantId,
                BusinessPartnerId = Id,
                ActivatedBy = updatedBy,
            }
        );
    }

    private static string? NormalizeCountryCode(string? code)
    {
        var c = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(c))
            return null;
        if (c.Length != CountryCodeLen)
            throw new ArgumentException(
                $"CountryCode debe ser un código ISO 3166-1 alpha-2 de {CountryCodeLen} caracteres.",
                nameof(code)
            );
        return c;
    }
}
