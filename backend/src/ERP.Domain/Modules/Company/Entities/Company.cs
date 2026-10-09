using System.Globalization;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Empresa emisora de comprobantes electrónicos (RUC).
/// Un Tenant puede tener N companies (holdings / franquicias).
/// Tablas SRI y documentos electrónicos referencian <c>company_id</c>.
/// </summary>
public class Company : ITenantScopedEntity
{
    public const int CorporateEmailMaxLen = 120;
    public const int WebsiteMaxLen = 200;

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TaxIdentificationNumber { get; set; } = null!;
    public bool IsTemporaryTaxIdentification { get; set; }
    public TaxIdentificationStatus TaxIdentificationStatus { get; set; } =
        TaxIdentificationStatus.Verified;
    public string LegalName { get; set; } = null!;
    public string? TradeName { get; set; }
    public string? CorporateEmail { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string CountryCode { get; set; } = "ECU";

    /// <summary>IANA timezone (e.g. America/Guayaquil).</summary>
    public string Timezone { get; set; } = "America/Guayaquil";

    /// <summary>ISO 4217 currency (e.g. USD).</summary>
    public string CurrencyCode { get; set; } = "USD";
    public string? TaxRegimeCode { get; set; }
    public bool IsAccountingReq { get; set; }
    public string? SpecialTaxpayerNo { get; set; }
    public bool IsForeignTrade { get; set; }
    public bool WithholdsRenta { get; set; } = true;
    public bool WithholdsVat { get; set; } = true;

    public string? ExtraLegend { get; set; }

    /// <summary>Idioma principal de la empresa (es/en/qu).</summary>
    public string LanguageCode { get; set; } = "es";

    // Representante legal
    public string? LegalRepName { get; set; }
    public string? LegalRepPosition { get; set; }
    public string? LegalRepIdNumber { get; set; }
    public string? LegalRepEmail { get; set; }
    public string? LegalRepPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // ── Onboarding & Operational Status ──────────────────────────────────────

    /// <summary>
    /// Histórico del wizard de onboarding (eliminado en la limpieza "ERP pure").
    /// Las companies quedan operativas de inmediato al provisionarse; este campo
    /// se conserva por compatibilidad de esquema/contrato de integración con Platform.
    /// </summary>
    public bool OnboardingCompleted { get; private set; }

    /// <summary>
    /// Operational: empresa lista para operar. Suspended: suspendida por el operador platform.
    /// </summary>
    public CompanyOperationalStatus OperationalStatus { get; private set; } =
        CompanyOperationalStatus.Operational;

    /// <summary>
    /// IL-5A — fecha de corte de la apertura de saldos de la empresa (SSOT): toda carga inicial de
    /// saldos (CxC IL-5, CxP IL-6, contabilidad IL-7) debe usar exactamente esta fecha. Null mientras
    /// la empresa está en implementación y aún no definió su corte.
    /// </summary>
    public DateOnly? OpeningBalanceDate { get; private set; }

    // Navigation
    public SriCountry? Country { get; set; }
    public SriTaxRegime? TaxRegime { get; set; }

    public ICollection<Establishment> Establishments { get; set; } = [];

    // ── Lifecycle methods ─────────────────────────────────────────────────────

    /// <summary>Marks company as fully operational (alias for use after admin re-enables).</summary>
    public void MarkOperational()
    {
        OperationalStatus = CompanyOperationalStatus.Operational;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Suspends company ERP operations (e.g., billing issue, platform action).</summary>
    public void SuspendOperations()
    {
        OperationalStatus = CompanyOperationalStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Define o corrige el corte de apertura de saldos. Se admite mientras la empresa no tenga
    /// operaciones reales y la fecha coincida con las cargas iniciales ya confirmadas (si existen).
    /// Compatibilidad: una empresa que ya opera y tiene apertura confirmada sin fecha definida puede
    /// fijarla una única vez, solo con la fecha real de esa apertura. Repetir la fecha actual es
    /// idempotente.
    /// </summary>
    public void SetOpeningBalanceDate(
        DateOnly openingBalanceDate,
        OpeningBalanceDateConstraints constraints,
        Guid? updatedBy
    )
    {
        if (OpeningBalanceDate == openingBalanceDate)
            return;
        var lockReason = OpeningBalanceDateLockReason(OpeningBalanceDate, constraints);
        if (lockReason is not null)
            throw new DomainRuleViolationException(lockReason);
        if (constraints.ConfirmedOpeningDates.Count > 0
            && !constraints.ConfirmedOpeningDates.Contains(openingBalanceDate))
            throw new DomainRuleViolationException(
                $"La fecha solicitada ({openingBalanceDate:yyyy-MM-dd}) no coincide con las cargas iniciales "
                    + $"confirmadas al {FormatDates(constraints.ConfirmedOpeningDates)}. Para usar otra fecha primero "
                    + "corrija o reabra esa apertura."
            );
        OpeningBalanceDate = openingBalanceDate;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Motivo por el que la fecha de apertura no admite ningún valor nuevo, o <c>null</c> si se puede
    /// definir/corregir (posiblemente restringida a <see cref="OpeningBalanceDateConstraints.ConfirmedOpeningDates"/>).
    /// </summary>
    public static string? OpeningBalanceDateLockReason(
        DateOnly? current,
        OpeningBalanceDateConstraints constraints
    )
    {
        if (constraints.ConfirmedOpeningDates.Count > 1)
            return $"Existen cargas iniciales confirmadas con fechas distintas ({FormatDates(constraints.ConfirmedOpeningDates)}). "
                + "Corrija esas aperturas antes de definir la fecha.";
        if (!constraints.HasRealOperations)
            return null;
        if (current is { } fixedDate)
            return $"La fecha de apertura ({fixedDate:yyyy-MM-dd}) es definitiva: la empresa ya registra operaciones reales.";
        return constraints.ConfirmedOpeningDates.Count == 0
            ? "La empresa ya registra operaciones reales y no tiene ninguna carga inicial confirmada: no se puede definir una fecha de apertura."
            : null;
    }

    private static string FormatDates(IEnumerable<DateOnly> dates) =>
        string.Join(", ", dates.Order().Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    // ── Factory methods ───────────────────────────────────────────────────────

    public static Company CreateFromTenant(
        Guid tenantId,
        string taxIdentificationNumber,
        string legalName,
        string? tradeName = null,
        string? corporateEmail = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD"
    ) =>
        CreateManaged(
            tenantId,
            taxIdentificationNumber,
            legalName,
            tradeName,
            corporateEmail,
            countryCode,
            timezone,
            currencyCode
        );

    public static Company CreateManaged(
        Guid tenantId,
        string taxIdentificationNumber,
        string legalName,
        string? tradeName = null,
        string? corporateEmail = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? website = null,
        bool isTemporaryTaxIdentification = false,
        TaxIdentificationStatus taxIdentificationStatus = TaxIdentificationStatus.Verified,
        Guid? createdBy = null
    )
    {
        var now = DateTime.UtcNow;
        return new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaxIdentificationNumber = NormalizeTaxIdentificationNumber(
                taxIdentificationNumber,
                isTemporaryTaxIdentification
            ),
            IsTemporaryTaxIdentification = isTemporaryTaxIdentification,
            TaxIdentificationStatus = taxIdentificationStatus,
            LegalName = legalName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            CorporateEmail = NormalizeEmail(corporateEmail),
            Website = NormalizeWebsite(website),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? "ECU"
                : countryCode.Trim().ToUpperInvariant(),
            Timezone = string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
                ? "USD"
                : currencyCode.Trim().ToUpperInvariant(),
            IsActive = true,
            OnboardingCompleted = true,
            OperationalStatus = CompanyOperationalStatus.Operational,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
        };
    }

    /// <summary>
    /// Actualiza contacto, representante legal y configuración regional (pestaña "General"
    /// de Company Settings, empresa activa). No toca <see cref="LegalName"/>/<see cref="TradeName"/>/
    /// <see cref="IsActive"/> — esos son propiedad exclusiva de Companies Admin
    /// (<see cref="UpdateAdminIdentity"/>), para evitar el mismo campo editable en dos pantallas.
    /// </summary>
    public void UpdateContactProfile(
        string? corporateEmail,
        string? phone,
        string? website,
        string timezone,
        string currencyCode,
        string? legalRepName,
        string? legalRepPosition,
        string? legalRepIdNumber,
        string? legalRepEmail,
        string? legalRepPhone,
        Guid? updatedBy
    )
    {
        CorporateEmail = NormalizeEmail(corporateEmail);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Website = NormalizeWebsite(website);
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone.Trim();
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
            ? CurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
        LegalRepName = string.IsNullOrWhiteSpace(legalRepName) ? null : legalRepName.Trim();
        LegalRepPosition = string.IsNullOrWhiteSpace(legalRepPosition)
            ? null
            : legalRepPosition.Trim();
        LegalRepIdNumber = string.IsNullOrWhiteSpace(legalRepIdNumber)
            ? null
            : legalRepIdNumber.Trim();
        LegalRepEmail = NormalizeEmail(legalRepEmail);
        LegalRepPhone = string.IsNullOrWhiteSpace(legalRepPhone) ? null : legalRepPhone.Trim();
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Actualiza identidad administrativa (Companies Admin: <c>/companies</c>): razón social,
    /// nombre comercial y estado activo/inactivo. No toca contacto/marca/locale/fiscal —
    /// esos son propiedad exclusiva de Company Settings.
    /// </summary>
    public void UpdateAdminIdentity(
        string legalName,
        string? tradeName,
        bool isActive,
        Guid? updatedBy
    )
    {
        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateTaxIdentification(
        string taxIdentificationNumber,
        bool isTemporary,
        TaxIdentificationStatus status,
        Guid? updatedBy = null
    )
    {
        TaxIdentificationNumber = NormalizeTaxIdentificationNumber(
            taxIdentificationNumber,
            isTemporary
        );
        IsTemporaryTaxIdentification = isTemporary;
        TaxIdentificationStatus = status;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy ?? UpdatedBy;
    }

    /// <summary>Actualiza la configuración fiscal (pestaña "Fiscal" de Configuración → Empresa).</summary>
    public void UpdateFiscalSettings(
        string? taxRegimeCode,
        bool isAccountingReq,
        string? specialTaxpayerNo,
        bool isForeignTrade,
        bool withholdsRenta,
        bool withholdsVat,
        Guid? updatedBy
    )
    {
        TaxRegimeCode = string.IsNullOrWhiteSpace(taxRegimeCode)
            ? null
            : taxRegimeCode.Trim().ToUpperInvariant();
        IsAccountingReq = isAccountingReq;
        SpecialTaxpayerNo = string.IsNullOrWhiteSpace(specialTaxpayerNo)
            ? null
            : specialTaxpayerNo.Trim();
        IsForeignTrade = isForeignTrade;
        WithholdsRenta = withholdsRenta;
        WithholdsVat = withholdsVat;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Actualiza el idioma principal (pestaña "Operación" de Configuración → Empresa).</summary>
    public void UpdateOperationSettings(string languageCode, Guid? updatedBy)
    {
        LanguageCode = string.IsNullOrWhiteSpace(languageCode)
            ? LanguageCode
            : languageCode.Trim().ToLowerInvariant();
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Actualiza las notas legales institucionales (pestaña "Documentos" de Configuración → Empresa).</summary>
    public void UpdateDocumentsSettings(string? extraLegend, Guid? updatedBy)
    {
        ExtraLegend = string.IsNullOrWhiteSpace(extraLegend) ? null : extraLegend.Trim();
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    private static string NormalizeTaxIdentificationNumber(
        string taxIdentificationNumber,
        bool isTemporary
    )
    {
        var t = taxIdentificationNumber.Trim();
        if (isTemporary)
            return t;
        if (t.Length == 13)
            return t;
        if (t.Length < 13)
            return t.PadRight(13, '0')[..13];
        return t[..13];
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e))
            return null;
        if (e.Length > CorporateEmailMaxLen)
            throw new ArgumentException(
                $"El email no puede superar {CorporateEmailMaxLen} caracteres.",
                nameof(email)
            );
        if (!e.Contains('@') || e.IndexOf('.', e.IndexOf('@')) < 0)
            throw new ArgumentException("Formato de email inválido.", nameof(email));
        return e;
    }

    private static string? NormalizeWebsite(string? website)
    {
        var w = website?.Trim();
        if (string.IsNullOrEmpty(w))
            return null;
        if (w.Length > WebsiteMaxLen)
            throw new ArgumentException(
                $"El sitio web no puede superar {WebsiteMaxLen} caracteres.",
                nameof(website)
            );
        return w;
    }
}
