using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>Lo que distingue a un rol importable (Cliente/Proveedor) en mensajes y catálogos.</summary>
public sealed record PartnerImportRole(
    RoleType Role,
    IdentificationUsageType Usage,
    string RoleName,
    string Singular,
    string Plural,
    string ContactPurpose
)
{
    public static readonly PartnerImportRole Customer =
        new(RoleType.Customer, IdentificationUsageType.Customer, "Cliente", "cliente", "clientes", "facturación");

    public static readonly PartnerImportRole Supplier =
        new(RoleType.Supplier, IdentificationUsageType.Supplier, "Proveedor", "proveedor", "proveedores", "compras");
}

/// <summary>Columnas comunes ya validadas y la clasificación de la fila contra el maestro.</summary>
public sealed record PartnerRowValidation(
    string IdentificationType,
    string IdentificationNumber,
    int? LegalEntityTypeCode,
    string LegalName,
    string? TradeName,
    string? CountryCode,
    string? Email,
    string? Phone,
    Guid? PaymentTermId,
    PartnerImportAction Action,
    Guid? ExistingBusinessPartnerId,
    BusinessPartnerImportMatch? Match = null
);

/// <summary>
/// Reglas compartidas de la carga inicial de terceros (IL-2A Clientes, IL-3A Proveedores):
/// Validate aplica las mismas reglas de dominio que Confirm (identificación SRI según el catálogo de
/// uso del rol, naturaleza jurídica, contacto, condición de pago por Company) y clasifica cada fila
/// contra el maestro del tenant — nuevo → crear; sin el rol → asignar; ya tiene el rol →
/// idempotente; nunca se duplica un BP. Confirm revalida esa clasificación (stale preview).
/// Una instancia por processor (cachea las condiciones de pago del tenant durante el lote).
/// </summary>
public sealed class BusinessPartnerImportRules
{
    private readonly IBusinessPartnerImportLookup _lookup;
    private readonly IPaymentTermRepository _paymentTermRepo;
    private readonly ILegalEntityTypeRepository _legalEntityTypeRepo;
    private readonly IIdentificationUsageValidator _usageValidator;
    private readonly IOperationalContext _ctx;
    private readonly PartnerImportRole _role;
    private IReadOnlyList<PaymentTerm>? _paymentTerms;

    public BusinessPartnerImportRules(
        IBusinessPartnerImportLookup lookup,
        IPaymentTermRepository paymentTermRepo,
        ILegalEntityTypeRepository legalEntityTypeRepo,
        IIdentificationUsageValidator usageValidator,
        IOperationalContext ctx,
        PartnerImportRole role
    )
    {
        _lookup = lookup;
        _paymentTermRepo = paymentTermRepo;
        _legalEntityTypeRepo = legalEntityTypeRepo;
        _usageValidator = usageValidator;
        _ctx = ctx;
        _role = role;
    }

    public async Task<PartnerRowValidation> ValidateAsync(
        IReadOnlyDictionary<string, string?> rawRow, List<RowIssue> issues, CancellationToken ct)
    {
        void Error(string code, string message, string field) =>
            issues.Add(new RowIssue(ImportSeverity.Error, code, message, field));

        var identificationType = Get(rawRow, PartnerImportColumns.IdentificationType) ?? string.Empty;
        var number = Get(rawRow, PartnerImportColumns.IdentificationNumber) ?? string.Empty;
        var legalEntityRaw = Get(rawRow, PartnerImportColumns.LegalEntityTypeCode);
        var legalName = Get(rawRow, PartnerImportColumns.LegalName) ?? string.Empty;
        var tradeName = Get(rawRow, PartnerImportColumns.TradeName);
        var countryCode = Get(rawRow, PartnerImportColumns.CountryCode)?.ToUpperInvariant();
        var email = Get(rawRow, PartnerImportColumns.Email);
        var phone = Get(rawRow, PartnerImportColumns.Phone);
        var paymentTermCode = Get(rawRow, PartnerImportColumns.PaymentTermCode);

        if (identificationType.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "El tipo de identificación es obligatorio.",
                PartnerImportColumns.IdentificationType);
        if (number.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "El número de identificación es obligatorio.",
                PartnerImportColumns.IdentificationNumber);
        if (legalName.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "La razón social es obligatoria.", PartnerImportColumns.LegalName);
        else if (legalName.Length < 2 || legalName.Length > PersonName.LegalNameMaxLen)
            Error("INVALID_LEGAL_NAME",
                $"La razón social debe tener entre 2 y {PersonName.LegalNameMaxLen} caracteres.",
                PartnerImportColumns.LegalName);
        if (tradeName is { Length: > PersonName.TradeNameMaxLen })
            Error("INVALID_TRADE_NAME",
                $"El nombre comercial no puede superar {PersonName.TradeNameMaxLen} caracteres.",
                PartnerImportColumns.TradeName);
        if (countryCode is not null && (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter)))
            Error("INVALID_COUNTRY", "País debe ser un código ISO de 2 letras (p. ej. EC).",
                PartnerImportColumns.CountryCode);

        TaxIdentification? identification = null;
        if (identificationType.Length > 0 && number.Length > 0)
            identification = await ValidateIdentificationAsync(identificationType, number, Error, ct);

        int? legalEntityTypeCode = null;
        if (legalEntityRaw is not null)
        {
            if (int.TryParse(legalEntityRaw, out var parsedCode) && parsedCode > 0)
                legalEntityTypeCode = parsedCode;
            else
                Error("INVALID_LEGAL_ENTITY_TYPE", "Tipo Entidad Legal debe ser un código numérico del catálogo.",
                    PartnerImportColumns.LegalEntityTypeCode);
        }

        if (email is not null || phone is not null)
        {
            try
            {
                ContactInfo.Create(phone, null, email);
            }
            catch (ArgumentException ex)
            {
                Error("INVALID_CONTACT", PublicMessage(ex),
                    email is not null ? PartnerImportColumns.Email : PartnerImportColumns.Phone);
            }
        }

        var paymentTermId = await ResolvePaymentTermAsync(paymentTermCode, Error, ct);

        var action = PartnerImportAction.Create;
        Guid? existingId = null;
        BusinessPartnerImportMatch? existing = null;
        if (identification is not null)
        {
            var match = await _lookup.FindByIdentificationAsync(identification.Type, identification.Number, _role.Role, ct);
            if (match is null)
            {
                await ValidateNewPartnerAsync(identification, legalEntityRaw, legalEntityTypeCode, legalName,
                    email, phone, Error, ct);
            }
            else
            {
                existing = match;
                existingId = match.BusinessPartnerId;
                number = match.IdentificationNumber;
                action = ClassifyExisting(match, paymentTermId, Error);
                if (!match.IsAmbiguous && match.IsActive)
                    issues.Add(new RowIssue(ImportSeverity.Warning, "EXISTING_BUSINESS_PARTNER",
                        $"El tercero ya existe ({match.LegalName}): se conserva su ficha maestra; Razón Social, "
                        + "Nombre Comercial, Tipo Entidad Legal, País y contacto del archivo no se aplican.",
                        PartnerImportColumns.IdentificationNumber));
            }
        }

        return new PartnerRowValidation(identificationType, number, legalEntityTypeCode, legalName, tradeName,
            countryCode, email, phone, paymentTermId, action, existingId, existing);
    }

    /// <summary>Marca como error toda fila cuya identificación (sin distinguir mayúsculas) se repite.</summary>
    public static IReadOnlyList<RowValidationResult> FlagDuplicateIdentifications(
        IReadOnlyList<RowValidationResult> rows, Func<string, (string Type, string Number)> identificationOf)
    {
        var issues = rows.Select(r => r.Issues.ToList()).ToList();
        var duplicates = rows
            .Select((r, i) => (Id: identificationOf(r.ParsedDataJson), Index: i))
            .Where(e => e.Id.Type.Length > 0 && e.Id.Number.Length > 0)
            .GroupBy(e => e.Id.Type + ":" + e.Id.Number.ToUpperInvariant())
            .Where(g => g.Count() > 1);
        foreach (var group in duplicates)
            foreach (var entry in group)
                issues[entry.Index].Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_IDENTIFICATION_IN_FILE",
                    "La identificación se repite en otras filas del archivo.",
                    PartnerImportColumns.IdentificationNumber));

        return rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();
    }

    /// <summary>
    /// Revalida al confirmar lo que Validate clasificó (stale preview). Devuelve el motivo de
    /// rechazo — o <c>null</c> — y el estado actual del maestro.
    /// </summary>
    public async Task<(string? Error, BusinessPartnerImportMatch? Match)> RevalidateAsync(
        string identificationType, string identificationNumber, PartnerImportAction action,
        Guid? existingBusinessPartnerId, Guid paymentTermId, CancellationToken ct)
    {
        var paymentTerm = await _paymentTermRepo.GetByIdAsync(_ctx.TenantId, paymentTermId, ct);
        if (paymentTerm is null || !paymentTerm.IsActive)
            return (StaleMessage("la condición de pago ya no existe o está inactiva"), null);

        var match = await _lookup.FindByIdentificationAsync(identificationType, identificationNumber, _role.Role, ct);
        var reason = StaleReason(action, existingBusinessPartnerId, paymentTermId, match);
        return (reason is null ? null : StaleMessage(reason), match);
    }

    private string? StaleReason(
        PartnerImportAction action, Guid? existingId, Guid paymentTermId, BusinessPartnerImportMatch? match)
    {
        if (action == PartnerImportAction.Create)
            return match is null ? null : "el tercero ya existe en el maestro";
        if (match is null || match.BusinessPartnerId != existingId)
            return "el tercero ya no existe en el maestro";
        if (match.IsAmbiguous)
            return "la identificación es ambigua en el maestro";
        if (!match.IsActive)
            return "el tercero fue inactivado";
        if ((action is PartnerImportAction.AssignRole or PartnerImportAction.ReactivateRole) && match.HasActiveRole)
            return $"el tercero ya tiene rol {_role.RoleName}";
        if (action == PartnerImportAction.AssignRole && match.HasRevokedRole)
            return $"el tercero tiene el rol {_role.RoleName} revocado";
        if (action == PartnerImportAction.ReactivateRole && !match.HasRevokedRole)
            return $"el tercero ya no tiene el rol {_role.RoleName} revocado";
        if (action == PartnerImportAction.AlreadyHasRole && !match.HasActiveRole)
            return $"el tercero ya no tiene rol {_role.RoleName} activo";
        if (match.CompanyPaymentTermId is { } current && current != paymentTermId)
            return $"el {_role.Singular} ya tiene otra condición de pago en esta empresa";
        return null;
    }

    private static string StaleMessage(string reason) =>
        $"El maestro cambió desde la validación ({reason}). Vuelva a validar el archivo.";

    private async Task<TaxIdentification?> ValidateIdentificationAsync(
        string type, string number, Action<string, string, string> error, CancellationToken ct)
    {
        if (type == TaxIdentification.SriConsumidorFinal)
        {
            error("CONSUMIDOR_FINAL_NOT_IMPORTABLE",
                "Consumidor Final es un tercero del sistema y no se carga por plantilla.",
                PartnerImportColumns.IdentificationType);
            return null;
        }

        if (type.Length == 1 && char.IsAsciiDigit(type[0]))
        {
            error("LEADING_ZERO_LOST",
                $"Tipo de identificación '{type}': probablemente Excel quitó el 0 inicial (use p. ej. 0{type}). "
                + "Formatee la columna como Texto.",
                PartnerImportColumns.IdentificationType);
            return null;
        }

        if (!await _usageValidator.IsAllowedAsync(type, _role.Usage, ct))
        {
            error("INVALID_IDENTIFICATION_TYPE",
                $"El tipo de identificación '{type}' no existe o no está permitido para {_role.Plural}.",
                PartnerImportColumns.IdentificationType);
            return null;
        }

        if (type is TaxIdentification.SriRuc or TaxIdentification.SriCi)
        {
            if (!number.All(char.IsAsciiDigit))
            {
                error("INVALID_IDENTIFICATION", "RUC y cédula solo admiten dígitos, sin espacios ni guiones.",
                    PartnerImportColumns.IdentificationNumber);
                return null;
            }
            var expected = type == TaxIdentification.SriRuc ? 13 : 10;
            if (number.Length == expected - 1)
            {
                error("LEADING_ZERO_LOST",
                    $"Tiene {number.Length} dígitos: probablemente Excel quitó el 0 inicial. "
                    + "Formatee la columna como Texto y vuelva a escribir el número.",
                    PartnerImportColumns.IdentificationNumber);
                return null;
            }
        }

        try
        {
            return TaxIdentification.Create(type, number);
        }
        catch (ArgumentException ex)
        {
            error("INVALID_IDENTIFICATION", PublicMessage(ex), PartnerImportColumns.IdentificationNumber);
            return null;
        }
    }

    private async Task ValidateNewPartnerAsync(
        TaxIdentification identification, string? legalEntityRaw, int? legalEntityTypeCode, string legalName,
        string? email, string? phone, Action<string, string, string> error, CancellationToken ct)
    {
        // Un código con formato inválido ya quedó reportado; no se intenta resolverlo.
        if (legalEntityRaw is null || legalEntityTypeCode is not null)
        {
            int? resolved = null;
            try
            {
                resolved = identification.ResolveLegalEntityTypeCode(legalEntityTypeCode);
            }
            catch (ArgumentException ex)
            {
                error("INVALID_LEGAL_ENTITY_TYPE", PublicMessage(ex), PartnerImportColumns.LegalEntityTypeCode);
            }

            if (resolved is { } code && !await _legalEntityTypeRepo.ExistsActiveAsync(code, ct))
                error("INVALID_LEGAL_ENTITY_TYPE", $"El tipo de entidad legal {code} no existe o está inactivo.",
                    PartnerImportColumns.LegalEntityTypeCode);
        }

        // El contacto usa la Razón Social como nombre (máx. del contacto < máx. BP).
        if ((email is not null || phone is not null) && legalName.Length > BusinessPartnerContact.FirstNameMaxLen)
            error("CONTACT_NAME_TOO_LONG",
                $"Con Email/Teléfono la Razón Social no puede superar {BusinessPartnerContact.FirstNameMaxLen} "
                + $"caracteres (se usa como nombre del contacto de {_role.ContactPurpose}).",
                PartnerImportColumns.LegalName);
    }

    private PartnerImportAction ClassifyExisting(
        BusinessPartnerImportMatch match, Guid? paymentTermId, Action<string, string, string> error)
    {
        if (match.IsAmbiguous)
        {
            error("AMBIGUOUS_IDENTIFICATION",
                "Existe más de un tercero con esta identificación (solo difieren en mayúsculas). Corrija el maestro.",
                PartnerImportColumns.IdentificationNumber);
            return PartnerImportAction.AlreadyHasRole;
        }

        if (!match.IsActive)
        {
            error("INACTIVE_BUSINESS_PARTNER",
                $"El tercero {match.LegalName} existe pero está inactivo. Reactívelo antes de importarlo.",
                PartnerImportColumns.IdentificationNumber);
            return PartnerImportAction.AlreadyHasRole;
        }

        // La condición existente en la Company nunca se sobrescribe — tampoco la que conserva un
        // tercero con el rol revocado.
        if (paymentTermId is { } requested && match.CompanyPaymentTermId is { } current && current != requested)
            error("PAYMENT_TERM_CONFLICT",
                $"El {_role.Singular} ya tiene otra condición de pago en esta empresa; la carga inicial no la sobrescribe.",
                PartnerImportColumns.PaymentTermCode);

        if (match.HasActiveRole)
            return PartnerImportAction.AlreadyHasRole;
        return match.HasRevokedRole ? PartnerImportAction.ReactivateRole : PartnerImportAction.AssignRole;
    }

    private async Task<Guid?> ResolvePaymentTermAsync(
        string? code, Action<string, string, string> error, CancellationToken ct)
    {
        if (code is null)
        {
            error("MISSING_REQUIRED_FIELD", "La condición de pago es obligatoria.",
                PartnerImportColumns.PaymentTermCode);
            return null;
        }

        _paymentTerms ??= await _paymentTermRepo.ListAsync(_ctx.TenantId, null, ct);
        var match = _paymentTerms.FirstOrDefault(pt =>
            pt.IsActive && string.Equals(pt.Code, code, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error("INVALID_PAYMENT_TERM", $"La condición de pago '{code}' no existe o está inactiva.",
                PartnerImportColumns.PaymentTermCode);
            return null;
        }
        return match.Id;
    }

    /// <summary>
    /// Mensaje de la regla de dominio sin el sufijo técnico "(Parameter 'x')" que .NET agrega
    /// cuando la excepción tiene ParamName. El sufijo se obtiene del propio runtime, así respeta
    /// su idioma.
    /// </summary>
    public static string PublicMessage(ArgumentException ex)
    {
        if (string.IsNullOrEmpty(ex.ParamName))
            return ex.Message;
        var suffix = new ArgumentException(string.Empty, ex.ParamName).Message;
        return ex.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? ex.Message[..^suffix.Length].TrimEnd()
            : ex.Message;
    }

    public static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
