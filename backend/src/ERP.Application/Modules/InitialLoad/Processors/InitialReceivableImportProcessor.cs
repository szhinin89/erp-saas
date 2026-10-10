using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Cuentas por Cobrar (IL-5A). Representa saldos pendientes al corte — nunca
/// reconstruye ventas, facturas ni cobros históricos, y nunca crea clientes.
///
/// IL-5A — Validate es fiel a lo que confirmará IL-5B (<c>SalesReceivable.CreateInitialBalance</c>):
/// - Una fila = un documento pendiente de un cliente existente, activo y con rol Cliente activo.
/// - Número de documento obligatorio; no se repite (normalizado) para el mismo cliente en el
///   archivo ni contra sus CxC existentes de cualquier origen (factura o saldo inicial).
/// - Emisión ≤ corte; vencimiento ≥ emisión; saldo &gt; 0 con máximo 2 decimales (sin redondeo).
/// - Moneda obligatoria, solo USD. Fecha de corte única por lote, no futura e igual a
///   <c>Company.OpeningBalanceDate</c> (SSOT del corte de apertura; sin fecha definida = error).
/// - Sucursal = la activa del lote; otra sucursal se carga en otro lote.
///
/// IL-5B — se confirma el LOTE completo (<see cref="IBatchImportConfirmation"/>): una
/// <c>SalesReceivable</c> InitialBalance por fila, dentro de la transacción única del handler y
/// tras revalidar cada fila contra el estado actual (ver InitialReceivableImportProcessor.Confirm.cs).
/// </summary>
public sealed partial class InitialReceivableImportProcessor
    : IImportProcessor, IImportBatchValidator, IImportBatchScopeGuard, IBatchImportConfirmation
{
    public const string SupportedCurrency = OpeningBalanceImportRules.SupportedCurrency;

    private readonly IInitialReceivableImportSheetReader _reader;
    private readonly IBusinessPartnerImportLookup _partnerLookup;
    private readonly IInitialReceivableLookup _receivableLookup;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly ICompanyClock _clock;
    private readonly ICurrentBranch _branch;
    private readonly IOperationalContext _ctx;
    private readonly ISalesReceivableRepository _receivables;
    private readonly OpeningBalanceRowRules _rules;

    public InitialReceivableImportProcessor(
        IInitialReceivableImportSheetReader reader,
        IBusinessPartnerImportLookup partnerLookup,
        IInitialReceivableLookup receivableLookup,
        IOpeningBalanceConstraintsReader openingBalance,
        ICompanyClock clock,
        ICurrentBranch branch,
        IOperationalContext ctx,
        ISalesReceivableRepository receivables
    )
    {
        _reader = reader;
        _partnerLookup = partnerLookup;
        _receivableLookup = receivableLookup;
        _openingBalance = openingBalance;
        _clock = clock;
        _branch = branch;
        _ctx = ctx;
        _receivables = receivables;
        _rules = new OpeningBalanceRowRules(OpeningBalanceImportKind.Receivables, partnerLookup, openingBalance,
            clock, ctx, branch, receivableLookup.GetDocumentNumbersAsync, SalesReceivable.DocumentNumberMaxLen);
    }

    public ImportType ImportType => ImportType.InitialReceivables;

    public string TemplateFileName => "plantilla-cxc-inicial.xlsx";

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

        var identificationType = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.IdentificationType);
        var identificationNumber = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.IdentificationNumber);
        var documentNumber = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.DocumentNumber);
        var issueDateRaw = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.IssueDate);
        var dueDateRaw = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.DueDate);
        var balanceRaw = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.Balance);
        var currencyRaw = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.Currency);
        var cutoffRaw = BusinessPartnerImportRules.Get(rawRow, InitialReceivableImportColumns.CutoffDate);

        _rules.ValidateBranch(issues);
        var customer = await _rules.ResolvePartyAsync(identificationType, identificationNumber, issues, ct);
        var (number, documentKey) = await _rules.ValidateDocumentNumberAsync(documentNumber, customer, issues, ct);
        var cutoffDate = await _rules.ValidateCutoffDateAsync(cutoffRaw, issues, ct);
        var (issueDate, dueDate) = OpeningBalanceImportRules.ValidateDocumentDates(issueDateRaw, dueDateRaw, cutoffDate, issues);
        var balance = OpeningBalanceImportRules.ParseBalance(balanceRaw, issues);
        var currency = OpeningBalanceImportRules.ValidateCurrency(currencyRaw, OpeningBalanceImportKind.Receivables, issues)
            ?? string.Empty;

        var parsed = new ParsedInitialReceivableRow(
            customer?.BusinessPartnerId ?? Guid.Empty,
            customer?.LegalName ?? string.Empty,
            identificationType ?? string.Empty,
            customer?.IdentificationNumber ?? identificationNumber ?? string.Empty,
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
    /// Validaciones entre filas: el mismo documento (normalizado) no se repite para el mismo cliente
    /// y la Fecha de corte es única en el lote.
    /// </summary>
    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows)
    {
        var parsed = rows.Select(r => JsonSerializer.Deserialize<ParsedInitialReceivableRow>(r.ParsedDataJson)!).ToList();
        var issues = rows.Select(r => r.Issues.ToList()).ToList();

        OpeningBalanceImportRules.FlagDuplicateDocuments(
            parsed.Select(p => (p.CustomerId, p.DocumentKey)).ToList(), OpeningBalanceImportKind.Receivables, issues);
        OpeningBalanceImportRules.FlagMultipleCutoffDates(
            parsed.Select(p => p.CutoffDate).ToList(), issues, InitialReceivableImportColumns.CutoffDate);

        return OpeningBalanceImportRules.WithIssues(rows, issues);
    }

    /// <summary>CxC Inicial nunca confirma fila por fila: ver <see cref="ConfirmBatchAsync(Guid, IReadOnlyList{ValueTuple{int, string}}, CancellationToken)"/>.</summary>
    public Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct) =>
        Task.FromResult(RowConfirmResult.Failed("La CxC inicial se confirma por lote completo."));

    /// <summary>
    /// El lote pertenece a la sucursal activa con la que se validó; otra sucursal no puede
    /// revalidarlo, confirmarlo ni cancelarlo.
    /// </summary>
    public Task<string?> CheckStagingScopeAsync(IReadOnlyList<string> parsedDataJson, CancellationToken ct) =>
        Task.FromResult(OpeningBalanceImportRules.CheckStagingBranch<ParsedInitialReceivableRow>(
            parsedDataJson, r => r.BranchId, _branch.BranchId));
}
