using ERP.Domain.MasterData.Enums;

namespace ERP.API.Contracts.MasterData;

// ── BusinessPartner Identity ──────────────────────────────────────────────────

/// <summary>Crea la identidad fiscal del tercero. Roles y contactos se agregan después.</summary>
public sealed class CreateBusinessPartnerRequest
{
    /// <summary>Código SRI: 04=RUC, 05=CI, 06=Pasaporte, 07=ConsumidorFinal, 08=Exterior, 09=Placa</summary>
    public string IdentificationType { get; set; } = "";
    public string IdentificationNumber { get; set; } = "";

    /// <summary>1=Natural, 2=Legal, 3=Government, 4=Organization</summary>
    public PersonType PersonType { get; set; } = PersonType.Natural;
    public string LegalName { get; set; } = "";
    public string? TradeName { get; set; }

    /// <summary>ISO 3166-1 alpha-2. Ej: EC, US, CO</summary>
    public string? CountryCode { get; set; }
}

/// <summary>Actualiza perfil de identidad. No incluye la identificación fiscal.</summary>
public sealed class UpdateBusinessPartnerRequest
{
    public string LegalName { get; set; } = "";
    public PersonType PersonType { get; set; } = PersonType.Natural;
    public string? TradeName { get; set; }
    public string? CountryCode { get; set; }
}

/// <summary>Cambia la identificación fiscal. Operación de alto impacto — genera evento de auditoría.</summary>
public sealed class UpdateIdentificationRequest
{
    public string IdentificationType { get; set; } = "";
    public string IdentificationNumber { get; set; } = "";
}

// ── BusinessPartner Roles ─────────────────────────────────────────────────────

/// <summary>
/// Asigna un rol al BP. Semántica UPSERT: si ya estaba revocado se reactiva;
/// si ya está activo se devuelve error 422.
/// </summary>
public sealed class AssignRoleRequest
{
    /// <summary>Customer=1, Supplier=2, Employee=3, Carrier=4, Broker=5, Agent=6, Distributor=7, Contractor=8</summary>
    public RoleType RoleType { get; set; }
    public SupplierConfigRequest? SupplierConfig { get; set; }
    public SupplierClassificationRequest? SupplierClassification { get; set; }
    public CarrierConfigRequest? CarrierConfig { get; set; }
    public CustomerConfigRequest? CustomerConfig { get; set; }
}

/// <summary>Config SRI operativa del proveedor (pre-llenado para documentos de compra).</summary>
public sealed class SupplierConfigRequest
{
    /// <summary>Condición de pago obligatoria — FK a master_payment_terms.</summary>
    public Guid PaymentTermId { get; set; }

    /// <summary>Código sustento tributario SRI (01-19). Ej: "01" = Crédito Tributario.</summary>
    public string? DefaultTaxSupportCode { get; set; }

    /// <summary>Código retención IVA SRI. Ej: "725".</summary>
    public string? DefaultRetentionVatCode { get; set; }

    /// <summary>Código retención renta SRI. Ej: "303".</summary>
    public string? DefaultRetentionIncomeCode { get; set; }

    /// <summary>Método de pago SRI por defecto (01-21). Ej: "01" = Sin sistema financiero.</summary>
    public string? DefaultPaymentMethodCode { get; set; }

    /// <summary>Tipo Proveedor de Reembolso (global.sri_supplier_type: 01=Persona Natural, 02=Sociedad).</summary>
    public string? RefundProviderTypeCode { get; set; }

    /// <summary>Proveedor exento de retención (RISE, microempresa, sector público). Default: false.</summary>
    public bool IsRetentionExempt { get; set; }
}

/// <summary>Datos del transportista para guías de remisión electrónicas SRI.</summary>
public sealed class CarrierConfigRequest
{
    public string? TransportAuthorizationNumber { get; set; }
    public decimal? VehicleCapacityTons { get; set; }
}

public sealed class UpdateRoleNotesRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// Clasificación y scoring del cliente. CRM-ready.
/// Todos los campos son opcionales (null = sin cambio semántico para el campo).
/// </summary>
public sealed class CustomerConfigRequest
{
    /// <summary>Retail | Wholesale | Corporate | Government | VIP | Other</summary>
    public string? CustomerCategory { get; set; }

    /// <summary>Individual | SMB | MidMarket | Enterprise | StartUp</summary>
    public string? CustomerSegment { get; set; }

    /// <summary>Territorio libre: "Norte", "Sur", código de zona, etc.</summary>
    public string? SalesZone { get; set; }

    /// <summary>AAA | AA | A | BBB | BB | B | C | D | NR (no reemplaza CreditLimit)</summary>
    public string? CreditRating { get; set; }

    /// <summary>None | Bronze | Silver | Gold | Platinum</summary>
    public string? LoyaltyTier { get; set; }

    /// <summary>PDF | XML | EMAIL | PORTAL | PAPER</summary>
    public string? PreferredInvoiceFormat { get; set; }

    /// <summary>Nacional | Exportador | Importador | Exento | Especial</summary>
    public string? CustomerClassification { get; set; }
}

// ── BusinessPartner Locations ─────────────────────────────────────────────────

/// <summary>
/// Registra una ubicación física del BP. Tenant-scoped (compartida entre companies).
/// Si OtherDescription es null y Type=Other se devuelve error 422.
/// </summary>
public sealed class CreateLocationRequest
{
    public string Name { get; set; } = "";

    /// <summary>Matrix=1, Branch=2, Office=3, Warehouse=4, DeliveryPoint=5, Other=99</summary>
    public LocationType Type { get; set; }

    /// <summary>Bitmask: None=0, Billing=1, Delivery=2, Fiscal=4, Correspondence=8</summary>
    public LocationPurpose Purpose { get; set; } = LocationPurpose.None;
    public string AddressLine { get; set; } = "";

    /// <summary>Código INEC provincia (2 dígitos). Ej: "17" = Pichincha.</summary>
    public string? ProvinceCode { get; set; }

    /// <summary>Código INEC cantón (4 dígitos). Requiere ProvinceCode.</summary>
    public string? CantonCode { get; set; }

    /// <summary>Código INEC parroquia (6 dígitos). Requiere CantonCode.</summary>
    public string? ParishCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>Obligatorio si Type=Other.</summary>
    public string? OtherDescription { get; set; }
}

public sealed class UpdateLocationRequest
{
    public string Name { get; set; } = "";
    public LocationType Type { get; set; }
    public LocationPurpose Purpose { get; set; } = LocationPurpose.None;
    public string AddressLine { get; set; } = "";
    public string? ProvinceCode { get; set; }
    public string? CantonCode { get; set; }
    public string? ParishCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? OtherDescription { get; set; }
}

// ── BusinessPartner Contacts ──────────────────────────────────────────────────

public sealed class CreateContactRequest
{
    public string FirstName { get; set; } = "";

    /// <summary>Commercial=1, Accounting=2, Management=3, Reception=4, Dispatch=5, Billing=6, Technical=7, Purchasing=8, Legal=9, Other=99</summary>
    public ContactRole Role { get; set; }
    public Guid? LocationId { get; set; }
    public string? LastName { get; set; }
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>Obligatorio si Role=Other.</summary>
    public string? OtherDescription { get; set; }
}

public sealed class UpdateContactRequest
{
    public string FirstName { get; set; } = "";
    public ContactRole Role { get; set; }
    public Guid? LocationId { get; set; }
    public string? LastName { get; set; }
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string? OtherDescription { get; set; }
}

// ── CompanyBpTradingSettings ──────────────────────────────────────────────────

/// <summary>Configura condiciones comerciales del BP en la empresa activa. Crea o actualiza.</summary>
public sealed class UpsertTradingSettingsRequest
{
    /// <summary>Límite de crédito. 0 = sin crédito. Mínimo: 0.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Plazo de pago en días. 0 = contado. Mínimo: 0.</summary>
    public int PaymentDays { get; set; }

    /// <summary>ISO 4217. Default: USD.</summary>
    public string CreditCurrencyCode { get; set; } = "USD";
}

/// <summary>Clasificación estratégica del proveedor (S3-B).</summary>
public sealed class SupplierClassificationRequest
{
    /// <summary>Manufacturer | Distributor | ServiceProvider | Agent | Retailer | Other</summary>
    public string? SupplierCategory { get; set; }

    /// <summary>National | International | Both</summary>
    public string? SupplierType { get; set; }

    /// <summary>Low | Medium | High | Critical</summary>
    public string? SupplierRisk { get; set; }

    /// <summary>AAA | AA | A | BBB | BB | B | C | D | NR</summary>
    public string? SupplierRating { get; set; }

    /// <summary>Goods | Services | Both | Digital</summary>
    public string? PrimaryGoodType { get; set; }

    /// <summary>Strategic | Preferred | Approved | Transactional</summary>
    public string? SupplierSegment { get; set; }

    /// <summary>Texto libre — método de pago operativo interno (distinto del código SRI).</summary>
    public string? PaymentMethodPreference { get; set; }
}

/// <summary>Bloquea operativamente al BP en la empresa activa. Requiere motivo.</summary>
public sealed class BlockRequest
{
    /// <summary>Motivo del bloqueo. Obligatorio. Máximo 500 caracteres.</summary>
    public string Reason { get; set; } = "";
}
