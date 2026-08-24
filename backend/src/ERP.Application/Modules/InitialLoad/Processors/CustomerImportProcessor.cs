using System.Text.Json;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpTradingSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Único <c>IImportProcessor</c> registrado en esta entrega (INITIAL-LOAD-ARCH-01). Confirmar una
/// fila NUNCA escribe directo a BusinessPartner — orquesta los mismos comandos MediatR que usaría
/// un usuario manual (<see cref="CreateBusinessPartnerCommand"/> → <see cref="AssignBusinessPartnerRoleCommand"/>
/// → opcionalmente <see cref="CreateBpContactCommand"/> / <see cref="UpsertCompanyBpTradingSettingsCommand"/>),
/// reutilizando exactamente las mismas invariantes/validaciones/duplicados del flujo manual.
/// </summary>
public sealed class CustomerImportProcessor : IImportProcessor
{
    private readonly ICustomerImportSheetReader _reader;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IMediator _mediator;

    public CustomerImportProcessor(
        ICustomerImportSheetReader reader,
        IBusinessPartnerRepository bpRepo,
        IMediator mediator
    )
    {
        _reader = reader;
        _bpRepo = bpRepo;
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

        var identificationType = Get(rawRow, CustomerImportColumns.IdentificationType);
        var identificationNumber = Get(rawRow, CustomerImportColumns.IdentificationNumber);
        var legalName = Get(rawRow, CustomerImportColumns.LegalName);

        if (string.IsNullOrWhiteSpace(identificationType))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "El tipo de identificación es obligatorio.",
                    CustomerImportColumns.IdentificationType
                )
            );

        if (string.IsNullOrWhiteSpace(identificationNumber))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "El número de identificación es obligatorio.",
                    CustomerImportColumns.IdentificationNumber
                )
            );

        if (string.IsNullOrWhiteSpace(legalName))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "La razón social es obligatoria.",
                    CustomerImportColumns.LegalName
                )
            );

        decimal? creditLimit = null;
        var creditLimitRaw = Get(rawRow, CustomerImportColumns.CreditLimit);
        if (!string.IsNullOrWhiteSpace(creditLimitRaw))
        {
            if (decimal.TryParse(creditLimitRaw, out var parsedCreditLimit) && parsedCreditLimit >= 0)
                creditLimit = parsedCreditLimit;
            else
                issues.Add(
                    new RowIssue(
                        ImportSeverity.Error,
                        "INVALID_NUMBER",
                        "El límite de crédito no es un número válido.",
                        CustomerImportColumns.CreditLimit
                    )
                );
        }

        int? paymentDays = null;
        var paymentDaysRaw = Get(rawRow, CustomerImportColumns.PaymentDays);
        if (!string.IsNullOrWhiteSpace(paymentDaysRaw))
        {
            if (int.TryParse(paymentDaysRaw, out var parsedPaymentDays) && parsedPaymentDays >= 0)
                paymentDays = parsedPaymentDays;
            else
                issues.Add(
                    new RowIssue(
                        ImportSeverity.Error,
                        "INVALID_NUMBER",
                        "Los días de pago no son un número válido.",
                        CustomerImportColumns.PaymentDays
                    )
                );
        }

        if (
            !string.IsNullOrWhiteSpace(identificationType)
            && !string.IsNullOrWhiteSpace(identificationNumber)
            && await _bpRepo.ExistsByIdentificationAsync(
                identificationType.Trim(),
                identificationNumber.Trim(),
                cancellationToken: ct
            )
        )
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "DUPLICATE_IDENTIFICATION",
                    $"Ya existe un tercero con {identificationType} {identificationNumber}.",
                    CustomerImportColumns.IdentificationNumber
                )
            );
        }

        var parsed = new ParsedCustomerRow(
            identificationType?.Trim() ?? string.Empty,
            identificationNumber?.Trim() ?? string.Empty,
            legalName?.Trim() ?? string.Empty,
            Get(rawRow, CustomerImportColumns.TradeName),
            Get(rawRow, CustomerImportColumns.CountryCode),
            Get(rawRow, CustomerImportColumns.Email),
            Get(rawRow, CustomerImportColumns.Phone),
            Get(rawRow, CustomerImportColumns.CustomerCategory),
            Get(rawRow, CustomerImportColumns.CustomerSegment),
            Get(rawRow, CustomerImportColumns.SalesZone),
            creditLimit,
            paymentDays
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedCustomerRow>(parsedDataJson)!;

        var bpResult = await _mediator.Send(
            new CreateBusinessPartnerCommand(
                parsed.IdentificationType,
                parsed.IdentificationNumber,
                null,
                parsed.LegalName,
                parsed.TradeName,
                parsed.CountryCode
            ),
            ct
        );
        if (!bpResult.IsSuccess)
            return RowConfirmResult.Failed(bpResult.Error ?? "No se pudo crear el tercero.");

        var businessPartnerId = bpResult.Value!.Id;

        var roleResult = await _mediator.Send(
            new AssignBusinessPartnerRoleCommand(
                businessPartnerId,
                RoleType.Customer,
                CustomerConfig: CustomerRoleConfig.Create(
                    parsed.CustomerCategory,
                    parsed.CustomerSegment,
                    parsed.SalesZone
                )
            ),
            ct
        );
        if (!roleResult.IsSuccess)
        {
            // El BP ya fue creado y committeado por CreateBusinessPartnerCommand — no hay
            // transacción cruzada entre agregados (mismo patrón preexistente en el resto del
            // código para BusinessPartner + BusinessPartnerRole). Se reporta explícitamente
            // para revisión manual en vez de dejarlo como fallo silencioso.
            return RowConfirmResult.Failed(
                $"Cliente creado sin rol asignado, revisar manualmente: {roleResult.Error}"
            );
        }

        if (!string.IsNullOrWhiteSpace(parsed.Email) || !string.IsNullOrWhiteSpace(parsed.Phone))
        {
            await _mediator.Send(
                new CreateBpContactCommand(
                    businessPartnerId,
                    parsed.LegalName,
                    ContactRole.Billing,
                    Email: parsed.Email,
                    Phone: parsed.Phone
                ),
                ct
            );
        }

        if (parsed.CreditLimit.HasValue || parsed.PaymentDays.HasValue)
        {
            await _mediator.Send(
                new UpsertCompanyBpTradingSettingsCommand(
                    businessPartnerId,
                    parsed.CreditLimit ?? 0,
                    parsed.PaymentDays ?? 0
                ),
                ct
            );
        }

        return RowConfirmResult.Success(businessPartnerId);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) ? value?.Trim() : null;
}
