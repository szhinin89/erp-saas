using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.Services;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Proveedores (INITIAL-LOAD-SUPPLIERS-01, endurecido en IL-3A). Mismo contrato
/// que Clientes vía <see cref="BusinessPartnerImportRules"/>: Validate es fiel a Confirm, solo
/// acepta los tipos de identificación que el catálogo SRI permite para Proveedor (04 RUC, 08
/// Exterior) y clasifica contra el maestro: nuevo → crear; sin rol Proveedor → reutilizar y asignar
/// rol; ya Proveedor → idempotente; nunca se duplica un BP.
///
/// La condición de pago es obligatoria, sin default, y se guarda por Company en
/// <c>CompanyBpPurchaseSettings</c> (ADR-033). "Obligado a llevar contabilidad" y "Exento de
/// retención" alimentan retenciones: obligatorios SI/NO, sin default, y solo se aplican al asignar
/// el rol — nunca modifican los datos fiscales de un proveedor existente.
/// </summary>
public sealed class SupplierImportProcessor : IImportProcessor, IImportBatchValidator
{
    private readonly ISupplierImportSheetReader _reader;
    private readonly IMediator _mediator;
    private readonly BusinessPartnerImportRules _rules;

    public SupplierImportProcessor(
        ISupplierImportSheetReader reader,
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
            ctx, PartnerImportRole.Supplier);
    }

    public ImportType ImportType => ImportType.Suppliers;

    public string TemplateFileName => "plantilla-proveedores.xlsx";

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

        var keepsAccounting = ParseYesNo(rawRow, SupplierImportColumns.IsRequiredToKeepAccounting, issues);
        var retentionExempt = ParseYesNo(rawRow, SupplierImportColumns.IsRetentionExempt, issues);
        if (keepsAccounting is { } keeps && retentionExempt is { } exempt)
        {
            // Misma invariante de dominio que aplicará AssignBusinessPartnerRole al confirmar.
            var config = RoleConfigFactory.Build(new SupplierRoleConfigDto(null, null, null, exempt, keeps));
            if (!config.IsValid)
                issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_SUPPLIER_FISCAL_DATA", config.Error!,
                    SupplierImportColumns.IsRetentionExempt));
        }
        if (row.Action == PartnerImportAction.Create && row.Email is null && row.Phone is null)
            issues.Add(new RowIssue(ImportSeverity.Warning, "MISSING_CONTACT_INFO",
                "El proveedor no tiene email ni teléfono — se importa igual, pero sin datos de contacto."));
        if (row.Action == PartnerImportAction.ReactivateRole)
            issues.Add(row.Match!.RevokedRoleHasFiscalData
                ? new RowIssue(ImportSeverity.Warning, "REVOKED_SUPPLIER_REACTIVATED",
                    "El tercero tuvo rol Proveedor (revocado): se reactiva conservando sus datos fiscales previos; "
                    + "Obligado a llevar contabilidad y Exento de retención del archivo NO se aplican.",
                    SupplierImportColumns.IsRequiredToKeepAccounting)
                : new RowIssue(ImportSeverity.Error, "REVOKED_SUPPLIER_FISCAL_DATA_MISSING",
                    "El tercero tuvo rol Proveedor (revocado) sin datos fiscales registrados. La carga inicial no los "
                    + "completa ni los asume: corrija explícitamente Obligado a llevar contabilidad y Exento de "
                    + "retención en la ficha del proveedor antes de importarlo.",
                    SupplierImportColumns.IsRequiredToKeepAccounting));
        if (row.Action == PartnerImportAction.AlreadyHasRole && row.ExistingBusinessPartnerId.HasValue)
            issues.Add(new RowIssue(ImportSeverity.Warning, "EXISTING_SUPPLIER_FISCAL_DATA_KEPT",
                "El tercero ya es Proveedor: se conservan sus datos fiscales; Obligado a llevar contabilidad "
                + "y Exento de retención del archivo no se aplican.",
                SupplierImportColumns.IsRequiredToKeepAccounting));

        var parsed = new ParsedSupplierRow(
            row.IdentificationType,
            row.IdentificationNumber,
            row.LegalEntityTypeCode,
            row.LegalName,
            row.TradeName,
            row.CountryCode,
            row.Email,
            row.Phone,
            row.PaymentTermId ?? Guid.Empty,
            keepsAccounting ?? false,
            retentionExempt ?? false,
            row.Action,
            row.ExistingBusinessPartnerId
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows) =>
        BusinessPartnerImportRules.FlagDuplicateIdentifications(rows, json =>
        {
            var parsed = JsonSerializer.Deserialize<ParsedSupplierRow>(json)!;
            return (parsed.IdentificationType, parsed.IdentificationNumber);
        });

    /// <summary>
    /// IL-3B: se ejecuta dentro de la transacción única del lote — cualquier <c>Failed</c> revierte
    /// todo. Antes de escribir se revalida contra el maestro actual lo que Validate clasificó
    /// (stale preview): si el tercero, su rol o su condición cambiaron, la fila falla.
    /// </summary>
    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedSupplierRow>(parsedDataJson)!;
        if (parsed.PaymentTermId == Guid.Empty)
            return RowConfirmResult.Failed("La fila no tiene condición de pago validada.");

        var (staleError, match) = await _rules.RevalidateAsync(parsed.IdentificationType,
            parsed.IdentificationNumber, parsed.Action, parsed.ExistingBusinessPartnerId, parsed.PaymentTermId, ct);
        if (staleError is not null)
            return RowConfirmResult.Failed(staleError);
        if (parsed.Action == PartnerImportAction.ReactivateRole && !match!.RevokedRoleHasFiscalData)
            return RowConfirmResult.Failed(
                "El maestro cambió desde la validación (el rol Proveedor revocado no tiene datos fiscales). "
                + "Vuelva a validar el archivo.");

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

                var roleError = await AssignSupplierRoleAsync(businessPartnerId, parsed, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed(roleError);

                if (parsed.Email is not null || parsed.Phone is not null)
                {
                    var contactResult = await _mediator.Send(
                        new CreateBpContactCommand(
                            businessPartnerId,
                            parsed.LegalName,
                            ContactRole.Purchasing,
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
                var roleError = await AssignSupplierRoleAsync(businessPartnerId, parsed, ct);
                if (roleError is not null)
                    return RowConfirmResult.Failed(roleError);
                break;
            }
            case PartnerImportAction.ReactivateRole:
            {
                // Reactivación sin config: los datos fiscales previos se conservan, nunca se sobrescriben.
                businessPartnerId = parsed.ExistingBusinessPartnerId!.Value;
                var roleResult = await _mediator.Send(
                    new AssignBusinessPartnerRoleCommand(businessPartnerId, RoleType.Supplier), ct);
                if (!roleResult.IsSuccess)
                    return RowConfirmResult.Failed(roleResult.Error ?? "No se pudo reactivar el rol Proveedor.");
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

        // ADR-033: el default de condición de pago para compras/gastos vive en
        // CompanyBpPurchaseSettings (company-scoped), no en SupplierRoleConfig.
        var settingsResult = await _mediator.Send(
            new UpsertCompanyBpPurchaseSettingsCommand(businessPartnerId, parsed.PaymentTermId),
            ct
        );
        if (!settingsResult.IsSuccess)
            return RowConfirmResult.Failed($"Condición de pago: {settingsResult.Error}");

        return RowConfirmResult.Success(businessPartnerId);
    }

    private async Task<string?> AssignSupplierRoleAsync(
        Guid businessPartnerId, ParsedSupplierRow parsed, CancellationToken ct)
    {
        var roleResult = await _mediator.Send(
            new AssignBusinessPartnerRoleCommand(
                businessPartnerId,
                RoleType.Supplier,
                SupplierConfig: new SupplierRoleConfigDto(
                    null,
                    null,
                    null,
                    parsed.IsRetentionExempt,
                    parsed.IsRequiredToKeepAccounting
                )
            ),
            ct
        );
        return roleResult.IsSuccess ? null : roleResult.Error ?? "No se pudo asignar el rol Proveedor.";
    }

    private static bool? ParseYesNo(IReadOnlyDictionary<string, string?> row, string column, List<RowIssue> issues)
    {
        var raw = BusinessPartnerImportRules.Get(row, column);
        if (string.Equals(raw, "SI", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(raw, "NO", StringComparison.OrdinalIgnoreCase))
            return false;
        issues.Add(raw is null
            ? new RowIssue(ImportSeverity.Error, "MISSING_REQUIRED_FIELD", $"{column} es obligatorio (SI/NO).", column)
            : new RowIssue(ImportSeverity.Error, "INVALID_YES_NO", $"{column} admite únicamente SI o NO.", column));
        return null;
    }
}
