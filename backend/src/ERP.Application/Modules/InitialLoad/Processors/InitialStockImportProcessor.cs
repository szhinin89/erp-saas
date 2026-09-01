using ERP.Application.Common;
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
using System.Text.Json;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Cuarto <c>IImportProcessor</c> registrado (INITIAL-LOAD-INITIAL-STOCK-01) — archivo separado a
/// propósito del Catálogo de Productos porque afecta inventario/Kardex/costo. Confirm orquesta
/// <see cref="CreateStockAdjustmentCommand"/> → <see cref="ExecuteStockAdjustmentCommand"/> (casos
/// de uso existentes de Ajustes de Inventario) — un <c>StockAdjustment</c> Ingreso de una sola
/// línea por fila válida, nunca escribe Kardex/CurrentStock directamente. Nunca crea Ítems ni
/// Bodegas: si SKU/código de barras o Bodega no resuelven contra el catálogo ya existente, la fila
/// se bloquea.
///
/// DECISIONES DOCUMENTADAS (INITIAL-LOAD-INITIAL-STOCK-01):
/// - <b>Motivo de ajuste</b>: <c>ExecuteStockAdjustmentCommand</c> exige un <c>InventoryAdjustmentReason</c>
///   activo que permita Ingreso, y ningún tenant tiene uno por defecto. Se resuelve/crea (solo en
///   Confirm, nunca en Validate) un motivo tenant-wide de código estable <c>CARGA_INICIAL</c> vía
///   <see cref="CreateInventoryAdjustmentReasonCommand"/> — si ya existe pero inactivo o sin
///   permitir Ingreso, se reporta error explícito en vez de duplicar o forzar su uso.
/// - <b>Costo unitario es OBLIGATORIO, no advertencia</b>: <c>ExecuteStockAdjustmentCommandHandler</c>
///   ya rechaza cualquier línea de Ingreso con costo nulo o ≤ 0 (regla de dominio preexistente,
///   ajena a este importador) — sin costo válido la fila bloquea, igual que SKU/Bodega.
/// - <b>Fecha de corte no se aplica como fecha de posteo</b>: <c>ExecuteStockAdjustmentCommandHandler</c>
///   postea el movimiento con <c>DateTime.UtcNow</c> fijo, sin parámetro de fecha — no hay forma de
///   respetar una fecha retroactiva sin tocar esa infraestructura (fuera de alcance, "no tocar
///   Kardex"). La columna se valida como fecha bien formada si viene informada, pero su valor solo
///   queda registrado en las Observaciones de la línea — nunca como fecha real de posteo — y se
///   reporta con una advertencia informativa.
/// - <b>Duplicado ítem+bodega dentro del archivo</b>: se detecta con un <see cref="HashSet{T}"/> de
///   instancia poblado a medida que se validan las filas — seguro porque
///   <see cref="ValidateImportBatch.ValidateImportBatchHandler"/> reutiliza la MISMA instancia de
///   processor (ciclo de vida Scoped) para todas las filas de un lote, en un bucle siempre
///   secuencial (nunca paralelo). No es un patrón a copiar a la ligera en otro processor sin la
///   misma garantía de secuencialidad.
/// </summary>
public sealed class InitialStockImportProcessor : IImportProcessor
{
    private const string ReasonCode = "CARGA_INICIAL";
    private const string ReasonName = "Carga Inicial";

    private readonly IInitialStockImportSheetReader _reader;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IOperationalContext _ctx;
    private readonly IMediator _mediator;

    private readonly HashSet<(Guid ItemId, Guid WarehouseId)> _seenInBatch = [];

    public InitialStockImportProcessor(
        IInitialStockImportSheetReader reader,
        IItemRepository itemRepo,
        IWarehouseRepository warehouseRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _reasonRepo = reasonRepo;
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
        var warehouseName = Get(rawRow, InitialStockImportColumns.Warehouse);
        var quantityRaw = Get(rawRow, InitialStockImportColumns.Quantity);
        var unitCostRaw = Get(rawRow, InitialStockImportColumns.UnitCost);
        var cutoffDateRaw = Get(rawRow, InitialStockImportColumns.CutoffDate);
        var observation = Get(rawRow, InitialStockImportColumns.Observation);

        var item = await ResolveItemAsync(sku, barcode, issues, ct);
        var warehouse = await ResolveWarehouseAsync(warehouseName, issues, ct);
        var quantity = ValidateQuantity(quantityRaw, issues);
        var unitCost = ValidateUnitCost(unitCostRaw, issues);
        ValidateCutoffDate(cutoffDateRaw, issues);

        if (item is not null && warehouse is not null)
        {
            var key = (item.Id, warehouse.Id);
            if (!_seenInBatch.Add(key))
                issues.Add(
                    new RowIssue(
                        ImportSeverity.Error,
                        "DUPLICATE_ITEM_WAREHOUSE_IN_ROW",
                        $"Ítem '{sku ?? barcode}' y bodega '{warehouseName}' ya aparecen en otra fila de este archivo.",
                        InitialStockImportColumns.Sku
                    )
                );
        }

        var parsed = new ParsedInitialStockRow(
            item?.Id ?? Guid.Empty,
            item?.Code.ShortName ?? string.Empty,
            item?.DefaultUomCode ?? string.Empty,
            warehouse?.Id ?? Guid.Empty,
            warehouse?.Name ?? string.Empty,
            quantity ?? 0m,
            unitCost ?? 0m,
            observation
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
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
            return RowConfirmResult.Failed(createResult.Error ?? "No se pudo crear el ajuste de inventario.");

        var executeResult = await _mediator.Send(
            new ExecuteStockAdjustmentCommand(createResult.Value!.Id),
            ct
        );
        if (!executeResult.IsSuccess)
        {
            // El StockAdjustment quedó creado en Draft (agregado propio, no huérfano: sigue
            // siendo un documento real y consultable) pero sin ejecutar — no hay stock/Kardex
            // afectado. Mismo patrón de "commit parcial reportado" que Clientes/Proveedores con
            // AssignBusinessPartnerRoleCommand.
            return RowConfirmResult.Failed(
                $"Ajuste de inventario creado sin ejecutar, revisar manualmente: {executeResult.Error}"
            );
        }

        return RowConfirmResult.Success(parsed.ItemId);
    }

    // ── Validate helpers ─────────────────────────────────────────────────────

    private async Task<Item?> ResolveItemAsync(
        string? sku,
        string? barcode,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(barcode))
        {
            AddMissing(issues, InitialStockImportColumns.Sku, "Debe indicar SKU o código de barras.");
            return null;
        }

        var code = !string.IsNullOrWhiteSpace(sku) ? sku.Trim() : barcode!.Trim();
        var item = await _itemRepo.ResolveByAnyCodeAsync(code, _ctx.TenantId, ct);

        if (item is null)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "ITEM_NOT_FOUND",
                    $"No se encontró ningún ítem con SKU/código de barras '{code}'.",
                    InitialStockImportColumns.Sku
                )
            );
            return null;
        }

        if (!item.IsActive)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "ITEM_INACTIVE",
                    $"El ítem '{code}' está deshabilitado.",
                    InitialStockImportColumns.Sku
                )
            );
            return null;
        }

        if (!item.SaleConfig.IsAvailableOnPOS)
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "ITEM_NOT_AVAILABLE_ON_POS",
                    $"El ítem '{code}' no está disponible en POS — el stock se importa de todas formas.",
                    InitialStockImportColumns.Sku
                )
            );

        return item;
    }

    private async Task<Warehouse?> ResolveWarehouseAsync(
        string? warehouseName,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(warehouseName))
        {
            AddMissing(issues, InitialStockImportColumns.Warehouse, "La bodega es obligatoria.");
            return null;
        }

        var name = warehouseName.Trim();
        var warehouses = await _warehouseRepo.GetAsync(_ctx.TenantId, true, name, null, ct);
        var warehouse = warehouses.FirstOrDefault(w =>
            string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)
        );

        if (warehouse is null)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "WAREHOUSE_NOT_FOUND",
                    $"No se encontró ninguna bodega activa llamada '{name}'.",
                    InitialStockImportColumns.Warehouse
                )
            );
            return null;
        }

        return warehouse;
    }

    private static decimal? ValidateQuantity(string? quantityRaw, List<RowIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(quantityRaw))
        {
            AddMissing(issues, InitialStockImportColumns.Quantity, "La cantidad es obligatoria.");
            return null;
        }

        if (!decimal.TryParse(quantityRaw, out var quantity) || quantity <= 0)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_QUANTITY",
                    $"La cantidad '{quantityRaw}' debe ser un número mayor a cero.",
                    InitialStockImportColumns.Quantity
                )
            );
            return null;
        }

        return quantity;
    }

    private static decimal? ValidateUnitCost(string? unitCostRaw, List<RowIssue> issues)
    {
        // ExecuteStockAdjustmentCommandHandler ya exige costo > 0 para cualquier línea de
        // Ingreso (regla de dominio preexistente) — costo faltante o cero bloquea aquí también,
        // nunca es una advertencia.
        if (string.IsNullOrWhiteSpace(unitCostRaw))
        {
            AddMissing(
                issues,
                InitialStockImportColumns.UnitCost,
                "El costo unitario es obligatorio para un ingreso de inventario."
            );
            return null;
        }

        if (!decimal.TryParse(unitCostRaw, out var unitCost) || unitCost <= 0)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_UNIT_COST",
                    $"El costo unitario '{unitCostRaw}' debe ser un número mayor a cero.",
                    InitialStockImportColumns.UnitCost
                )
            );
            return null;
        }

        return unitCost;
    }

    private static void ValidateCutoffDate(string? cutoffDateRaw, List<RowIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(cutoffDateRaw))
            return;

        if (!DateTime.TryParse(cutoffDateRaw, out _))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_CUTOFF_DATE",
                    $"La fecha de corte '{cutoffDateRaw}' no es una fecha válida.",
                    InitialStockImportColumns.CutoffDate
                )
            );
            return;
        }

        issues.Add(
            new RowIssue(
                ImportSeverity.Warning,
                "CUTOFF_DATE_NOT_APPLIED",
                "La fecha de corte no se usa como fecha de posteo — el movimiento de inventario se registra con la fecha de confirmación.",
                InitialStockImportColumns.CutoffDate
            )
        );
    }

    // ── Confirm helpers ──────────────────────────────────────────────────────

    private async Task<Guid?> ResolveOrCreateReasonAsync(CancellationToken ct)
    {
        var existing = await _reasonRepo.GetByCodeAsync(_ctx.TenantId, ReasonCode, ct);
        if (existing is not null)
            return existing.IsActive && existing.AllowsMovementType(StockAdjustment.MovementTypeIngreso)
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
        row.TryGetValue(column, out var value) ? value?.Trim() : null;
}
