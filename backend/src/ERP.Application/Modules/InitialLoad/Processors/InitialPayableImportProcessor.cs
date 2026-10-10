using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Interfaces;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Cuentas por Pagar (IL-6A). Representa saldos NETOS pendientes al corte — nunca
/// reconstruye compras, gastos, pagos ni retenciones históricas, nunca crea proveedores y nunca
/// genera retención ni asiento contable.
///
/// IL-6A — Validate es fiel a lo que confirmará IL-6B (<c>AccountsPayable.CreateInitialBalance</c>).
/// Las reglas comunes con CxC Inicial (tercero con rol activo, número normalizado no duplicado,
/// emisión/vencimiento, saldo numeric(18,2), solo USD, corte = <c>Company.OpeningBalanceDate</c>,
/// sucursal activa, lote) viven en <see cref="OpeningBalanceRowRules"/>/<see cref="OpeningBalanceImportRules"/>.
/// Propio de CxP:
/// - Proveedor con rol Proveedor activo (no se exigen defaults/configuración de retención).
/// - Tipo de documento obligatorio: el código SRI real del documento histórico, activo en el catálogo
///   SRI (<c>ISriCatalogLookupRepository.GetActiveDocTypesAsync</c>, SSOT); nota de crédito, guía de
///   remisión y comprobante de retención no son deuda.
/// - Duplicados contra CxP del proveedor de cualquier origen (Compra, Gasto o saldo inicial).
///
/// IL-6B — se confirma el LOTE completo (<see cref="IBatchImportConfirmation"/>): una
/// <c>AccountsPayable</c> InitialBalance por fila, dentro de la transacción única del handler y
/// tras revalidar cada fila contra el estado actual (ver InitialPayableImportProcessor.Confirm.cs).
/// </summary>
public sealed partial class InitialPayableImportProcessor
    : IImportProcessor, IImportBatchValidator, IImportBatchScopeGuard, IBatchImportConfirmation
{
    /// <summary>
    /// Tipos SRI que no son una obligación de pago: reducen la deuda (nota de crédito), la certifican
    /// (retención) o solo amparan el traslado de mercadería (guía de remisión).
    /// </summary>
    private static readonly HashSet<string> NonPayableDocTypes =
    [
        SriDocumentTypeCodes.CreditNote,
        SriDocumentTypeCodes.RemissionGuide,
        SriDocumentTypeCodes.Withholding,
    ];

    private readonly IInitialPayableImportSheetReader _reader;
    private readonly IBusinessPartnerImportLookup _partnerLookup;
    private readonly IInitialPayableLookup _payableLookup;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly ISriCatalogLookupRepository _sriCatalog;
    private readonly ICompanyClock _clock;
    private readonly ICurrentBranch _branch;
    private readonly IOperationalContext _ctx;
    private readonly IAccountsPayableRepository _payables;
    private readonly OpeningBalanceRowRules _rules;
    private HashSet<string>? _activeSriDocTypes;

    public InitialPayableImportProcessor(
        IInitialPayableImportSheetReader reader,
        IBusinessPartnerImportLookup partnerLookup,
        IInitialPayableLookup payableLookup,
        IOpeningBalanceConstraintsReader openingBalance,
        ISriCatalogLookupRepository sriCatalog,
        ICompanyClock clock,
        ICurrentBranch branch,
        IOperationalContext ctx,
        IAccountsPayableRepository payables
    )
    {
        _reader = reader;
        _partnerLookup = partnerLookup;
        _payableLookup = payableLookup;
        _openingBalance = openingBalance;
        _sriCatalog = sriCatalog;
        _clock = clock;
        _branch = branch;
        _ctx = ctx;
        _payables = payables;
        _rules = new OpeningBalanceRowRules(OpeningBalanceImportKind.Payables, partnerLookup, openingBalance,
            clock, ctx, branch, payableLookup.GetDocumentNumbersAsync, AccountsPayable.DocumentNumberMaxLen);
    }

    public ImportType ImportType => ImportType.InitialPayables;

    public string TemplateFileName => "plantilla-cxp-inicial.xlsx";

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

        var identificationType = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.IdentificationType);
        var identificationNumber = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.IdentificationNumber);
        var documentTypeRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.DocumentType);
        var documentNumber = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.DocumentNumber);
        var issueDateRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.IssueDate);
        var dueDateRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.DueDate);
        var balanceRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.Balance);
        var currencyRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.Currency);
        var cutoffRaw = BusinessPartnerImportRules.Get(rawRow, InitialPayableImportColumns.CutoffDate);

        _rules.ValidateBranch(issues);
        var supplier = await _rules.ResolvePartyAsync(identificationType, identificationNumber, issues, ct);
        var documentType = await ValidateDocumentTypeAsync(documentTypeRaw, issues, ct);
        var (number, documentKey) = await _rules.ValidateDocumentNumberAsync(documentNumber, supplier, issues, ct);
        var cutoffDate = await _rules.ValidateCutoffDateAsync(cutoffRaw, issues, ct);
        var (issueDate, dueDate) = OpeningBalanceImportRules.ValidateDocumentDates(issueDateRaw, dueDateRaw, cutoffDate, issues);
        var balance = OpeningBalanceImportRules.ParseBalance(balanceRaw, issues);
        var currency = OpeningBalanceImportRules.ValidateCurrency(currencyRaw, OpeningBalanceImportKind.Payables, issues)
            ?? string.Empty;

        var parsed = new ParsedInitialPayableRow(
            supplier?.BusinessPartnerId ?? Guid.Empty,
            supplier?.LegalName ?? string.Empty,
            identificationType ?? string.Empty,
            supplier?.IdentificationNumber ?? identificationNumber ?? string.Empty,
            documentType ?? documentTypeRaw ?? string.Empty,
            number,
            documentKey,
            issueDate,
            dueDate,
            balance ?? 0m,
            currency,
            cutoffDate,
            _branch.BranchId
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    /// <summary>
    /// Validaciones entre filas: el mismo documento (normalizado) no se repite para el mismo
    /// proveedor y la Fecha de corte es única en el lote.
    /// </summary>
    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows)
    {
        var parsed = rows.Select(r => JsonSerializer.Deserialize<ParsedInitialPayableRow>(r.ParsedDataJson)!).ToList();
        var issues = rows.Select(r => r.Issues.ToList()).ToList();

        OpeningBalanceImportRules.FlagDuplicateDocuments(
            parsed.Select(p => (p.SupplierId, p.DocumentKey)).ToList(), OpeningBalanceImportKind.Payables, issues);
        OpeningBalanceImportRules.FlagMultipleCutoffDates(
            parsed.Select(p => p.CutoffDate).ToList(), issues, InitialPayableImportColumns.CutoffDate);

        return OpeningBalanceImportRules.WithIssues(rows, issues);
    }

    /// <summary>CxP Inicial nunca confirma fila por fila: ver InitialPayableImportProcessor.Confirm.cs.</summary>
    public Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct) =>
        Task.FromResult(RowConfirmResult.Failed("La CxP inicial se confirma por lote completo."));

    /// <inheritdoc cref="OpeningBalanceImportRules.CheckStagingBranch{TRow}"/>
    public Task<string?> CheckStagingScopeAsync(IReadOnlyList<string> parsedDataJson, CancellationToken ct) =>
        Task.FromResult(OpeningBalanceImportRules.CheckStagingBranch<ParsedInitialPayableRow>(
            parsedDataJson, r => r.BranchId, _branch.BranchId));

    /// <summary>
    /// El tipo real del documento histórico: activo en el catálogo SRI (SSOT, incluye no electrónicos
    /// porque un documento histórico puede ser físico) y que represente una deuda — nunca un tipo
    /// ficticio de "saldo inicial".
    /// </summary>
    private async Task<string?> ValidateDocumentTypeAsync(string? raw, List<RowIssue> issues, CancellationToken ct)
    {
        const string column = InitialPayableImportColumns.DocumentType;
        if (raw is null)
        {
            OpeningBalanceImportRules.AddMissing(issues, column,
                "El tipo de documento es obligatorio: código SRI del documento pendiente (p. ej. 01 Factura).");
            return null;
        }
        if (BusinessPartnerImportRules.IsLeadingZeroLost(raw))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, BusinessPartnerImportRules.LeadingZeroLostCode,
                BusinessPartnerImportRules.LeadingZeroLostMessage("Tipo de documento", raw), column));
            return null;
        }

        _activeSriDocTypes ??= (await _sriCatalog.GetActiveDocTypesAsync(ct))
            .Select(d => d.Code).ToHashSet(StringComparer.Ordinal);
        if (!_activeSriDocTypes.Contains(raw))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_TYPE_NOT_FOUND",
                $"El tipo de documento '{raw}' no existe o está inactivo en el catálogo SRI de tipos de comprobante.",
                column));
            return null;
        }
        if (NonPayableDocTypes.Contains(raw))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_TYPE_NOT_PAYABLE",
                $"El tipo de documento '{raw}' no representa una deuda con el proveedor (nota de crédito, "
                + "guía de remisión o retención). Cargue solo documentos con saldo pendiente de pago.",
                column));
            return null;
        }
        return raw;
    }
}
