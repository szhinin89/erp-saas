using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using static ERP.Domain.Common.FiscalPrecision;

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
/// La confirmación (IL-5B) todavía no existe: <see cref="ConfirmRowAsync"/> rechaza y el handler
/// de confirmación bloquea este tipo de lote.
/// </summary>
public sealed partial class InitialReceivableImportProcessor
    : IImportProcessor, IImportBatchValidator, IImportBatchScopeGuard
{
    public const string SupportedCurrency = "USD";
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];

    // numeric(18,2): 16 dígitos enteros como máximo.
    private const decimal MaxBalance = 9_999_999_999_999_999.99m;

    private readonly IInitialReceivableImportSheetReader _reader;
    private readonly IBusinessPartnerImportLookup _partnerLookup;
    private readonly IInitialReceivableLookup _receivableLookup;
    private readonly ICompanyClock _clock;
    private readonly ICurrentBranch _branch;
    private readonly IOperationalContext _ctx;
    private readonly Dictionary<string, BusinessPartnerImportMatch?> _partners = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, HashSet<string>> _documentKeysByCustomer = new();
    private (bool Loaded, DateOnly? Date) _openingBalanceDate;
    private DateOnly? _companyToday;

    public InitialReceivableImportProcessor(
        IInitialReceivableImportSheetReader reader,
        IBusinessPartnerImportLookup partnerLookup,
        IInitialReceivableLookup receivableLookup,
        ICompanyClock clock,
        ICurrentBranch branch,
        IOperationalContext ctx
    )
    {
        _reader = reader;
        _partnerLookup = partnerLookup;
        _receivableLookup = receivableLookup;
        _clock = clock;
        _branch = branch;
        _ctx = ctx;
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

        var identificationType = Get(rawRow, InitialReceivableImportColumns.IdentificationType);
        var identificationNumber = Get(rawRow, InitialReceivableImportColumns.IdentificationNumber);
        var documentNumber = Get(rawRow, InitialReceivableImportColumns.DocumentNumber);
        var issueDateRaw = Get(rawRow, InitialReceivableImportColumns.IssueDate);
        var dueDateRaw = Get(rawRow, InitialReceivableImportColumns.DueDate);
        var balanceRaw = Get(rawRow, InitialReceivableImportColumns.Balance);
        var currencyRaw = Get(rawRow, InitialReceivableImportColumns.Currency);
        var cutoffRaw = Get(rawRow, InitialReceivableImportColumns.CutoffDate);

        if (_branch.BranchId == Guid.Empty)
            issues.Add(new RowIssue(ImportSeverity.Error, "BRANCH_REQUIRED",
                "Seleccione una sucursal activa: la CxC inicial se registra en la sucursal activa del lote."));

        var customer = await ResolveCustomerAsync(identificationType, identificationNumber, issues, ct);
        var (number, documentKey) = await ValidateDocumentNumberAsync(documentNumber, customer, issues, ct);

        var cutoffDate = await ValidateCutoffDateAsync(cutoffRaw, issues, ct);
        var issueDate = ParseDate(issueDateRaw, InitialReceivableImportColumns.IssueDate, "La fecha de emisión", issues);
        var dueDate = ParseDate(dueDateRaw, InitialReceivableImportColumns.DueDate, "La fecha de vencimiento", issues);
        if (issueDate is { } issued && cutoffDate is { } cutoff && issued > cutoff)
            issues.Add(new RowIssue(ImportSeverity.Error, "ISSUE_DATE_AFTER_CUTOFF",
                $"La fecha de emisión {issued:yyyy-MM-dd} es posterior a la fecha de corte {cutoff:yyyy-MM-dd}: "
                + "un saldo al corte solo incluye documentos emitidos hasta esa fecha.",
                InitialReceivableImportColumns.IssueDate));
        if (issueDate is { } from && dueDate is { } due && due < from)
            issues.Add(new RowIssue(ImportSeverity.Error, "DUE_DATE_BEFORE_ISSUE_DATE",
                $"La fecha de vencimiento {due:yyyy-MM-dd} es anterior a la fecha de emisión {from:yyyy-MM-dd}.",
                InitialReceivableImportColumns.DueDate));

        var balance = ParseBalance(balanceRaw, issues);
        var currency = ValidateCurrency(currencyRaw, issues) ?? string.Empty;

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

        foreach (var group in parsed.Select((p, i) => (Key: (p.CustomerId, p.DocumentKey), Index: i))
                     .Where(e => e.Key.CustomerId != Guid.Empty && e.Key.DocumentKey.Length > 0)
                     .GroupBy(e => e.Key).Where(g => g.Count() > 1))
            foreach (var entry in group)
                issues[entry.Index].Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_DOCUMENT_IN_FILE",
                    "El mismo documento del mismo cliente aparece en otras filas del archivo "
                    + "(se comparan sin guiones, espacios ni mayúsculas).",
                    InitialReceivableImportColumns.DocumentNumber));

        var dates = parsed.Where(p => p.CutoffDate.HasValue).Select(p => p.CutoffDate!.Value).Distinct().ToList();
        if (dates.Count > 1)
            for (var i = 0; i < parsed.Count; i++)
                if (parsed[i].CutoffDate.HasValue)
                    issues[i].Add(new RowIssue(ImportSeverity.Error, "MULTIPLE_CUTOFF_DATES",
                        $"El archivo tiene varias fechas de corte ({FormatDates(dates)}). "
                        + "Un saldo inicial tiene una sola fecha de corte por lote.",
                        InitialReceivableImportColumns.CutoffDate));

        return rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();
    }

    /// <summary>IL-5A: la confirmación de CxC Inicial (IL-5B) todavía no está disponible.</summary>
    public Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct) =>
        Task.FromResult(RowConfirmResult.Failed("La confirmación de CxC inicial todavía no está disponible."));

    /// <summary>
    /// El lote pertenece a la sucursal activa con la que se validó; otra sucursal no puede
    /// revalidarlo, confirmarlo ni cancelarlo.
    /// </summary>
    public Task<string?> CheckStagingScopeAsync(IReadOnlyList<string> parsedDataJson, CancellationToken ct)
    {
        var otherBranch = parsedDataJson
            .Select(json => JsonSerializer.Deserialize<ParsedInitialReceivableRow>(json)!.BranchId)
            .Any(id => id != Guid.Empty && id != _branch.BranchId);
        return Task.FromResult<string?>(otherBranch
            ? "El lote pertenece a otra sucursal: cambie a la sucursal con la que se validó para validarlo, "
                + "confirmarlo o cancelarlo."
            : null);
    }

    // ── Validate helpers ─────────────────────────────────────────────────────

    private async Task<BusinessPartnerImportMatch?> ResolveCustomerAsync(
        string? type, string? number, List<RowIssue> issues, CancellationToken ct)
    {
        if (type is null)
            AddMissing(issues, InitialReceivableImportColumns.IdentificationType, "El tipo de identificación es obligatorio.");
        if (number is null)
            AddMissing(issues, InitialReceivableImportColumns.IdentificationNumber, "El número de identificación es obligatorio.");
        if (type is null || number is null)
            return null;

        if (type == TaxIdentification.SriConsumidorFinal)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "CONSUMIDOR_FINAL_NOT_ALLOWED",
                "Consumidor Final no admite cuentas por cobrar: la deuda debe ser de un cliente identificado.",
                InitialReceivableImportColumns.IdentificationType));
            return null;
        }
        if (type.Length == 1 && char.IsAsciiDigit(type[0]))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "LEADING_ZERO_LOST",
                $"Tipo de identificación '{type}': probablemente Excel quitó el 0 inicial (use p. ej. 0{type}). "
                + "Formatee la columna como Texto.",
                InitialReceivableImportColumns.IdentificationType));
            return null;
        }

        var cacheKey = type + ":" + number.ToUpperInvariant();
        if (!_partners.TryGetValue(cacheKey, out var match))
        {
            match = await _partnerLookup.FindByIdentificationAsync(type, number, RoleType.Customer, ct);
            _partners[cacheKey] = match;
        }

        (string Code, string Message)? rejection = match switch
        {
            null => ("CUSTOMER_NOT_FOUND", $"No existe un cliente con identificación {type}-{number}. Cárguelo "
                + "primero en la Carga Inicial de Clientes; esta plantilla nunca crea clientes."),
            { IsAmbiguous: true } => ("AMBIGUOUS_IDENTIFICATION", "Existe más de un tercero con esta "
                + "identificación (solo difieren en mayúsculas). Corrija el maestro."),
            { IsActive: false } => ("CUSTOMER_INACTIVE",
                $"El tercero {match.LegalName} está inactivo. Reactívelo antes de cargar su saldo."),
            { HasActiveRole: false, HasRevokedRole: true } => ("CUSTOMER_ROLE_REVOKED",
                $"El tercero {match.LegalName} tiene el rol Cliente revocado. Reactívelo antes de cargar su saldo."),
            { HasActiveRole: false } => ("CUSTOMER_ROLE_MISSING",
                $"El tercero {match.LegalName} no tiene rol Cliente. Asígnele el rol antes de cargar su saldo."),
            _ => null,
        };
        if (rejection is null)
            return match;

        issues.Add(new RowIssue(ImportSeverity.Error, rejection.Value.Code, rejection.Value.Message,
            InitialReceivableImportColumns.IdentificationNumber));
        return null;
    }

    private async Task<(string Number, string Key)> ValidateDocumentNumberAsync(
        string? raw, BusinessPartnerImportMatch? customer, List<RowIssue> issues, CancellationToken ct)
    {
        if (raw is null)
        {
            AddMissing(issues, InitialReceivableImportColumns.DocumentNumber, "El número de documento es obligatorio.");
            return (string.Empty, string.Empty);
        }
        if (raw.Length > SalesReceivable.DocumentNumberMaxLen)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_NUMBER_TOO_LONG",
                $"El número de documento no puede superar {SalesReceivable.DocumentNumberMaxLen} caracteres.",
                InitialReceivableImportColumns.DocumentNumber));
            return (raw, string.Empty);
        }

        var key = SalesReceivable.NormalizeDocumentNumber(raw);
        if (key.Length == 0)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_DOCUMENT_NUMBER",
                "El número de documento debe contener letras o dígitos.", InitialReceivableImportColumns.DocumentNumber));
            return (raw, string.Empty);
        }

        if (customer is not null)
        {
            if (!_documentKeysByCustomer.TryGetValue(customer.BusinessPartnerId, out var existing))
            {
                var numbers = await _receivableLookup.GetDocumentNumbersAsync(customer.BusinessPartnerId, ct);
                existing = numbers.Select(SalesReceivable.NormalizeDocumentNumber).ToHashSet(StringComparer.Ordinal);
                _documentKeysByCustomer[customer.BusinessPartnerId] = existing;
            }
            if (existing.Contains(key))
                issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_ALREADY_EXISTS",
                    $"El cliente {customer.LegalName} ya tiene una cuenta por cobrar con el documento '{raw}' "
                    + "(factura o saldo inicial). No se duplica la deuda.",
                    InitialReceivableImportColumns.DocumentNumber));
        }
        return (raw, key);
    }

    private async Task<DateOnly?> ValidateCutoffDateAsync(string? raw, List<RowIssue> issues, CancellationToken ct)
    {
        if (raw is null)
        {
            AddMissing(issues, InitialReceivableImportColumns.CutoffDate,
                "La fecha de corte es obligatoria: es la fecha a la que se reporta el saldo pendiente.");
            return null;
        }
        var date = ParseDate(raw, InitialReceivableImportColumns.CutoffDate, "La fecha de corte", issues);
        if (date is null)
            return null;

        _companyToday ??= await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        if (date > _companyToday.Value)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "FUTURE_CUTOFF_DATE",
                $"La fecha de corte {date:yyyy-MM-dd} es posterior a hoy ({_companyToday.Value:yyyy-MM-dd}).",
                InitialReceivableImportColumns.CutoffDate));
            return null;
        }

        if (!_openingBalanceDate.Loaded)
            _openingBalanceDate = (true, await _receivableLookup.GetOpeningBalanceDateAsync(ct));
        if (_openingBalanceDate.Date is not { } opening)
            issues.Add(new RowIssue(ImportSeverity.Error, "OPENING_BALANCE_DATE_NOT_SET",
                "La empresa no tiene definida su fecha de apertura de saldos. Defínala antes de cargar saldos "
                + "iniciales: todas las cargas deben usar ese mismo corte.",
                InitialReceivableImportColumns.CutoffDate));
        else if (date.Value != opening)
            issues.Add(new RowIssue(ImportSeverity.Error, "CUTOFF_DATE_MISMATCH",
                $"La fecha de corte {date:yyyy-MM-dd} no coincide con la fecha de apertura de saldos de la "
                + $"empresa ({opening:yyyy-MM-dd}). No se mezclan fechas de corte.",
                InitialReceivableImportColumns.CutoffDate));
        return date;
    }

    // ZH-TEMPORAL-CONTRACT-02: fecha de negocio → DateOnly con formatos explícitos e InvariantCulture.
    private static DateOnly? ParseDate(string? raw, string column, string label, List<RowIssue> issues)
    {
        if (raw is null)
        {
            AddMissing(issues, column, $"{label} es obligatoria.");
            return null;
        }
        if (DateOnly.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_DATE",
            $"{label} '{raw}' no es válida (use AAAA-MM-DD).", column));
        return null;
    }

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex InvariantDecimal();

    /// <summary>
    /// Punto decimal invariante, sin signo ni separador de miles, &gt; 0 y con máximo 2 decimales
    /// (numeric(18,2)) — nunca se redondea en silencio.
    /// </summary>
    private static decimal? ParseBalance(string? raw, List<RowIssue> issues)
    {
        const string column = InitialReceivableImportColumns.Balance;
        if (raw is null)
        {
            AddMissing(issues, column, "El saldo pendiente es obligatorio.");
            return null;
        }
        if (!InvariantDecimal().IsMatch(raw)
            || !decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_NUMBER",
                $"El saldo pendiente '{raw}' no es válido: use punto decimal (p. ej. 150.75), sin separador de "
                + "miles ni signo.",
                column));
            return null;
        }
        if (value <= 0)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "NON_POSITIVE_NUMBER", "El saldo pendiente debe ser mayor a cero.", column));
            return null;
        }
        var decimals = raw.Contains('.') ? raw.Length - raw.IndexOf('.') - 1 : 0;
        if (decimals > TaxAmount)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "PRECISION_EXCEEDED",
                $"El saldo pendiente '{raw}' tiene {decimals} decimales; se admiten como máximo {TaxAmount}. "
                + "No se redondea automáticamente.",
                column));
            return null;
        }
        if (value > MaxBalance)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "AMOUNT_TOO_LARGE",
                $"El saldo pendiente '{raw}' excede el máximo admitido.", column));
            return null;
        }
        return value;
    }

    /// <summary>Moneda obligatoria y solo USD: nunca se asume por omisión.</summary>
    private static string? ValidateCurrency(string? raw, List<RowIssue> issues)
    {
        if (raw is null)
        {
            AddMissing(issues, InitialReceivableImportColumns.Currency,
                $"La moneda es obligatoria (solo {SupportedCurrency}).");
            return null;
        }
        var code = raw.ToUpperInvariant();
        if (code != SupportedCurrency)
            issues.Add(new RowIssue(ImportSeverity.Error, "CURRENCY_NOT_SUPPORTED",
                $"La moneda '{raw}' no está admitida: la CxC inicial solo se carga en {SupportedCurrency}.",
                InitialReceivableImportColumns.Currency));
        return code;
    }

    private static string FormatDates(IEnumerable<DateOnly> dates) =>
        string.Join(", ", dates.Order().Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    private static void AddMissing(List<RowIssue> issues, string field, string message) =>
        issues.Add(new RowIssue(ImportSeverity.Error, "MISSING_REQUIRED_FIELD", message, field));

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
