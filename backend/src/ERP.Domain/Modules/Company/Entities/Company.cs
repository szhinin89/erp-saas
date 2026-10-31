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

    // ── Designaciones del emisor por resolución SRI (Ficha Técnica 2.34) ─────────────────────
    // Independientes de WithholdsRenta/WithholdsVat (práctica operativa de retener) y de
    // SpecialTaxpayerNo (contribuyente especial): ninguna se deriva de otra. Número y vigencia van
    // juntos (ambos nulos = no designada); la aplicabilidad se evalúa contra la fecha de emisión.

    /// <summary>Anexo 21 — número de la resolución de designación como agente de retención, sin
    /// ceros a la izquierda (<c>agenteRetencion</c>: numérico, máximo 8).</summary>
    public string? RetentionAgentResolutionNo { get; private set; }

    /// <summary>Anexo 21 — fecha de emisión desde la que rige la designación.</summary>
    public DateOnly? RetentionAgentEffectiveFrom { get; private set; }

    /// <summary>Anexo 24 — resolución de calificación como Gran Contribuyente, tal como la emitió
    /// el SRI (valor del <c>campoAdicional</c> "Gran Contribuyente", máximo 300).</summary>
    public string? LargeTaxpayerResolutionNo { get; private set; }

    /// <summary>Anexo 24 — fecha de emisión desde la que rige la calificación.</summary>
    public DateOnly? LargeTaxpayerEffectiveFrom { get; private set; }

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

    /// <summary>
    /// IL-8E — instante (UTC) del cierre definitivo de la Carga Inicial de saldos. Null mientras la
    /// implementación sigue abierta; una vez asignado nunca se limpia (irreversible desde el ERP).
    /// </summary>
    public DateTime? InitialLoadClosedAt { get; private set; }

    /// <summary>IL-8E — usuario que cerró la Carga Inicial (null mientras siga abierta).</summary>
    public Guid? InitialLoadClosedBy { get; private set; }

    public bool IsInitialLoadClosed => InitialLoadClosedAt is not null;

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
        var lockReason = OpeningBalanceDateLockReason(OpeningBalanceDate, constraints, IsInitialLoadClosed);
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
        OpeningBalanceDateConstraints constraints,
        bool initialLoadClosed = false
    )
    {
        if (initialLoadClosed)
            return InitialLoadClosedMessage;
        if (constraints.HasPostedOpeningJournalEntry && current is { } postedDate)
            return $"La fecha de apertura ({postedDate:yyyy-MM-dd}) es definitiva: el asiento de apertura (ASI) ya está publicado.";
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

    /// <summary>IL-8E — motivo único de rechazo de cualquier cambio a la apertura tras el cierre.</summary>
    public const string InitialLoadClosedMessage =
        "La carga inicial de la empresa está cerrada definitivamente: la apertura (fecha, lotes de saldos, "
        + "contabilización y asiento de apertura) ya no se modifica. Las correcciones se registran con los "
        + "procesos contables u operativos normales.";

    /// <summary>
    /// IL-8E — cierre definitivo de la Carga Inicial. Idempotente (un segundo cierre conserva el
    /// primero) e irreversible: no existe método para reabrir. Las condiciones (conciliación sin
    /// blockers) las verifica el caso de uso bajo el bloqueo de la apertura.
    /// </summary>
    public void CloseInitialLoad(Guid closedBy)
    {
        if (closedBy == Guid.Empty)
            throw new ArgumentException("El usuario que cierra es obligatorio.", nameof(closedBy));
        if (IsInitialLoadClosed)
            return;
        InitialLoadClosedAt = DateTime.UtcNow;
        InitialLoadClosedBy = closedBy;
        UpdatedBy = closedBy;
        UpdatedAt = DateTime.UtcNow;
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
        var normalizedSpecialTaxpayerNo = string.IsNullOrWhiteSpace(specialTaxpayerNo)
            ? null
            : specialTaxpayerNo.Trim();
        if (
            normalizedSpecialTaxpayerNo is not null
            && !IsValidSpecialTaxpayerNo(normalizedSpecialTaxpayerNo)
        )
            throw new DomainRuleViolationException(SpecialTaxpayerNoFormatMessage);

        TaxRegimeCode = string.IsNullOrWhiteSpace(taxRegimeCode)
            ? null
            : taxRegimeCode.Trim().ToUpperInvariant();
        IsAccountingReq = isAccountingReq;
        SpecialTaxpayerNo = normalizedSpecialTaxpayerNo;
        IsForeignTrade = isForeignTrade;
        WithholdsRenta = withholdsRenta;
        WithholdsVat = withholdsVat;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    // ── Formatos exigidos por la Ficha Técnica SRI 2.34 y los XSD oficiales embebidos ─────────

    /// <summary>XSD <c>contribuyenteEspecial</c> (Factura 1.1.0, NC 1.1.0, Retención 1.0.0).</summary>
    public const int SpecialTaxpayerNoMinLength = 3;

    /// <inheritdoc cref="SpecialTaxpayerNoMinLength"/>
    public const int SpecialTaxpayerNoMaxLength = 13;

    /// <summary>Anexo 21 / XSD <c>agenteRetencion</c>: numérico, máximo 8.</summary>
    public const int RetentionAgentResolutionNoMaxLength = 8;

    /// <summary>Anexo 24 / XSD <c>campoAdicional</c>: máximo 300.</summary>
    public const int LargeTaxpayerResolutionNoMaxLength = 300;

    public const string SpecialTaxpayerNoFormatMessage =
        "El número de contribuyente especial debe tener entre 3 y 13 caracteres alfanuméricos, sin espacios ni símbolos.";

    public const string RetentionAgentDesignationMessage =
        "La designación como agente de retención requiere el número de resolución (solo dígitos, máximo 8, sin ceros a la izquierda) y la fecha desde la que rige.";

    public const string LargeTaxpayerDesignationMessage =
        "La calificación como Gran Contribuyente requiere el número de resolución (máximo 300 caracteres) y la fecha desde la que rige.";

    public static bool IsValidSpecialTaxpayerNo(string? value) =>
        value is not null
        && value.Length is >= SpecialTaxpayerNoMinLength and <= SpecialTaxpayerNoMaxLength
        && value.All(char.IsAsciiLetterOrDigit);

    /// <summary>Forma final del XML: dígitos, sin ceros a la izquierda, entre 1 y 8.</summary>
    public static bool IsValidRetentionAgentResolutionNo(string? value) =>
        value is not null
        && value.Length is >= 1 and <= RetentionAgentResolutionNoMaxLength
        && value[0] != '0'
        && value.All(char.IsAsciiDigit);

    public static bool IsValidLargeTaxpayerResolutionNo(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= LargeTaxpayerResolutionNoMaxLength
        && value == value.Trim();

    /// <summary>Par número + vigencia coherente: ambos nulos, o número válido con fecha.</summary>
    public bool HasValidRetentionAgentDesignation =>
        RetentionAgentResolutionNo is null
            ? RetentionAgentEffectiveFrom is null
            : RetentionAgentEffectiveFrom is not null
                && IsValidRetentionAgentResolutionNo(RetentionAgentResolutionNo);

    /// <inheritdoc cref="HasValidRetentionAgentDesignation"/>
    public bool HasValidLargeTaxpayerDesignation =>
        LargeTaxpayerResolutionNo is null
            ? LargeTaxpayerEffectiveFrom is null
            : LargeTaxpayerEffectiveFrom is not null
                && IsValidLargeTaxpayerResolutionNo(LargeTaxpayerResolutionNo);

    /// <summary>
    /// Registra o retira las designaciones por resolución (pestaña "Fiscal"). Cada par es todo o
    /// nada: número y vigencia juntos, o ninguno. El número de agente de retención se guarda en la
    /// forma exigida por el Anexo 21 ("omitiendo los ceros a la izquierda"); la resolución de Gran
    /// Contribuyente, tal cual (sin espacios externos). Nunca se infiere de otros datos fiscales.
    /// </summary>
    public void UpdateFiscalDesignations(
        string? retentionAgentResolutionNo,
        DateOnly? retentionAgentEffectiveFrom,
        string? largeTaxpayerResolutionNo,
        DateOnly? largeTaxpayerEffectiveFrom,
        Guid? updatedBy
    )
    {
        var retentionAgentNo = NormalizeRetentionAgentResolutionNo(retentionAgentResolutionNo);
        if (
            (retentionAgentNo is null) != (retentionAgentEffectiveFrom is null)
            || (retentionAgentNo is not null && !IsValidRetentionAgentResolutionNo(retentionAgentNo))
        )
            throw new DomainRuleViolationException(RetentionAgentDesignationMessage);

        var largeTaxpayerNo = string.IsNullOrWhiteSpace(largeTaxpayerResolutionNo)
            ? null
            : largeTaxpayerResolutionNo.Trim();
        if (
            (largeTaxpayerNo is null) != (largeTaxpayerEffectiveFrom is null)
            || (largeTaxpayerNo is not null && !IsValidLargeTaxpayerResolutionNo(largeTaxpayerNo))
        )
            throw new DomainRuleViolationException(LargeTaxpayerDesignationMessage);

        RetentionAgentResolutionNo = retentionAgentNo;
        RetentionAgentEffectiveFrom = retentionAgentEffectiveFrom;
        LargeTaxpayerResolutionNo = largeTaxpayerNo;
        LargeTaxpayerEffectiveFrom = largeTaxpayerEffectiveFrom;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Anexo 21 — resolución aplicable a un comprobante con esa fecha de emisión de
    /// negocio (ADR-034); <c>null</c> si no está designada o la designación rige después.</summary>
    public string? RetentionAgentResolutionFor(DateOnly issueDate) =>
        RetentionAgentResolutionNo is not null && RetentionAgentEffectiveFrom <= issueDate
            ? RetentionAgentResolutionNo
            : null;

    /// <summary>Anexo 24 — mismo criterio que <see cref="RetentionAgentResolutionFor"/>.</summary>
    public string? LargeTaxpayerResolutionFor(DateOnly issueDate) =>
        LargeTaxpayerResolutionNo is not null && LargeTaxpayerEffectiveFrom <= issueDate
            ? LargeTaxpayerResolutionNo
            : null;

    /// <summary>Anexo 21: "omitiendo los ceros a la izquierda". Vacío → <c>null</c>.</summary>
    public static string? NormalizeRetentionAgentResolutionNo(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (!trimmed.All(char.IsAsciiDigit))
            return trimmed;
        var withoutLeadingZeros = trimmed.TrimStart('0');
        // "0", "000": no es un número de resolución; se conserva para que la validación lo rechace.
        return withoutLeadingZeros.Length == 0 ? trimmed : withoutLeadingZeros;
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
