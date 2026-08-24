using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Segundo <c>IImportProcessor</c> registrado (INITIAL-LOAD-SUPPLIERS-01), plugin del mismo
/// motor genérico que <see cref="CustomerImportProcessor"/> — mismo patrón: Confirm orquesta
/// los comandos MediatR existentes (<see cref="CreateBusinessPartnerCommand"/> →
/// <see cref="AssignBusinessPartnerRoleCommand"/> → opcionalmente <see cref="CreateBpContactCommand"/>),
/// nunca escribe directo a <c>BusinessPartner</c>.
///
/// Única condición operativa obligatoria de <see cref="SupplierRoleConfig"/> es
/// <c>PaymentTermId</c> — se resuelve por código de plantilla contra <see cref="IPaymentTermRepository"/>
/// durante la validación; sin condición de pago válida la fila queda bloqueada (no hay valor por
/// defecto silencioso: es una decisión de negocio, no algo que el importador deba inventar).
/// </summary>
public sealed class SupplierImportProcessor : IImportProcessor
{
    private readonly ISupplierImportSheetReader _reader;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IPaymentTermRepository _paymentTermRepo;
    private readonly IOperationalContext _ctx;
    private readonly IMediator _mediator;

    public SupplierImportProcessor(
        ISupplierImportSheetReader reader,
        IBusinessPartnerRepository bpRepo,
        IPaymentTermRepository paymentTermRepo,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _bpRepo = bpRepo;
        _paymentTermRepo = paymentTermRepo;
        _ctx = ctx;
        _mediator = mediator;
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

        var identificationType = Get(rawRow, SupplierImportColumns.IdentificationType);
        var identificationNumber = Get(rawRow, SupplierImportColumns.IdentificationNumber);
        var legalName = Get(rawRow, SupplierImportColumns.LegalName);
        var paymentTermCode = Get(rawRow, SupplierImportColumns.PaymentTermCode);
        var email = Get(rawRow, SupplierImportColumns.Email);
        var phone = Get(rawRow, SupplierImportColumns.Phone);

        if (string.IsNullOrWhiteSpace(identificationType))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "El tipo de identificación es obligatorio.",
                    SupplierImportColumns.IdentificationType
                )
            );

        if (string.IsNullOrWhiteSpace(identificationNumber))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "El número de identificación es obligatorio.",
                    SupplierImportColumns.IdentificationNumber
                )
            );

        if (string.IsNullOrWhiteSpace(legalName))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "La razón social es obligatoria.",
                    SupplierImportColumns.LegalName
                )
            );

        Guid paymentTermId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(paymentTermCode))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "MISSING_REQUIRED_FIELD",
                    "La condición de pago es obligatoria.",
                    SupplierImportColumns.PaymentTermCode
                )
            );
        }
        else
        {
            var paymentTerms = await _paymentTermRepo.ListAsync(_ctx.TenantId, null, ct);
            var match = paymentTerms.FirstOrDefault(pt =>
                string.Equals(pt.Code, paymentTermCode.Trim(), StringComparison.OrdinalIgnoreCase)
                && pt.IsActive
            );
            if (match is null)
                issues.Add(
                    new RowIssue(
                        ImportSeverity.Error,
                        "INVALID_PAYMENT_TERM",
                        $"La condición de pago '{paymentTermCode}' no existe o está inactiva.",
                        SupplierImportColumns.PaymentTermCode
                    )
                );
            else
                paymentTermId = match.Id;
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
                    SupplierImportColumns.IdentificationNumber
                )
            );
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "MISSING_CONTACT_INFO",
                    "El proveedor no tiene email ni teléfono — se importa igual, pero sin datos de contacto."
                )
            );
        }

        var parsed = new ParsedSupplierRow(
            identificationType?.Trim() ?? string.Empty,
            identificationNumber?.Trim() ?? string.Empty,
            legalName?.Trim() ?? string.Empty,
            Get(rawRow, SupplierImportColumns.TradeName),
            Get(rawRow, SupplierImportColumns.CountryCode),
            email,
            phone,
            paymentTermId,
            Get(rawRow, SupplierImportColumns.SupplierCategory),
            Get(rawRow, SupplierImportColumns.SupplierType),
            Get(rawRow, SupplierImportColumns.PrimaryGoodType),
            Get(rawRow, SupplierImportColumns.SupplierSegment)
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedSupplierRow>(parsedDataJson)!;

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
                RoleType.Supplier,
                SupplierConfig: SupplierRoleConfig.Create(parsed.PaymentTermId),
                ClassificationConfig: SupplierClassificationConfig.Create(
                    parsed.SupplierCategory,
                    parsed.SupplierType,
                    primaryGoodType: parsed.PrimaryGoodType,
                    supplierSegment: parsed.SupplierSegment
                )
            ),
            ct
        );
        if (!roleResult.IsSuccess)
        {
            // Mismo patrón de CustomerImportProcessor: el BP ya quedó creado/committeado — no hay
            // transacción cruzada entre agregados. Se reporta para revisión manual.
            return RowConfirmResult.Failed(
                $"Proveedor creado sin rol asignado, revisar manualmente: {roleResult.Error}"
            );
        }

        if (!string.IsNullOrWhiteSpace(parsed.Email) || !string.IsNullOrWhiteSpace(parsed.Phone))
        {
            await _mediator.Send(
                new CreateBpContactCommand(
                    businessPartnerId,
                    parsed.LegalName,
                    ContactRole.Purchasing,
                    Email: parsed.Email,
                    Phone: parsed.Phone
                ),
                ct
            );
        }

        return RowConfirmResult.Success(businessPartnerId);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) ? value?.Trim() : null;
}
