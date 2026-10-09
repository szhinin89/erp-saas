using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Clientes (INITIAL-LOAD-ARCH-01, endurecido en IL-2A). Confirmar una fila NUNCA
/// escribe directo a BusinessPartner — orquesta los mismos comandos MediatR que el flujo manual.
///
/// IL-2A — Validate es fiel a Confirm: aplica las mismas reglas de dominio (identificación SRI,
/// naturaleza jurídica, contacto) y clasifica cada fila contra el maestro del tenant:
/// identificación nueva → crear; BP existente sin rol Cliente → reutilizar y asignar rol; ya es
/// Cliente → idempotente. Nunca se crea un BP duplicado. La condición de pago es obligatoria y se
/// guarda por Company (<c>CompanyBpSalesSettings</c>), nunca en el BP global ni con default.
/// </summary>
public sealed class CustomerImportProcessor : IImportProcessor, IImportBatchValidator
{
    private readonly ICustomerImportSheetReader _reader;
    private readonly ICustomerImportLookup _lookup;
    private readonly IPaymentTermRepository _paymentTermRepo;
    private readonly ILegalEntityTypeRepository _legalEntityTypeRepo;
    private readonly IIdentificationUsageValidator _usageValidator;
    private readonly IOperationalContext _ctx;
    private readonly IMediator _mediator;
    private IReadOnlyList<PaymentTerm>? _paymentTerms;

    public CustomerImportProcessor(
        ICustomerImportSheetReader reader,
        ICustomerImportLookup lookup,
        IPaymentTermRepository paymentTermRepo,
        ILegalEntityTypeRepository legalEntityTypeRepo,
        IIdentificationUsageValidator usageValidator,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _lookup = lookup;
        _paymentTermRepo = paymentTermRepo;
        _legalEntityTypeRepo = legalEntityTypeRepo;
        _usageValidator = usageValidator;
        _ctx = ctx;
        _mediator = mediator;
    }

    public ImportType ImportType => ImportType.Customers;

    public string TemplateFileName => "plantilla-clientes.xlsx";

    public async Task<ImportTemplateFileDto> BuildTemplateAsync(CancellationToken ct)
    {
        var content = await _reader.BuildTemplateAsync(ct);
        return new ImportTemplateFileDto(
            content,
            TemplateFileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    public Task<ImportReadResult> ReadAsync(Stream fileContent, CancellationToken ct) =>
        _reader.ReadAsync(fileContent, ct);

    public async Task<RowValidationResult> ValidateRowAsync(
        int rowNumber,
        IReadOnlyDictionary<string, string?> rawRow,
        bool autoCreateCatalogValues,
        CancellationToken ct
    )
    {
        var issues = new List<RowIssue>();
        void Error(string code, string message, string field) =>
            issues.Add(new RowIssue(ImportSeverity.Error, code, message, field));

        var identificationType = Get(rawRow, CustomerImportColumns.IdentificationType) ?? string.Empty;
        var rawNumber = Get(rawRow, CustomerImportColumns.IdentificationNumber) ?? string.Empty;
        var legalEntityRaw = Get(rawRow, CustomerImportColumns.LegalEntityTypeCode);
        var legalName = Get(rawRow, CustomerImportColumns.LegalName) ?? string.Empty;
        var tradeName = Get(rawRow, CustomerImportColumns.TradeName);
        var countryCode = Get(rawRow, CustomerImportColumns.CountryCode)?.ToUpperInvariant();
        var email = Get(rawRow, CustomerImportColumns.Email);
        var phone = Get(rawRow, CustomerImportColumns.Phone);
        var paymentTermCode = Get(rawRow, CustomerImportColumns.PaymentTermCode);

        if (identificationType.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "El tipo de identificación es obligatorio.",
                CustomerImportColumns.IdentificationType);
        if (rawNumber.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "El número de identificación es obligatorio.",
                CustomerImportColumns.IdentificationNumber);
        if (legalName.Length == 0)
            Error("MISSING_REQUIRED_FIELD", "La razón social es obligatoria.", CustomerImportColumns.LegalName);
        else if (legalName.Length < 2 || legalName.Length > PersonName.LegalNameMaxLen)
            Error("INVALID_LEGAL_NAME",
                $"La razón social debe tener entre 2 y {PersonName.LegalNameMaxLen} caracteres.",
                CustomerImportColumns.LegalName);
        if (tradeName is { Length: > PersonName.TradeNameMaxLen })
            Error("INVALID_TRADE_NAME",
                $"El nombre comercial no puede superar {PersonName.TradeNameMaxLen} caracteres.",
                CustomerImportColumns.TradeName);
        if (countryCode is not null && (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter)))
            Error("INVALID_COUNTRY", "País debe ser un código ISO de 2 letras (p. ej. EC).",
                CustomerImportColumns.CountryCode);

        var number = rawNumber;
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
                    CustomerImportColumns.LegalEntityTypeCode);
        }

        if (email is not null || phone is not null)
        {
            try
            {
                ContactInfo.Create(phone, null, email);
            }
            catch (ArgumentException ex)
            {
                Error("INVALID_CONTACT", ex.Message,
                    email is not null ? CustomerImportColumns.Email : CustomerImportColumns.Phone);
            }
        }

        var paymentTermId = await ResolvePaymentTermAsync(paymentTermCode, Error, ct);

        var action = CustomerImportAction.CreateCustomer;
        Guid? existingId = null;
        if (identification is not null)
        {
            var match = await _lookup.FindByIdentificationAsync(identification.Type, identification.Number, ct);
            if (match is null)
            {
                await ValidateNewPartnerAsync(identification, legalEntityRaw, legalEntityTypeCode, legalName,
                    email, phone, Error, ct);
            }
            else
            {
                existingId = match.BusinessPartnerId;
                number = match.IdentificationNumber;
                action = ClassifyExisting(match, paymentTermId, Error);
                if (!match.IsAmbiguous && match.IsActive)
                    issues.Add(new RowIssue(ImportSeverity.Warning, "EXISTING_BUSINESS_PARTNER",
                        $"El tercero ya existe ({match.LegalName}): se conserva su ficha maestra; Razón Social, "
                        + "Nombre Comercial, Tipo Entidad Legal, País y contacto del archivo no se aplican.",
                        CustomerImportColumns.IdentificationNumber));
            }
        }

        var parsed = new ParsedCustomerRow(
            identificationType,
            number,
            legalEntityTypeCode,
            legalName,
            tradeName,
            countryCode,
            email,
            phone,
            paymentTermId ?? Guid.Empty,
            action,
            existingId
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows)
    {
        var parsed = rows.Select(r => JsonSerializer.Deserialize<ParsedCustomerRow>(r.ParsedDataJson)!).ToList();
        var issues = rows.Select(r => r.Issues.ToList()).ToList();
        var duplicates = parsed
            .Select((r, i) => (Key: r.IdentificationType + ":" + r.IdentificationNumber.ToUpperInvariant(), Index: i,
                Valid: r.IdentificationType.Length > 0 && r.IdentificationNumber.Length > 0))
            .Where(e => e.Valid)
            .GroupBy(e => e.Key)
            .Where(g => g.Count() > 1);
        foreach (var group in duplicates)
            foreach (var entry in group)
                issues[entry.Index].Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_IDENTIFICATION_IN_FILE",
                    "La identificación se repite en otras filas del archivo.",
                    CustomerImportColumns.IdentificationNumber));

        return rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedCustomerRow>(parsedDataJson)!;
        if (parsed.PaymentTermId == Guid.Empty)
            return RowConfirmResult.Failed("La fila no tiene condición de pago validada.");

        Guid businessPartnerId;
        switch (parsed.Action)
        {
            case CustomerImportAction.CreateCustomer:
            {
                var bpResult = await _mediator.Send(
                    new CreateBusinessPartnerCommand(
                        parsed.IdentificationType,
                        parsed.IdentificationNumber,
                        parsed.LegalEntityTypeCode,
                        parsed.LegalName,
                        parsed.TradeName,
                        parsed.CountryCode
                    ),
                    ct
                );
                if (!bpResult.IsSuccess)
                    return RowConfirmResult.Failed(bpResult.Error ?? "No se pudo crear el tercero.");
                businessPartnerId = bpResult.Value!.Id;

                // Sin transacción cruzada entre agregados hasta IL-2B: cada fallo posterior se
                // reporta explícitamente para revisión manual, nunca como éxito silencioso.
                var roleError = await AssignCustomerRoleAsync(businessPartnerId, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed($"Cliente creado sin rol asignado, revisar manualmente: {roleError}");

                if (parsed.Email is not null || parsed.Phone is not null)
                {
                    var contactResult = await _mediator.Send(
                        new CreateBpContactCommand(
                            businessPartnerId,
                            parsed.LegalName,
                            ContactRole.Billing,
                            Email: parsed.Email,
                            Phone: parsed.Phone
                        ),
                        ct
                    );
                    if (!contactResult.IsSuccess)
                        return RowConfirmResult.Failed(
                            $"Cliente creado sin contacto, revisar manualmente: {contactResult.Error}");
                }
                break;
            }
            case CustomerImportAction.AssignCustomerRole:
            {
                businessPartnerId = parsed.ExistingBusinessPartnerId!.Value;
                var roleError = await AssignCustomerRoleAsync(businessPartnerId, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed(roleError);
                break;
            }
            case CustomerImportAction.AlreadyCustomer:
                businessPartnerId = parsed.ExistingBusinessPartnerId!.Value;
                break;
            default:
                return RowConfirmResult.Failed("Acción de importación no soportada.");
        }

        var settingsResult = await _mediator.Send(
            new UpsertCompanyBpSalesSettingsCommand(businessPartnerId, parsed.PaymentTermId),
            ct
        );
        if (!settingsResult.IsSuccess)
            return RowConfirmResult.Failed(
                $"Cliente sin condición de pago en esta empresa, revisar manualmente: {settingsResult.Error}");

        return RowConfirmResult.Success(businessPartnerId);
    }

    private async Task<string?> AssignCustomerRoleAsync(Guid businessPartnerId, CancellationToken ct)
    {
        var roleResult = await _mediator.Send(
            new AssignBusinessPartnerRoleCommand(businessPartnerId, RoleType.Customer),
            ct
        );
        return roleResult.IsSuccess ? null : roleResult.Error ?? "No se pudo asignar el rol Cliente.";
    }

    private async Task<TaxIdentification?> ValidateIdentificationAsync(
        string type, string number, Action<string, string, string> error, CancellationToken ct)
    {
        if (type == TaxIdentification.SriConsumidorFinal)
        {
            error("CONSUMIDOR_FINAL_NOT_IMPORTABLE",
                "Consumidor Final es un tercero del sistema y no se carga por plantilla.",
                CustomerImportColumns.IdentificationType);
            return null;
        }

        if (type.Length == 1 && char.IsAsciiDigit(type[0]))
        {
            error("LEADING_ZERO_LOST",
                $"Tipo de identificación '{type}': probablemente Excel quitó el 0 inicial (use p. ej. 0{type}). "
                + "Formatee la columna como Texto.",
                CustomerImportColumns.IdentificationType);
            return null;
        }

        if (!await _usageValidator.IsAllowedAsync(type, IdentificationUsageType.Customer, ct))
        {
            error("INVALID_IDENTIFICATION_TYPE",
                $"El tipo de identificación '{type}' no existe o no está permitido para clientes.",
                CustomerImportColumns.IdentificationType);
            return null;
        }

        if (type is TaxIdentification.SriRuc or TaxIdentification.SriCi)
        {
            if (!number.All(char.IsAsciiDigit))
            {
                error("INVALID_IDENTIFICATION", "RUC y cédula solo admiten dígitos, sin espacios ni guiones.",
                    CustomerImportColumns.IdentificationNumber);
                return null;
            }
            var expected = type == TaxIdentification.SriRuc ? 13 : 10;
            if (number.Length == expected - 1)
            {
                error("LEADING_ZERO_LOST",
                    $"Tiene {number.Length} dígitos: probablemente Excel quitó el 0 inicial. "
                    + "Formatee la columna como Texto y vuelva a escribir el número.",
                    CustomerImportColumns.IdentificationNumber);
                return null;
            }
        }

        try
        {
            return TaxIdentification.Create(type, number);
        }
        catch (ArgumentException ex)
        {
            error("INVALID_IDENTIFICATION", ex.Message, CustomerImportColumns.IdentificationNumber);
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
                error("INVALID_LEGAL_ENTITY_TYPE", ex.Message, CustomerImportColumns.LegalEntityTypeCode);
            }

            if (resolved is { } code && !await _legalEntityTypeRepo.ExistsActiveAsync(code, ct))
                error("INVALID_LEGAL_ENTITY_TYPE", $"El tipo de entidad legal {code} no existe o está inactivo.",
                    CustomerImportColumns.LegalEntityTypeCode);
        }

        // El contacto de facturación usa la Razón Social como nombre (máx. del contacto < máx. BP).
        if ((email is not null || phone is not null) && legalName.Length > BusinessPartnerContact.FirstNameMaxLen)
            error("CONTACT_NAME_TOO_LONG",
                $"Con Email/Teléfono la Razón Social no puede superar {BusinessPartnerContact.FirstNameMaxLen} "
                + "caracteres (se usa como nombre del contacto de facturación).",
                CustomerImportColumns.LegalName);
    }

    private static CustomerImportAction ClassifyExisting(
        CustomerImportMatch match, Guid? paymentTermId, Action<string, string, string> error)
    {
        if (match.IsAmbiguous)
        {
            error("AMBIGUOUS_IDENTIFICATION",
                "Existe más de un tercero con esta identificación (solo difieren en mayúsculas). Corrija el maestro.",
                CustomerImportColumns.IdentificationNumber);
            return CustomerImportAction.AlreadyCustomer;
        }

        if (!match.IsActive)
        {
            error("INACTIVE_BUSINESS_PARTNER",
                $"El tercero {match.LegalName} existe pero está inactivo. Reactívelo antes de importarlo.",
                CustomerImportColumns.IdentificationNumber);
            return CustomerImportAction.AlreadyCustomer;
        }

        if (!match.HasActiveCustomerRole)
            return CustomerImportAction.AssignCustomerRole;

        if (paymentTermId is { } requested && match.CompanyPaymentTermId is { } current && current != requested)
            error("PAYMENT_TERM_CONFLICT",
                "El cliente ya tiene otra condición de pago en esta empresa; la carga inicial no la sobrescribe.",
                CustomerImportColumns.PaymentTermCode);

        return CustomerImportAction.AlreadyCustomer;
    }

    private async Task<Guid?> ResolvePaymentTermAsync(
        string? code, Action<string, string, string> error, CancellationToken ct)
    {
        if (code is null)
        {
            error("MISSING_REQUIRED_FIELD", "La condición de pago es obligatoria.",
                CustomerImportColumns.PaymentTermCode);
            return null;
        }

        _paymentTerms ??= await _paymentTermRepo.ListAsync(_ctx.TenantId, null, ct);
        var match = _paymentTerms.FirstOrDefault(pt =>
            pt.IsActive && string.Equals(pt.Code, code, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error("INVALID_PAYMENT_TERM", $"La condición de pago '{code}' no existe o está inactiva.",
                CustomerImportColumns.PaymentTermCode);
            return null;
        }
        return match.Id;
    }

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
