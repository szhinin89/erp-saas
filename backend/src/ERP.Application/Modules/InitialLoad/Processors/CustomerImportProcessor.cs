using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Clientes (INITIAL-LOAD-ARCH-01, endurecido en IL-2A). Confirmar una fila NUNCA
/// escribe directo a BusinessPartner — orquesta los mismos comandos MediatR que el flujo manual.
///
/// IL-2A — Validate es fiel a Confirm (<see cref="BusinessPartnerImportRules"/>) y clasifica cada
/// fila contra el maestro del tenant: identificación nueva → crear; BP existente sin rol Cliente →
/// reutilizar y asignar rol; ya es Cliente → idempotente. Nunca se crea un BP duplicado. La
/// condición de pago es obligatoria y se guarda por Company (<c>CompanyBpSalesSettings</c>), nunca
/// en el BP global ni con default.
/// </summary>
public sealed class CustomerImportProcessor : IImportProcessor, IImportBatchValidator
{
    private readonly ICustomerImportSheetReader _reader;
    private readonly IMediator _mediator;
    private readonly BusinessPartnerImportRules _rules;

    public CustomerImportProcessor(
        ICustomerImportSheetReader reader,
        IBusinessPartnerImportLookup lookup,
        IPaymentTermRepository paymentTermRepo,
        ILegalEntityTypeRepository legalEntityTypeRepo,
        IIdentificationUsageValidator usageValidator,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _mediator = mediator;
        _rules = new BusinessPartnerImportRules(lookup, paymentTermRepo, legalEntityTypeRepo, usageValidator,
            ctx, PartnerImportRole.Customer);
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
        var row = await _rules.ValidateAsync(rawRow, issues, ct);

        var parsed = new ParsedCustomerRow(
            row.IdentificationType,
            row.IdentificationNumber,
            row.LegalEntityTypeCode,
            row.LegalName,
            row.TradeName,
            row.CountryCode,
            row.Email,
            row.Phone,
            row.PaymentTermId ?? Guid.Empty,
            row.Action,
            row.ExistingBusinessPartnerId
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows) =>
        BusinessPartnerImportRules.FlagDuplicateIdentifications(rows, json =>
        {
            var parsed = JsonSerializer.Deserialize<ParsedCustomerRow>(json)!;
            return (parsed.IdentificationType, parsed.IdentificationNumber);
        });

    /// <summary>
    /// IL-2B: se ejecuta dentro de la transacción única del lote — cualquier <c>Failed</c> revierte
    /// todo. Antes de escribir se revalida contra el maestro actual lo que Validate clasificó
    /// (stale preview): si el tercero, su rol o su condición cambiaron, la fila falla.
    /// </summary>
    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedCustomerRow>(parsedDataJson)!;
        if (parsed.PaymentTermId == Guid.Empty)
            return RowConfirmResult.Failed("La fila no tiene condición de pago validada.");

        var (staleError, match) = await _rules.RevalidateAsync(parsed.IdentificationType,
            parsed.IdentificationNumber, parsed.Action, parsed.ExistingBusinessPartnerId, parsed.PaymentTermId, ct);
        if (staleError is not null)
            return RowConfirmResult.Failed(staleError);

        Guid businessPartnerId;
        switch (parsed.Action)
        {
            case PartnerImportAction.Create:
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

                var roleError = await AssignCustomerRoleAsync(businessPartnerId, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed(roleError);

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
                        return RowConfirmResult.Failed($"Contacto inválido: {contactResult.Error}");
                }
                break;
            }
            case PartnerImportAction.AssignRole:
            {
                businessPartnerId = parsed.ExistingBusinessPartnerId!.Value;
                var roleError = await AssignCustomerRoleAsync(businessPartnerId, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed(roleError);
                break;
            }
            case PartnerImportAction.AlreadyHasRole:
                businessPartnerId = parsed.ExistingBusinessPartnerId!.Value;
                // Idempotente: misma condición ya registrada en esta empresa → no se escribe nada.
                if (match!.CompanyPaymentTermId == parsed.PaymentTermId)
                    return RowConfirmResult.Success(businessPartnerId);
                break;
            default:
                return RowConfirmResult.Failed("Acción de importación no soportada.");
        }

        var settingsResult = await _mediator.Send(
            new UpsertCompanyBpSalesSettingsCommand(businessPartnerId, parsed.PaymentTermId),
            ct
        );
        if (!settingsResult.IsSuccess)
            return RowConfirmResult.Failed($"Condición de pago: {settingsResult.Error}");

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
}
