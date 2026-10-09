using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Carga Inicial de Inventario (INITIAL-LOAD-INITIAL-STOCK-01, endurecido en IL-4A). Representa el
/// saldo inicial al corte — nunca compras históricas ficticias. Nunca crea Ítems ni Bodegas.
///
/// IL-4A — Validate es fiel a la confirmación:
/// - Item activo, que participa de inventario y sin lote/serie; si vienen SKU y código de barras
///   deben resolver al mismo Item.
/// - Bodega por CÓDIGO dentro de la sucursal activa (mismo criterio que ejecuta el ajuste); otra
///   sucursal se carga en otro lote cambiando de sucursal.
/// - Solo Item+Bodega sin stock ni movimientos previos — con historia se usa un ajuste normal.
/// - Cantidad y costo con punto decimal invariante, &gt; 0, sin exceder la precisión configurada
///   de la empresa (sin redondeo silencioso); cantidad entera si el Item no admite decimales.
/// - Fecha de corte obligatoria, no futura y única en todo el lote: es la fecha efectiva del saldo.
///
/// La confirmación (documento por bodega, movimiento de apertura propio con la fecha de corte,
/// transacción única) es IL-4B; hasta entonces Confirm conserva el ajuste de Ingreso por fila.
/// </summary>
public sealed partial class InitialStockImportProcessor : IImportProcessor, IImportBatchValidator
{
    private const string ReasonCode = "CARGA_INICIAL";
    private const string ReasonName = "Carga Inicial";
    private static readonly string[] CutoffDateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];

    private readonly IInitialStockImportSheetReader _reader;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IInitialStockLookup _stockLookup;
    private readonly ICompanyPrecisionPolicyProvider _precisionProvider;
    private readonly ICompanyClock _clock;
    private readonly ICurrentBranch _branch;
    private readonly IOperationalContext _ctx;
    private readonly IMediator _mediator;
    private (int Quantity, int UnitCost)? _precision;
    private DateOnly? _companyToday;

    public InitialStockImportProcessor(
        IInitialStockImportSheetReader reader,
        IItemRepository itemRepo,
        IWarehouseRepository warehouseRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IInitialStockLookup stockLookup,
        ICompanyPrecisionPolicyProvider precisionProvider,
        ICompanyClock clock,
        ICurrentBranch branch,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _reasonRepo = reasonRepo;
        _stockLookup = stockLookup;
        _precisionProvider = precisionProvider;
        _clock = clock;
        _branch = branch;
        _ctx = ctx;
        _mediator = mediator;
    }

    public ImportType ImportType => ImportType.InitialStock;

    public string TemplateFileName => "plantilla-stock-inicial.xlsx";

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

        var sku = Get(rawRow, InitialStockImportColumns.Sku);
        var barcode = Get(rawRow, InitialStockImportColumns.Barcode);
        var warehouseCode = Get(rawRow, InitialStockImportColumns.WarehouseCode);
        var quantityRaw = Get(rawRow, InitialStockImportColumns.Quantity);
        var unitCostRaw = Get(rawRow, InitialStockImportColumns.UnitCost);
        var cutoffDateRaw = Get(rawRow, InitialStockImportColumns.CutoffDate);
        var observation = Get(rawRow, InitialStockImportColumns.Observation);

        _precision ??= await LoadPrecisionAsync(ct);
        var item = await ResolveItemAsync(sku, barcode, issues, ct);
        var warehouse = await ResolveWarehouseAsync(warehouseCode, issues, ct);
        var quantity = ParseAmount(quantityRaw, InitialStockImportColumns.Quantity, "La cantidad",
            _precision.Value.Quantity, issues);
        var unitCost = ParseAmount(unitCostRaw, InitialStockImportColumns.UnitCost, "El costo unitario",
            _precision.Value.UnitCost, issues);
        var cutoffDate = await ValidateCutoffDateAsync(cutoffDateRaw, issues, ct);

        if (item is not null && quantity is { } q && !item.StockConfig.AllowDecimalQty && q != decimal.Truncate(q))
            issues.Add(new RowIssue(ImportSeverity.Error, "DECIMAL_QUANTITY_NOT_ALLOWED",
                $"El ítem '{sku ?? barcode}' no admite cantidades con decimales.", InitialStockImportColumns.Quantity));

        if (item is not null && warehouse is not null
            && await _stockLookup.HasStockHistoryAsync(item.Id, warehouse.Id, ct))
            issues.Add(new RowIssue(ImportSeverity.Error, "STOCK_HISTORY_EXISTS",
                $"El ítem '{sku ?? barcode}' ya tiene stock o movimientos en la bodega '{warehouse.Code}'. "
                + "La carga inicial solo aplica a ítem+bodega sin historia; use un ajuste de inventario normal.",
                InitialStockImportColumns.Sku));

        var parsed = new ParsedInitialStockRow(
            item?.Id ?? Guid.Empty,
            item?.Code.ShortName ?? string.Empty,
            item?.DefaultUomCode ?? string.Empty,
            warehouse?.Id ?? Guid.Empty,
            warehouse?.Code ?? warehouseCode ?? string.Empty,
            warehouse?.Name ?? string.Empty,
            quantity ?? 0m,
            unitCost ?? 0m,
            cutoffDate,
            observation
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    /// <summary>
    /// Validaciones entre filas: Item+Bodega no se repite y la Fecha de corte es única en el lote
    /// (un saldo inicial representa un único corte).
    /// </summary>
    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows)
    {
        var parsed = rows.Select(r => JsonSerializer.Deserialize<ParsedInitialStockRow>(r.ParsedDataJson)!).ToList();
        var issues = rows.Select(r => r.Issues.ToList()).ToList();

        foreach (var group in parsed.Select((p, i) => (Key: (p.ItemId, p.WarehouseId), Index: i))
                     .Where(e => e.Key.ItemId != Guid.Empty && e.Key.WarehouseId != Guid.Empty)
                     .GroupBy(e => e.Key).Where(g => g.Count() > 1))
            foreach (var entry in group)
                issues[entry.Index].Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_ITEM_WAREHOUSE_IN_FILE",
                    "El mismo ítem y bodega aparecen en otras filas del archivo.", InitialStockImportColumns.Sku));

        var dates = parsed.Where(p => p.CutoffDate.HasValue).Select(p => p.CutoffDate!.Value).Distinct().ToList();
        if (dates.Count > 1)
            for (var i = 0; i < parsed.Count; i++)
                if (parsed[i].CutoffDate.HasValue)
                    issues[i].Add(new RowIssue(ImportSeverity.Error, "MULTIPLE_CUTOFF_DATES",
                        $"El archivo tiene varias fechas de corte ({string.Join(", ", dates.Order().Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))}). "
                        + "Un saldo inicial tiene una sola fecha de corte por lote.",
                        InitialStockImportColumns.CutoffDate));

        return rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedInitialStockRow>(parsedDataJson)!;

        var reasonId = await ResolveOrCreateReasonAsync(ct);
        if (reasonId is null)
            return RowConfirmResult.Failed(
                $"El motivo de ajuste '{ReasonCode}' existe pero está inactivo o no permite Ingreso — revíselo en Configuración de Inventario."
            );

        var createResult = await _mediator.Send(
            new CreateStockAdjustmentCommand(
                parsed.WarehouseId,
                parsed.WarehouseName,
                StockAdjustment.MovementTypeIngreso,
                reasonId.Value,
                Notes: string.IsNullOrWhiteSpace(parsed.Observation)
                    ? "Carga Inicial de Stock"
                    : $"Carga Inicial de Stock — {parsed.Observation}",
                Lines:
                [
                    new CreateStockAdjustmentLineInput(
                        parsed.ItemId,
                        parsed.ItemName,
                        PackagingLevelId: null,
                        parsed.Quantity,
                        parsed.UnitCost,
                        LineNotes: null
                    ),
                ]
            ),
            ct
        );
        if (!createResult.IsSuccess)
            return RowConfirmResult.Failed(
                createResult.Error ?? "No se pudo crear el ajuste de inventario."
            );

        var executeResult = await _mediator.Send(
            new ExecuteStockAdjustmentCommand(createResult.Value!.Id),
            ct
        );
        if (!executeResult.IsSuccess)
            return RowConfirmResult.Failed(
                $"Ajuste de inventario creado sin ejecutar, revisar manualmente: {executeResult.Error}"
            );

        return RowConfirmResult.Success(parsed.ItemId);
    }

    // ── Validate helpers ─────────────────────────────────────────────────────

    private async Task<(int Quantity, int UnitCost)> LoadPrecisionAsync(CancellationToken ct)
    {
        var policy = await _precisionProvider.GetEffectiveAsync(ct);
        return (policy.QuantityDecimals, policy.UnitCostDecimals);
    }

    private async Task<Item?> ResolveItemAsync(
        string? sku, string? barcode, List<RowIssue> issues, CancellationToken ct)
    {
        if (sku is null && barcode is null)
        {
            AddMissing(issues, InitialStockImportColumns.Sku, "Debe indicar SKU o código de barras.");
            return null;
        }

        Item? bySku = null;
        if (sku is not null)
        {
            bySku = await _itemRepo.ResolveByAnyCodeAsync(sku, _ctx.TenantId, ct);
            if (bySku is null)
            {
                issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_NOT_FOUND",
                    $"No se encontró ningún ítem con SKU '{sku}'.", InitialStockImportColumns.Sku));
                return null;
            }
        }

        Item? byBarcode = null;
        if (barcode is not null)
        {
            byBarcode = await _itemRepo.ResolveByAnyCodeAsync(barcode, _ctx.TenantId, ct);
            if (byBarcode is null)
            {
                issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_NOT_FOUND",
                    $"No se encontró ningún ítem con código de barras '{barcode}'.", InitialStockImportColumns.Barcode));
                return null;
            }
        }

        if (bySku is not null && byBarcode is not null && bySku.Id != byBarcode.Id)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_CODE_MISMATCH",
                $"El SKU '{sku}' y el código de barras '{barcode}' corresponden a ítems distintos.",
                InitialStockImportColumns.Barcode));
            return null;
        }

        var item = bySku ?? byBarcode!;
        var code = sku ?? barcode;
        if (!item.IsActive)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_INACTIVE",
                $"El ítem '{code}' está deshabilitado.", InitialStockImportColumns.Sku));
            return null;
        }
        if (!item.ParticipatesInInventory)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_NOT_INVENTORIABLE",
                $"El ítem '{code}' no participa de inventario (servicio); no admite saldo inicial.",
                InitialStockImportColumns.Sku));
            return null;
        }
        if (item.StockConfig.TracksLot || item.StockConfig.TracksSeries)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_TRACKS_LOT_OR_SERIES",
                $"El ítem '{code}' controla lotes o series; la carga inicial base no los admite.",
                InitialStockImportColumns.Sku));
            return null;
        }

        if (!item.SaleConfig.IsAvailableOnPOS)
            issues.Add(new RowIssue(ImportSeverity.Warning, "ITEM_NOT_AVAILABLE_ON_POS",
                $"El ítem '{code}' no está disponible en POS — el stock se importa de todas formas.",
                InitialStockImportColumns.Sku));

        return item;
    }

    private async Task<Warehouse?> ResolveWarehouseAsync(string? code, List<RowIssue> issues, CancellationToken ct)
    {
        if (code is null)
        {
            AddMissing(issues, InitialStockImportColumns.WarehouseCode, "El código de bodega es obligatorio.");
            return null;
        }
        if (_branch.BranchId == Guid.Empty)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "BRANCH_REQUIRED",
                "Seleccione una sucursal activa: la carga inicial solo acepta bodegas de la sucursal activa.",
                InitialStockImportColumns.WarehouseCode));
            return null;
        }

        var candidates = await _warehouseRepo.GetAsync(_ctx.TenantId, null, code, _branch.BranchId, ct);
        var warehouse = candidates.SingleOrDefault(w => string.Equals(w.Code, code, StringComparison.OrdinalIgnoreCase));
        if (warehouse is null)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "WAREHOUSE_NOT_FOUND",
                $"No existe la bodega con código '{code}' en la sucursal activa. Si pertenece a otra sucursal, "
                + "cambie de sucursal y cárguela en otro lote.",
                InitialStockImportColumns.WarehouseCode));
            return null;
        }
        if (!warehouse.IsActive)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "WAREHOUSE_INACTIVE",
                $"La bodega '{code}' está inactiva.", InitialStockImportColumns.WarehouseCode));
            return null;
        }
        return warehouse;
    }

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex InvariantDecimal();

    /// <summary>
    /// Punto decimal invariante, sin signo ni separador de miles, &gt; 0 y sin exceder la precisión
    /// configurada — nunca se redondea en silencio.
    /// </summary>
    private static decimal? ParseAmount(string? raw, string column, string label, int maxDecimals, List<RowIssue> issues)
    {
        if (raw is null)
        {
            AddMissing(issues, column, $"{label} es obligatorio.");
            return null;
        }
        if (!InvariantDecimal().IsMatch(raw)
            || !decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_NUMBER",
                $"{label} '{raw}' no es válido: use punto decimal (p. ej. 3.50), sin separador de miles ni signo.",
                column));
            return null;
        }
        if (value <= 0)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "NON_POSITIVE_NUMBER", $"{label} debe ser mayor a cero.", column));
            return null;
        }
        var decimals = raw.Contains('.') ? raw.Length - raw.IndexOf('.') - 1 : 0;
        if (decimals > maxDecimals)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "PRECISION_EXCEEDED",
                $"{label} '{raw}' tiene {decimals} decimales; la empresa admite como máximo {maxDecimals}. "
                + "No se redondea automáticamente.",
                column));
            return null;
        }
        return value;
    }

    private async Task<DateOnly?> ValidateCutoffDateAsync(string? raw, List<RowIssue> issues, CancellationToken ct)
    {
        if (raw is null)
        {
            AddMissing(issues, InitialStockImportColumns.CutoffDate,
                "La fecha de corte es obligatoria: es la fecha efectiva del saldo inicial.");
            return null;
        }

        // ZH-TEMPORAL-CONTRACT-02: fecha de negocio → DateOnly con formatos explícitos e InvariantCulture.
        if (!DateOnly.TryParseExact(raw, CutoffDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_CUTOFF_DATE",
                $"La fecha de corte '{raw}' no es válida (use AAAA-MM-DD).", InitialStockImportColumns.CutoffDate));
            return null;
        }

        _companyToday ??= await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        if (date > _companyToday.Value)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "FUTURE_CUTOFF_DATE",
                $"La fecha de corte {date:yyyy-MM-dd} es posterior a hoy ({_companyToday.Value:yyyy-MM-dd}).",
                InitialStockImportColumns.CutoffDate));
            return null;
        }
        return date;
    }

    // ── Confirm helpers ──────────────────────────────────────────────────────

    private async Task<Guid?> ResolveOrCreateReasonAsync(CancellationToken ct)
    {
        var existing = await _reasonRepo.GetByCodeAsync(_ctx.TenantId, ReasonCode, ct);
        if (existing is not null)
            return
                existing.IsActive
                && existing.AllowsMovementType(StockAdjustment.MovementTypeIngreso)
                ? existing.Id
                : null;

        var created = await _mediator.Send(
            new CreateInventoryAdjustmentReasonCommand(
                CompanyId: null,
                Code: ReasonCode,
                Name: ReasonName,
                AllowedMovementType: InventoryAdjustmentReason.Ingreso,
                RequiresNotes: false,
                SortOrder: 0
            ),
            ct
        );

        return created.IsSuccess ? created.Value!.Id : null;
    }

    private static void AddMissing(List<RowIssue> issues, string field, string message) =>
        issues.Add(new RowIssue(ImportSeverity.Error, "MISSING_REQUIRED_FIELD", message, field));

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
