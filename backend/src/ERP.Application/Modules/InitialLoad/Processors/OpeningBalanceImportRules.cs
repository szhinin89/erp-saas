using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Modules.InitialLoad.Enums;
using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Encabezados comunes a las plantillas de saldos iniciales por documento (CxC IL-5, CxP IL-6) — SSOT
/// de los nombres que ambas comparten; cada plantilla agrega los suyos (p. ej. Tipo Documento en CxP).
/// </summary>
public static class OpeningBalanceImportColumns
{
    public const string IdentificationType = PartnerImportColumns.IdentificationType;
    public const string IdentificationNumber = PartnerImportColumns.IdentificationNumber;
    public const string DocumentNumber = "Número Documento";
    public const string IssueDate = "Fecha Emisión";
    public const string DueDate = "Fecha Vencimiento";
    public const string Balance = "Saldo Pendiente";
    public const string Currency = "Moneda";
    public const string CutoffDate = "Fecha de corte";
}

/// <summary>
/// Lo que distingue a una carga de saldos iniciales por documento en mensajes: el tercero (rol
/// existente <see cref="PartnerImportRole"/>) y cómo se nombra la deuda.
/// </summary>
public sealed record OpeningBalanceImportKind(
    PartnerImportRole Party,
    string ShortLabel,
    string BalanceSingular,
    string BalancePlural,
    string ExistingOrigins
)
{
    public static readonly OpeningBalanceImportKind Receivables = new(
        PartnerImportRole.Customer, "CxC inicial", "cuenta por cobrar", "cuentas por cobrar", "factura o saldo inicial");

    public static readonly OpeningBalanceImportKind Payables = new(
        PartnerImportRole.Supplier, "CxP inicial", "cuenta por pagar", "cuentas por pagar",
        "compra, gasto o saldo inicial");
}

/// <summary>
/// SSOT de la Carga Inicial para lo que es idéntico entre importadores: lectura invariante de fechas
/// de negocio y montos (ZH-TEMPORAL-CONTRACT-02, numeric(18,2) sin redondeo), campos obligatorios,
/// fecha de corte única por lote y presentación de issues de lote.
/// </summary>
public static partial class OpeningBalanceImportRules
{
    public const string SupportedCurrency = "USD";

    /// <summary>Fechas de negocio admitidas en plantillas (DateOnly, InvariantCulture).</summary>
    public static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];

    /// <summary>numeric(18,2): 16 dígitos enteros como máximo.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>Punto decimal invariante, sin signo ni separador de miles.</summary>
    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    public static partial Regex InvariantDecimal();

    public static void AddMissing(List<RowIssue> issues, string field, string message) =>
        issues.Add(new RowIssue(ImportSeverity.Error, "MISSING_REQUIRED_FIELD", message, field));

    // ZH-TEMPORAL-CONTRACT-02: fecha de negocio → DateOnly con formatos explícitos e InvariantCulture.
    public static DateOnly? ParseDate(string? raw, string column, string label, List<RowIssue> issues)
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

    /// <summary>
    /// Saldo pendiente: punto decimal invariante, sin signo ni separador de miles, &gt; 0 y con máximo
    /// 2 decimales (numeric(18,2)) — nunca se redondea en silencio.
    /// </summary>
    public static decimal? ParseBalance(string? raw, List<RowIssue> issues)
    {
        const string column = OpeningBalanceImportColumns.Balance;
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
        if (value > MaxAmount)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "AMOUNT_TOO_LARGE",
                $"El saldo pendiente '{raw}' excede el máximo admitido.", column));
            return null;
        }
        return value;
    }

    /// <summary>Saldo ya validado (revalidación de Confirm): mismo rango y precisión que <see cref="ParseBalance"/>.</summary>
    public static bool IsValidBalance(decimal balance) =>
        balance > 0 && balance <= MaxAmount && decimal.Round(balance, TaxAmount) == balance;

    /// <summary>Moneda obligatoria y solo USD: nunca se asume por omisión.</summary>
    public static string? ValidateCurrency(string? raw, OpeningBalanceImportKind kind, List<RowIssue> issues)
    {
        const string column = OpeningBalanceImportColumns.Currency;
        if (raw is null)
        {
            AddMissing(issues, column, $"La moneda es obligatoria (solo {SupportedCurrency}).");
            return null;
        }
        var code = raw.ToUpperInvariant();
        if (code != SupportedCurrency)
            issues.Add(new RowIssue(ImportSeverity.Error, "CURRENCY_NOT_SUPPORTED",
                $"La moneda '{raw}' no está admitida: la {kind.ShortLabel} solo se carga en {SupportedCurrency}.",
                column));
        return code;
    }

    /// <summary>Emisión ≤ corte y vencimiento ≥ emisión.</summary>
    public static (DateOnly? Issue, DateOnly? Due) ValidateDocumentDates(
        string? issueRaw, string? dueRaw, DateOnly? cutoffDate, List<RowIssue> issues)
    {
        var issueDate = ParseDate(issueRaw, OpeningBalanceImportColumns.IssueDate, "La fecha de emisión", issues);
        var dueDate = ParseDate(dueRaw, OpeningBalanceImportColumns.DueDate, "La fecha de vencimiento", issues);
        if (issueDate is { } issued && cutoffDate is { } cutoff && issued > cutoff)
            issues.Add(new RowIssue(ImportSeverity.Error, "ISSUE_DATE_AFTER_CUTOFF",
                $"La fecha de emisión {issued:yyyy-MM-dd} es posterior a la fecha de corte {cutoff:yyyy-MM-dd}: "
                + "un saldo al corte solo incluye documentos emitidos hasta esa fecha.",
                OpeningBalanceImportColumns.IssueDate));
        if (issueDate is { } from && dueDate is { } due && due < from)
            issues.Add(new RowIssue(ImportSeverity.Error, "DUE_DATE_BEFORE_ISSUE_DATE",
                $"La fecha de vencimiento {due:yyyy-MM-dd} es anterior a la fecha de emisión {from:yyyy-MM-dd}.",
                OpeningBalanceImportColumns.DueDate));
        return (issueDate, dueDate);
    }

    /// <summary>Un saldo inicial tiene una sola fecha de corte por lote (Inventario IL-4, CxC IL-5, CxP IL-6).</summary>
    public static void FlagMultipleCutoffDates(IReadOnlyList<DateOnly?> cutoffs, List<List<RowIssue>> issues, string column)
    {
        var dates = cutoffs.Where(d => d.HasValue).Select(d => d!.Value).Distinct().ToList();
        if (dates.Count <= 1)
            return;
        for (var i = 0; i < cutoffs.Count; i++)
            if (cutoffs[i].HasValue)
                issues[i].Add(new RowIssue(ImportSeverity.Error, "MULTIPLE_CUTOFF_DATES",
                    $"El archivo tiene varias fechas de corte ({FormatDates(dates)}). "
                    + "Un saldo inicial tiene una sola fecha de corte por lote.",
                    column));
    }

    /// <summary>El mismo documento (normalizado) no se repite para el mismo tercero en el archivo.</summary>
    public static void FlagDuplicateDocuments(
        IReadOnlyList<(Guid PartyId, string DocumentKey)> keys, OpeningBalanceImportKind kind, List<List<RowIssue>> issues)
    {
        foreach (var group in keys.Select((k, i) => (Key: k, Index: i))
                     .Where(e => e.Key.PartyId != Guid.Empty && e.Key.DocumentKey.Length > 0)
                     .GroupBy(e => e.Key).Where(g => g.Count() > 1))
            foreach (var entry in group)
                issues[entry.Index].Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_DOCUMENT_IN_FILE",
                    $"El mismo documento del mismo {kind.Party.Singular} aparece en otras filas del archivo "
                    + "(se comparan sin guiones, espacios ni mayúsculas).",
                    OpeningBalanceImportColumns.DocumentNumber));
    }

    /// <summary>Reaplica los issues de lote a cada fila y recalcula si bloquea.</summary>
    public static IReadOnlyList<RowValidationResult> WithIssues(
        IReadOnlyList<RowValidationResult> rows, List<List<RowIssue>> issues) =>
        rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();

    /// <summary>
    /// El lote pertenece a la sucursal activa con la que se validó; otra sucursal no puede
    /// revalidarlo, confirmarlo ni cancelarlo.
    /// </summary>
    public static string? CheckStagingBranch<TRow>(
        IReadOnlyList<string> parsedDataJson, Func<TRow, Guid> branchOf, Guid activeBranchId)
    {
        var otherBranch = parsedDataJson
            .Select(json => branchOf(JsonSerializer.Deserialize<TRow>(json)!))
            .Any(id => id != Guid.Empty && id != activeBranchId);
        return otherBranch
            ? "El lote pertenece a otra sucursal: cambie a la sucursal con la que se validó para validarlo, "
                + "confirmarlo o cancelarlo."
            : null;
    }

    public static string FormatDates(IEnumerable<DateOnly> dates) =>
        string.Join(", ", dates.Order().Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
}

/// <summary>
/// Reglas por fila con estado de lote (cachés) de las cargas de saldos iniciales por documento —
/// una instancia por processor: sucursal activa, tercero existente con su rol activo (nunca se crea),
/// número de documento normalizado (<see cref="DocumentNumberKey"/>) no duplicado contra la deuda
/// existente y fecha de corte = <c>Company.OpeningBalanceDate</c> (<see cref="IOpeningBalanceConstraintsReader"/>).
/// </summary>
public sealed class OpeningBalanceRowRules
{
    public delegate Task<IReadOnlyList<string>> ExistingDocumentNumbers(Guid partyId, CancellationToken ct);

    private readonly OpeningBalanceImportKind _kind;
    private readonly IBusinessPartnerImportLookup _partnerLookup;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly ICompanyClock _clock;
    private readonly IOperationalContext _ctx;
    private readonly ICurrentBranch _branch;
    private readonly ExistingDocumentNumbers _existingDocuments;
    private readonly int _documentNumberMaxLen;
    private readonly Dictionary<string, BusinessPartnerImportMatch?> _partners = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, HashSet<string>> _documentKeysByParty = new();
    private (bool Loaded, DateOnly? Date) _openingBalanceDate;
    private DateOnly? _companyToday;

    public OpeningBalanceRowRules(
        OpeningBalanceImportKind kind,
        IBusinessPartnerImportLookup partnerLookup,
        IOpeningBalanceConstraintsReader openingBalance,
        ICompanyClock clock,
        IOperationalContext ctx,
        ICurrentBranch branch,
        ExistingDocumentNumbers existingDocuments,
        int documentNumberMaxLen
    )
    {
        _kind = kind;
        _partnerLookup = partnerLookup;
        _openingBalance = openingBalance;
        _clock = clock;
        _ctx = ctx;
        _branch = branch;
        _existingDocuments = existingDocuments;
        _documentNumberMaxLen = documentNumberMaxLen;
    }

    public void ValidateBranch(List<RowIssue> issues)
    {
        if (_branch.BranchId == Guid.Empty)
            issues.Add(new RowIssue(ImportSeverity.Error, "BRANCH_REQUIRED",
                $"Seleccione una sucursal activa: la {_kind.ShortLabel} se registra en la sucursal activa del lote."));
    }

    public async Task<BusinessPartnerImportMatch?> ResolvePartyAsync(
        string? type, string? number, List<RowIssue> issues, CancellationToken ct)
    {
        const string typeColumn = OpeningBalanceImportColumns.IdentificationType;
        const string numberColumn = OpeningBalanceImportColumns.IdentificationNumber;
        if (type is null)
            OpeningBalanceImportRules.AddMissing(issues, typeColumn, "El tipo de identificación es obligatorio.");
        if (number is null)
            OpeningBalanceImportRules.AddMissing(issues, numberColumn, "El número de identificación es obligatorio.");
        if (type is null || number is null)
            return null;

        var party = _kind.Party;
        if (BusinessPartnerImportRules.IsConsumidorFinal(type))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "CONSUMIDOR_FINAL_NOT_ALLOWED",
                $"Consumidor Final no admite {_kind.BalancePlural}: la deuda debe ser de un {party.Singular} identificado.",
                typeColumn));
            return null;
        }
        if (BusinessPartnerImportRules.IsLeadingZeroLost(type))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, BusinessPartnerImportRules.LeadingZeroLostCode,
                BusinessPartnerImportRules.LeadingZeroLostMessage("Tipo de identificación", type), typeColumn));
            return null;
        }

        var cacheKey = type + ":" + number.ToUpperInvariant();
        if (!_partners.TryGetValue(cacheKey, out var match))
        {
            match = await _partnerLookup.FindByIdentificationAsync(type, number, party.Role, ct);
            _partners[cacheKey] = match;
        }

        var prefix = party.Role.ToString().ToUpperInvariant();
        var plural = char.ToUpperInvariant(party.Plural[0]) + party.Plural[1..];
        (string Code, string Message)? rejection = match switch
        {
            null => ($"{prefix}_NOT_FOUND", $"No existe un {party.Singular} con identificación {type}-{number}. "
                + $"Cárguelo primero en la Carga Inicial de {plural}; esta plantilla nunca crea {party.Plural}."),
            { IsAmbiguous: true } => ("AMBIGUOUS_IDENTIFICATION", "Existe más de un tercero con esta "
                + "identificación (solo difieren en mayúsculas). Corrija el maestro."),
            { IsActive: false } => ($"{prefix}_INACTIVE",
                $"El tercero {match.LegalName} está inactivo. Reactívelo antes de cargar su saldo."),
            { HasActiveRole: false, HasRevokedRole: true } => ($"{prefix}_ROLE_REVOKED",
                $"El tercero {match.LegalName} tiene el rol {party.RoleName} revocado. Reactívelo antes de cargar su saldo."),
            { HasActiveRole: false } => ($"{prefix}_ROLE_MISSING",
                $"El tercero {match.LegalName} no tiene rol {party.RoleName}. Asígnele el rol antes de cargar su saldo."),
            _ => null,
        };
        if (rejection is null)
            return match;

        issues.Add(new RowIssue(ImportSeverity.Error, rejection.Value.Code, rejection.Value.Message, numberColumn));
        return null;
    }

    public async Task<(string Number, string Key)> ValidateDocumentNumberAsync(
        string? raw, BusinessPartnerImportMatch? party, List<RowIssue> issues, CancellationToken ct)
    {
        const string column = OpeningBalanceImportColumns.DocumentNumber;
        if (raw is null)
        {
            OpeningBalanceImportRules.AddMissing(issues, column, "El número de documento es obligatorio.");
            return (string.Empty, string.Empty);
        }
        if (raw.Length > _documentNumberMaxLen)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_NUMBER_TOO_LONG",
                $"El número de documento no puede superar {_documentNumberMaxLen} caracteres.", column));
            return (raw, string.Empty);
        }

        var key = DocumentNumberKey.Normalize(raw);
        if (key.Length == 0)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_DOCUMENT_NUMBER",
                "El número de documento debe contener letras o dígitos.", column));
            return (raw, string.Empty);
        }

        if (party is not null)
        {
            if (!_documentKeysByParty.TryGetValue(party.BusinessPartnerId, out var existing))
            {
                var numbers = await _existingDocuments(party.BusinessPartnerId, ct);
                existing = numbers.Select(DocumentNumberKey.Normalize).ToHashSet(StringComparer.Ordinal);
                _documentKeysByParty[party.BusinessPartnerId] = existing;
            }
            if (existing.Contains(key))
                issues.Add(new RowIssue(ImportSeverity.Error, "DOCUMENT_ALREADY_EXISTS",
                    $"El {_kind.Party.Singular} {party.LegalName} ya tiene una {_kind.BalanceSingular} con el documento "
                    + $"'{raw}' ({_kind.ExistingOrigins}). No se duplica la deuda.",
                    column));
        }
        return (raw, key);
    }

    public async Task<DateOnly?> ValidateCutoffDateAsync(string? raw, List<RowIssue> issues, CancellationToken ct)
    {
        const string column = OpeningBalanceImportColumns.CutoffDate;
        if (raw is null)
        {
            OpeningBalanceImportRules.AddMissing(issues, column,
                "La fecha de corte es obligatoria: es la fecha a la que se reporta el saldo pendiente.");
            return null;
        }
        var date = OpeningBalanceImportRules.ParseDate(raw, column, "La fecha de corte", issues);
        if (date is null)
            return null;

        _companyToday ??= await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        if (date > _companyToday.Value)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "FUTURE_CUTOFF_DATE",
                $"La fecha de corte {date:yyyy-MM-dd} es posterior a hoy ({_companyToday.Value:yyyy-MM-dd}).",
                column));
            return null;
        }

        if (!_openingBalanceDate.Loaded)
            _openingBalanceDate = (true, await _openingBalance.GetOpeningBalanceDateAsync(ct));
        if (_openingBalanceDate.Date is not { } opening)
            issues.Add(new RowIssue(ImportSeverity.Error, "OPENING_BALANCE_DATE_NOT_SET",
                "La empresa no tiene definida su fecha de apertura de saldos. Defínala antes de cargar saldos "
                + "iniciales: todas las cargas deben usar ese mismo corte.",
                column));
        else if (date.Value != opening)
            issues.Add(new RowIssue(ImportSeverity.Error, "CUTOFF_DATE_MISMATCH",
                $"La fecha de corte {date:yyyy-MM-dd} no coincide con la fecha de apertura de saldos de la "
                + $"empresa ({opening:yyyy-MM-dd}). No se mezclan fechas de corte.",
                column));
        return date;
    }
}
