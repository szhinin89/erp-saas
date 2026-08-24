using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Items.UseCases.Brands;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Único <c>IImportProcessor</c> de Catálogo de Productos — rediseño "importación inteligente"
/// (segunda vuelta de INITIAL-LOAD-ITEMS-01) sobre el mismo motor genérico que
/// <see cref="CustomerImportProcessor"/>/<see cref="SupplierImportProcessor"/>. Confirm orquesta
/// <see cref="CreateItemCommand"/> — y, cuando aplica, <see cref="CreateCategoryNodeCommand"/>/
/// <see cref="CreateBrandCommand"/> — nunca escribe directo a <c>Item</c>/<c>ItemCategoryNode</c>/
/// <c>Brand</c>.
///
/// Una sola hoja plana, columnas anchas (<see cref="ItemImportColumns"/>) — cada fila es un
/// producto principal completo (SKU, categoría, marca, hasta 3 códigos de barras, PVP,
/// proveedor+código). Ningún dato de stock/costeo/Kardex se escribe desde aquí.
///
/// DESVIACIÓN DOCUMENTADA (mantenida de la primera vuelta): <c>CreateItemCommandValidator</c> ya
/// exige Categoría/Marca/al menos un código de barras para CUALQUIER ítem — faltar cualquiera de
/// los tres sigue siendo error bloqueante aquí, nunca advertencia, para que el preview nunca
/// marque "válida" una fila que fallaría en Confirm.
///
/// Categoría/Marca se resuelven por NOMBRE (no por código) — más natural para una hoja de
/// productos donde el usuario escribe nombres de negocio, no códigos internos del catálogo. Si
/// el nombre no existe: bloquea, salvo que <c>ImportBatch.AutoCreateCatalogValues</c> esté activo,
/// en cuyo caso la fila queda como "se creará al confirmar" (advertencia informativa, no bloquea)
/// y la creación real ocurre recién en <c>ConfirmRowAsync</c> — nunca en Validate, para no dejar
/// catálogo huérfano si el usuario cancela el lote sin confirmar.
/// </summary>
public sealed class ItemImportProcessor : IImportProcessor
{
    private readonly IItemImportSheetReader _reader;
    private readonly IItemRepository _itemRepo;
    private readonly IItemTypeRepository _itemTypeRepo;
    private readonly ICategoryNodeRepository _categoryRepo;
    private readonly IItemCatalogRepository _catalogRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ISriCatalogResolver _sri;
    private readonly IOperationalContext _ctx;
    private readonly IMediator _mediator;

    public ItemImportProcessor(
        IItemImportSheetReader reader,
        IItemRepository itemRepo,
        IItemTypeRepository itemTypeRepo,
        ICategoryNodeRepository categoryRepo,
        IItemCatalogRepository catalogRepo,
        IBusinessPartnerRepository bpRepo,
        ISriCatalogResolver sri,
        IOperationalContext ctx,
        IMediator mediator
    )
    {
        _reader = reader;
        _itemRepo = itemRepo;
        _itemTypeRepo = itemTypeRepo;
        _categoryRepo = categoryRepo;
        _catalogRepo = catalogRepo;
        _bpRepo = bpRepo;
        _sri = sri;
        _ctx = ctx;
        _mediator = mediator;
    }

    public ImportType ImportType => ImportType.Items;

    public string TemplateFileName => "plantilla-catalogo-productos.xlsx";

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

        var sku = Get(rawRow, ItemImportColumns.Sku);
        var name = Get(rawRow, ItemImportColumns.Name);
        var itemTypeCode = Get(rawRow, ItemImportColumns.ItemTypeCode);
        var uomCode = Get(rawRow, ItemImportColumns.UomCode);
        var vatCode = Get(rawRow, ItemImportColumns.VatCode);
        var categoryName = Get(rawRow, ItemImportColumns.CategoryName);
        var brandName = Get(rawRow, ItemImportColumns.BrandName);
        var availableOnPosRaw = Get(rawRow, ItemImportColumns.AvailableOnPos);
        var priceRaw = Get(rawRow, ItemImportColumns.Pvp);
        var supplierQuery = Get(rawRow, ItemImportColumns.SupplierQuery);
        var supplierItemCode = Get(rawRow, ItemImportColumns.SupplierItemCode);
        var costRaw = Get(rawRow, ItemImportColumns.Cost);
        var observations = Get(rawRow, ItemImportColumns.Observations);

        if (string.IsNullOrWhiteSpace(sku))
            AddMissing(issues, ItemImportColumns.Sku, "El SKU es obligatorio.");
        else if (await _itemRepo.ExistsBySkuAsync(sku.Trim(), _ctx.TenantId, cancellationToken: ct))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "DUPLICATE_SKU",
                    $"Ya existe un ítem con SKU '{sku}'.",
                    ItemImportColumns.Sku
                )
            );

        if (string.IsNullOrWhiteSpace(name))
            AddMissing(issues, ItemImportColumns.Name, "El nombre es obligatorio.");
        else if (name.Trim().Length > 50)
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_LENGTH",
                    "El nombre no puede exceder 50 caracteres.",
                    ItemImportColumns.Name
                )
            );

        var itemTypeId = await ResolveItemTypeAsync(itemTypeCode, issues, ct);
        await ValidateUomAsync(uomCode, issues, ct);
        var resolvedVatCode = await ValidateVatAsync(vatCode, issues, ct);
        await ValidateCatalogNameAsync(
            categoryName,
            "La categoría",
            "CATEGORY",
            ItemImportColumns.CategoryName,
            autoCreateCatalogValues,
            async n =>
                (await _categoryRepo.GetAllAsync(_ctx.TenantId, includeInactive: false, ct)).Any(c =>
                    string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)
                ),
            issues
        );
        await ValidateCatalogNameAsync(
            brandName,
            "La marca",
            "BRAND",
            ItemImportColumns.BrandName,
            autoCreateCatalogValues,
            async n =>
                (await _catalogRepo.GetBrandsAsync(_ctx.TenantId, ct)).Any(b =>
                    b.IsActive && string.Equals(b.Name, n, StringComparison.OrdinalIgnoreCase)
                ),
            issues
        );

        var barcodeCodes = await ValidateBarcodesAsync(rawRow, issues, ct);

        var baseSalePrice = await ValidatePriceAsync(priceRaw, issues);

        if (!string.IsNullOrWhiteSpace(costRaw))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "COST_NOT_IMPORTED",
                    "El costo no se importa en esta versión del importador — revíselo manualmente si lo necesita.",
                    ItemImportColumns.Cost
                )
            );

        var supplierId = await ResolveSupplierAsync(supplierQuery, supplierItemCode, issues, ct);

        // Regla explícita: sin precio válido, nunca disponible en POS — independientemente de lo
        // que diga la columna de la plantilla.
        var isAvailableOnPos = baseSalePrice.HasValue && ParseBool(availableOnPosRaw);

        var parsed = new ParsedItemRow(
            sku?.Trim() ?? string.Empty,
            name?.Trim() ?? string.Empty,
            name?.Trim() ?? string.Empty,
            itemTypeId,
            uomCode?.Trim() ?? string.Empty,
            categoryName?.Trim() ?? string.Empty,
            brandName?.Trim() ?? string.Empty,
            barcodeCodes,
            resolvedVatCode,
            baseSalePrice,
            isAvailableOnPos,
            supplierId,
            supplierId.HasValue ? supplierItemCode?.Trim() : null,
            observations
        );

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedItemRow>(parsedDataJson)!;

        var categoryNodeId = await ResolveOrCreateCategoryAsync(parsed.CategoryName, ct);
        if (categoryNodeId is null)
            return RowConfirmResult.Failed($"No se pudo resolver/crear la categoría '{parsed.CategoryName}'.");

        var brandId = await ResolveOrCreateBrandAsync(parsed.BrandName, ct);
        if (brandId is null)
            return RowConfirmResult.Failed($"No se pudo resolver/crear la marca '{parsed.BrandName}'.");

        var barcodes = parsed.BarcodeCodes
            .Select((code, idx) => new CreateItemBarcodeDto(code, "Internal", idx == 0))
            .ToList();

        var supplierCodes =
            parsed.SupplierId.HasValue && !string.IsNullOrWhiteSpace(parsed.SupplierItemCode)
                ? new List<CreateItemSupplierCodeDto>
                {
                    new(parsed.SupplierId.Value, parsed.SupplierItemCode!, IsPrimary: true),
                }
                : null;

        var result = await _mediator.Send(
            new CreateItemCommand(
                parsed.SKU,
                parsed.ShortName,
                parsed.Description,
                parsed.ItemTypeId,
                parsed.DefaultUomCode,
                categoryNodeId.Value,
                brandId.Value,
                barcodes,
                SaleVatCode: parsed.SaleVatCode,
                Observations: parsed.Observations,
                SupplierCodes: supplierCodes,
                BaseSalePrice: parsed.BaseSalePrice,
                IsAvailableOnPOS: parsed.IsAvailableOnPOS
            ),
            ct
        );

        return result.IsSuccess
            ? RowConfirmResult.Success(result.Value!.Id)
            : RowConfirmResult.Failed(result.Error ?? "No se pudo crear el ítem.");
    }

    // ── Validate helpers ─────────────────────────────────────────────────────

    private async Task<Guid> ResolveItemTypeAsync(
        string? itemTypeCode,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(itemTypeCode))
        {
            AddMissing(issues, ItemImportColumns.ItemTypeCode, "El tipo de ítem es obligatorio.");
            return Guid.Empty;
        }

        var itemTypeDef = await _itemTypeRepo.GetByCodeAsync(_ctx.TenantId, itemTypeCode.Trim(), ct);
        if (itemTypeDef is null || !itemTypeDef.IsActive)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_ITEM_TYPE",
                    $"El tipo de ítem '{itemTypeCode}' no existe o está inactivo.",
                    ItemImportColumns.ItemTypeCode
                )
            );
            return Guid.Empty;
        }

        return itemTypeDef.Id;
    }

    private async Task ValidateUomAsync(string? uomCode, List<RowIssue> issues, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uomCode))
        {
            AddMissing(issues, ItemImportColumns.UomCode, "La unidad de medida base es obligatoria.");
            return;
        }

        var uoms = await _sri.ResolveUomsAsync([uomCode.Trim()], ct);
        if (!uoms.ContainsKey(uomCode.Trim()))
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_UOM",
                    $"La unidad de medida '{uomCode}' no existe en el catálogo SRI.",
                    ItemImportColumns.UomCode
                )
            );
    }

    private async Task<string?> ValidateVatAsync(
        string? vatCode,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(vatCode))
            return null;

        var vatRates = await _sri.ResolveVatRatesAsync([vatCode.Trim()], ct);
        if (!vatRates.ContainsKey(vatCode.Trim()))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "INVALID_VAT_CODE",
                    $"El código de IVA '{vatCode}' no existe en el catálogo SRI.",
                    ItemImportColumns.VatCode
                )
            );
            return null;
        }

        return vatCode.Trim();
    }

    private static async Task ValidateCatalogNameAsync(
        string? rawName,
        string label,
        string codePrefix,
        string fieldName,
        bool autoCreateCatalogValues,
        Func<string, Task<bool>> existsAsync,
        List<RowIssue> issues
    )
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            AddMissing(issues, fieldName, $"{label} es obligatoria.");
            return;
        }

        var name = rawName.Trim();
        if (await existsAsync(name))
            return;

        if (autoCreateCatalogValues)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    $"{codePrefix}_WILL_BE_CREATED",
                    $"{label} '{name}' no existe — se creará automáticamente al confirmar.",
                    fieldName
                )
            );
        }
        else
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    $"{codePrefix}_NOT_FOUND",
                    $"{label} '{name}' no existe en el catálogo. Actívala primero o habilita la creación automática.",
                    fieldName
                )
            );
        }
    }

    private async Task<IReadOnlyList<string>> ValidateBarcodesAsync(
        IReadOnlyDictionary<string, string?> rawRow,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        var raw = new[]
            {
                Get(rawRow, ItemImportColumns.Barcode1),
                Get(rawRow, ItemImportColumns.Barcode2),
                Get(rawRow, ItemImportColumns.Barcode3),
            }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .ToList();

        if (raw.Count == 0)
        {
            AddMissing(
                issues,
                ItemImportColumns.Barcode1,
                "Debe indicar al menos un código de barras (Código Barra 1/2/3)."
            );
            return [];
        }

        var distinct = raw.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count != raw.Count)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "DUPLICATE_BARCODE_IN_ROW",
                    "Los códigos de barras de la fila no pueden repetirse entre sí.",
                    ItemImportColumns.Barcode1
                )
            );
            return distinct;
        }

        var duplicatesInCatalog = new List<string>();
        foreach (var code in distinct)
        {
            if (await _itemRepo.BarcodeExistsAsync(code, _ctx.TenantId, ct))
                duplicatesInCatalog.Add(code);
        }

        if (duplicatesInCatalog.Count > 0)
            issues.Add(
                new RowIssue(
                    ImportSeverity.Error,
                    "DUPLICATE_BARCODE",
                    $"Código(s) de barras ya asignados a otro ítem: {string.Join(", ", duplicatesInCatalog)}.",
                    ItemImportColumns.Barcode1
                )
            );

        return distinct;
    }

    private static Task<decimal?> ValidatePriceAsync(string? priceRaw, List<RowIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(priceRaw))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "MISSING_SALE_PRICE",
                    "El ítem no tiene PVP — se importa igual, pero sin disponibilidad en POS.",
                    ItemImportColumns.Pvp
                )
            );
            return Task.FromResult<decimal?>(null);
        }

        if (decimal.TryParse(priceRaw, out var parsedPrice) && parsedPrice >= 0)
            return Task.FromResult<decimal?>(parsedPrice);

        // Regla explícita: PVP inválido nunca bloquea la fila — se importa el ítem base sin
        // precio (misma consecuencia que PVP ausente), solo se reporta que no se aplicó.
        issues.Add(
            new RowIssue(
                ImportSeverity.Warning,
                "PRICE_NOT_APPLIED",
                $"El PVP '{priceRaw}' no es un número válido — el ítem se importa sin precio.",
                ItemImportColumns.Pvp
            )
        );
        return Task.FromResult<decimal?>(null);
    }

    private async Task<Guid?> ResolveSupplierAsync(
        string? supplierQuery,
        string? supplierItemCode,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(supplierQuery))
            return null;

        if (string.IsNullOrWhiteSpace(supplierItemCode))
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "SUPPLIER_CODE_INCOMPLETE",
                    $"Se indicó Proveedor ('{supplierQuery}') sin Código Proveedor — no se vincula código de proveedor.",
                    ItemImportColumns.SupplierItemCode
                )
            );
            return null;
        }

        var matches = await _bpRepo.SearchAsync(
            query: supplierQuery.Trim(),
            isActive: true,
            roles: [RoleType.Supplier],
            take: 2,
            cancellationToken: ct
        );

        if (matches.Count != 1)
        {
            issues.Add(
                new RowIssue(
                    ImportSeverity.Warning,
                    "SUPPLIER_NOT_LINKED",
                    matches.Count == 0
                        ? $"No se encontró un proveedor activo que coincida con '{supplierQuery}' — el ítem se importa sin código de proveedor."
                        : $"'{supplierQuery}' coincide con más de un proveedor — el ítem se importa sin código de proveedor.",
                    ItemImportColumns.SupplierQuery
                )
            );
            return null;
        }

        return matches[0].Id;
    }

    // ── Confirm helpers (única capa que escribe catálogo) ───────────────────

    private async Task<Guid?> ResolveOrCreateCategoryAsync(string categoryName, CancellationToken ct)
    {
        var categories = await _categoryRepo.GetAllAsync(_ctx.TenantId, includeInactive: false, ct);
        var existing = categories.FirstOrDefault(c =>
            string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase)
        );
        if (existing is not null)
            return existing.Id;

        var result = await _mediator.Send(
            new CreateCategoryNodeCommand(
                ParentId: null,
                Code: DeriveCode(categoryName, 20),
                Name: categoryName,
                Description: "Creada automáticamente desde Carga Inicial — Catálogo de Productos.",
                Level: "Category"
            ),
            ct
        );

        return result.IsSuccess ? result.Value!.Id : null;
    }

    private async Task<Guid?> ResolveOrCreateBrandAsync(string brandName, CancellationToken ct)
    {
        var brands = await _catalogRepo.GetBrandsAsync(_ctx.TenantId, ct);
        var existing = brands.FirstOrDefault(b =>
            b.IsActive && string.Equals(b.Name, brandName, StringComparison.OrdinalIgnoreCase)
        );
        if (existing is not null)
            return existing.Id;

        var result = await _mediator.Send(
            new CreateBrandCommand(Code: DeriveCode(brandName, 20), Name: brandName),
            ct
        );

        return result.IsSuccess ? result.Value!.Id : null;
    }

    private static string DeriveCode(string name, int maxLen)
    {
        var upper = name.Trim().ToUpperInvariant();
        var chars = upper.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
        var code = new string(chars);
        if (string.IsNullOrEmpty(code))
            code = "CAT";
        return code.Length > maxLen ? code[..maxLen] : code;
    }

    private static void AddMissing(List<RowIssue> issues, string field, string message) =>
        issues.Add(new RowIssue(ImportSeverity.Error, "MISSING_REQUIRED_FIELD", message, field));

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var v = value.Trim();
        return string.Equals(v, "SI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "SÍ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "TRUE", StringComparison.OrdinalIgnoreCase)
            || v == "1";
    }

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) ? value?.Trim() : null;
}
