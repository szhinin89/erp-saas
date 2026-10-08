using System.Globalization;
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
using FluentValidation;

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
/// Contrato vigente: <c>CreateItemCommandValidator</c>
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
public sealed class ItemImportProcessor : IImportProcessor, IImportBatchValidator
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
        var saleVatCode = Get(rawRow, ItemImportColumns.SaleVatCode);
        var purchaseVatCode = Get(rawRow, ItemImportColumns.PurchaseVatCode);
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
        var resolvedSaleVatCode = await ValidateVatAsync(saleVatCode, ItemImportColumns.SaleVatCode, issues, ct);
        var resolvedPurchaseVatCode = await ValidateVatAsync(purchaseVatCode, ItemImportColumns.PurchaseVatCode, issues, ct);
        await ValidateCatalogNameAsync(
            categoryName,
            "La categoría",
            "CATEGORY",
            ItemImportColumns.CategoryName,
            autoCreateCatalogValues,
            async n =>
                (await _categoryRepo.GetAllAsync(_ctx.TenantId, includeInactive: false, ct)).Any(
                    c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)
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

        var barcodes = await ValidateBarcodesAsync(rawRow, issues, ct);

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

        var isAvailableOnPos = string.Equals(availableOnPosRaw, "SI", StringComparison.OrdinalIgnoreCase);
        if (!isAvailableOnPos && !string.Equals(availableOnPosRaw, "NO", StringComparison.OrdinalIgnoreCase))
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_POS", "Disponible POS debe indicar SI o NO.", ItemImportColumns.AvailableOnPos));
        if (isAvailableOnPos && (!baseSalePrice.HasValue || baseSalePrice <= 0))
            issues.Add(new RowIssue(ImportSeverity.Error, "POS_REQUIRES_PRICE", "Disponible POS=SI exige un PVP válido mayor que cero.", ItemImportColumns.Pvp));

        if (supplierId.HasValue && !string.IsNullOrWhiteSpace(supplierItemCode)
            && await _itemRepo.SupplierCodeExistsAsync(supplierId.Value, supplierItemCode, _ctx.TenantId, ct))
            issues.Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_SUPPLIER_CODE", "El código ya está asignado a otro ítem para este proveedor.", ItemImportColumns.SupplierItemCode));

        var category = (await _categoryRepo.GetAllAsync(_ctx.TenantId, false, ct)).FirstOrDefault(c =>
            string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is not null && (await _categoryRepo.HasActiveChildrenAsync(category.Id, ct)
            || await _categoryRepo.AnyAncestorDisabledAsync(category.Id, ct)))
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_CATEGORY", "La categoría debe ser un nodo hoja de una rama activa.", ItemImportColumns.CategoryName));

        var parsed = new ParsedItemRow(
            sku?.Trim() ?? string.Empty,
            name?.Trim() ?? string.Empty,
            name?.Trim() ?? string.Empty,
            itemTypeId,
            uomCode?.Trim() ?? string.Empty,
            categoryName?.Trim() ?? string.Empty,
            brandName?.Trim() ?? string.Empty,
            barcodes,
            resolvedSaleVatCode,
            resolvedPurchaseVatCode,
            baseSalePrice,
            isAvailableOnPos,
            supplierId,
            supplierId.HasValue ? supplierItemCode?.Trim() : null,
            observations
        );

        ValidateItemContract(parsed, rawRow, issues);

        var hasBlockingIssue = issues.Any(i => i.Severity == ImportSeverity.Error);
        return new RowValidationResult(JsonSerializer.Serialize(parsed), hasBlockingIssue, issues);
    }

    public async Task<RowConfirmResult> ConfirmRowAsync(string parsedDataJson, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ParsedItemRow>(parsedDataJson)!;

        var categoryNodeId = await ResolveOrCreateCategoryAsync(parsed.CategoryName, ct);
        if (categoryNodeId is null)
            return RowConfirmResult.Failed(
                $"No se pudo resolver/crear la categoría '{parsed.CategoryName}'."
            );

        var brandId = await ResolveOrCreateBrandAsync(parsed.BrandName, ct);
        if (brandId is null)
            return RowConfirmResult.Failed(
                $"No se pudo resolver/crear la marca '{parsed.BrandName}'."
            );

        var barcodes = parsed
            .Barcodes.Select(
                (barcode, idx) => new CreateItemBarcodeDto(barcode.Code, barcode.BarcodeType, idx == 0)
            )
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
                PurchaseVatCode: parsed.PurchaseVatCode,
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

        var itemTypeDef = await _itemTypeRepo.GetByCodeAsync(
            _ctx.TenantId,
            itemTypeCode.Trim(),
            ct
        );
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

    private async Task ValidateUomAsync(
        string? uomCode,
        List<RowIssue> issues,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(uomCode))
        {
            AddMissing(
                issues,
                ItemImportColumns.UomCode,
                "La unidad de medida base es obligatoria."
            );
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
        string fieldName,
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
                    fieldName
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
        if (name.Length > 120)
        {
            issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_LENGTH",
                $"{label} no puede exceder 120 caracteres.", fieldName));
            return;
        }
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

    private async Task<IReadOnlyList<ParsedItemBarcode>> ValidateBarcodesAsync(
        IReadOnlyDictionary<string, string?> rawRow, List<RowIssue> issues, CancellationToken ct)
    {
        var columns = new[]
        {
            (ItemImportColumns.Barcode1, ItemImportColumns.BarcodeType1),
            (ItemImportColumns.Barcode2, ItemImportColumns.BarcodeType2),
            (ItemImportColumns.Barcode3, ItemImportColumns.BarcodeType3),
        };
        var barcodes = new List<ParsedItemBarcode>();
        foreach (var (codeColumn, typeColumn) in columns)
        {
            var code = Get(rawRow, codeColumn);
            var type = Get(rawRow, typeColumn);
            if (string.IsNullOrWhiteSpace(code))
            {
                if (!string.IsNullOrWhiteSpace(type))
                    AddMissing(issues, codeColumn, "Un tipo de barcode requiere su código.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(type))
                AddMissing(issues, typeColumn, "El tipo de código de barras es obligatorio.");
            else if (!await _catalogRepo.BarcodeTypeExistsAndActiveAsync(type, ct))
                issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_BARCODE_TYPE",
                    "El tipo de código de barras no existe o está inactivo.", typeColumn));
            if (await _itemRepo.BarcodeExistsAsync(code, _ctx.TenantId, _ctx.CompanyId, ct))
                issues.Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_BARCODE",
                    "El código de barras ya está asignado a otro ítem.", codeColumn));
            barcodes.Add(new ParsedItemBarcode(code, type ?? string.Empty));
        }
        if (barcodes.Count == 0)
            AddMissing(issues, ItemImportColumns.Barcode1, "Debe indicar al menos un código de barras.");
        if (barcodes.Select(b => Normalize(b.Code)).Distinct().Count() != barcodes.Count)
            issues.Add(new RowIssue(ImportSeverity.Error, "DUPLICATE_BARCODE_IN_ROW",
                "Los códigos de barras de la fila no pueden repetirse.", ItemImportColumns.Barcode1));
        return barcodes;
    }

    private static Task<decimal?> ValidatePriceAsync(string? priceRaw, List<RowIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(priceRaw))
            return Task.FromResult<decimal?>(null);
        if (decimal.TryParse(priceRaw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out var price) && price >= 0)
            return Task.FromResult<decimal?>(price);
        issues.Add(new RowIssue(ImportSeverity.Error, "INVALID_PRICE",
            "El PVP debe ser un número no negativo, con punto decimal y sin separador de miles.", ItemImportColumns.Pvp));
        return Task.FromResult<decimal?>(null);
    }

    private static void ValidateItemContract(ParsedItemRow row, IReadOnlyDictionary<string, string?> rawRow, List<RowIssue> issues)
    {
        // Reutiliza el contrato CLOSED sin escribir ni alterar su validador.
        // Categoría/Marca se validan por nombre y catálogo arriba, sin IDs ficticios.
        var command = new CreateItemCommand(row.SKU, row.ShortName, row.Description,
            row.ItemTypeId, row.DefaultUomCode, null, null,
            row.Barcodes.Select((b, i) => new CreateItemBarcodeDto(b.Code, b.BarcodeType, i == 0)).ToList(),
            SaleVatCode: row.SaleVatCode, PurchaseVatCode: row.PurchaseVatCode,
            Observations: row.Observations, BaseSalePrice: row.BaseSalePrice,
            SupplierCodes: row.SupplierId.HasValue && row.SupplierItemCode is not null
                ? [new CreateItemSupplierCodeDto(row.SupplierId.Value, row.SupplierItemCode, true)] : null,
            IsAvailableOnPOS: row.IsAvailableOnPOS);
        foreach (var error in new CreateItemCommandValidator().Validate(command, options => options.IncludeProperties(
            nameof(CreateItemCommand.SKU), nameof(CreateItemCommand.ShortName), nameof(CreateItemCommand.Description),
            nameof(CreateItemCommand.ItemTypeId), nameof(CreateItemCommand.DefaultUomCode),
            nameof(CreateItemCommand.SaleVatCode), nameof(CreateItemCommand.PurchaseVatCode),
            nameof(CreateItemCommand.Observations), nameof(CreateItemCommand.BaseSalePrice),
            nameof(CreateItemCommand.Barcodes), nameof(CreateItemCommand.SupplierCodes), nameof(CreateItemCommand.Nature)
        )).Errors)
        {
            var field = error.PropertyName switch
            {
                "SKU" => ItemImportColumns.Sku,
                "ShortName" or "Description" => ItemImportColumns.Name,
                "ItemTypeId" => ItemImportColumns.ItemTypeCode,
                "DefaultUomCode" => ItemImportColumns.UomCode,
                "SaleVatCode" => ItemImportColumns.SaleVatCode,
                "PurchaseVatCode" => ItemImportColumns.PurchaseVatCode,
                "Observations" => ItemImportColumns.Observations,
                "BaseSalePrice" => ItemImportColumns.Pvp,
                _ when error.PropertyName.StartsWith("Barcodes[", StringComparison.Ordinal) => BarcodeField(error.PropertyName, rawRow),
                _ when error.PropertyName.StartsWith("SupplierCodes") => ItemImportColumns.SupplierItemCode,
                _ => ItemImportColumns.Barcode1,
            };
            if (!issues.Any(i => i.Severity == ImportSeverity.Error && i.FieldName == field))
                issues.Add(new RowIssue(ImportSeverity.Error, "ITEM_CONTRACT_INVALID", error.ErrorMessage, field));
        }
    }

    private static string BarcodeField(string property, IReadOnlyDictionary<string, string?> rawRow)
    {
        var columns = new[]
        {
            (Code: ItemImportColumns.Barcode1, Type: ItemImportColumns.BarcodeType1),
            (Code: ItemImportColumns.Barcode2, Type: ItemImportColumns.BarcodeType2),
            (Code: ItemImportColumns.Barcode3, Type: ItemImportColumns.BarcodeType3),
        }.Where(c => !string.IsNullOrWhiteSpace(Get(rawRow, c.Code))).ToList();
        var index = int.Parse(property.Split('[', ']')[1], CultureInfo.InvariantCulture);
        return property.EndsWith("BarcodeType", StringComparison.Ordinal) ? columns[index].Type : columns[index].Code;
    }

    public IReadOnlyList<RowValidationResult> ValidateBatch(IReadOnlyList<RowValidationResult> rows)
    {
        var parsed = rows.Select(r => JsonSerializer.Deserialize<ParsedItemRow>(r.ParsedDataJson)!).ToList();
        var issues = rows.Select(r => r.Issues.ToList()).ToList();
        void Check(IEnumerable<(string Key, int Row)> entries, string code, string field)
        {
            foreach (var group in entries.Where(e => e.Key.Length > 0).GroupBy(e => e.Key))
            {
                var affected = group.Select(e => e.Row).Distinct().ToList();
                if (affected.Count < 2) continue;
                foreach (var index in affected)
                    issues[index].Add(new RowIssue(ImportSeverity.Error, code,
                        "Valor duplicado entre filas del archivo.", field));
            }
        }
        Check(parsed.Select((r, i) => (Normalize(r.SKU), i)), "DUPLICATE_SKU_IN_FILE", ItemImportColumns.Sku);
        Check(parsed.SelectMany((r, i) => r.Barcodes.Select(b => (Normalize(b.Code), i))),
            "DUPLICATE_BARCODE_IN_FILE", ItemImportColumns.Barcode1);
        Check(parsed.Select((r, i) => (r.SupplierId.HasValue && !string.IsNullOrWhiteSpace(r.SupplierItemCode)
            ? r.SupplierId.Value.ToString() + ":" + Normalize(r.SupplierItemCode) : string.Empty, i)),
            "DUPLICATE_SUPPLIER_CODE_IN_FILE", ItemImportColumns.SupplierItemCode);
        return rows.Select((r, i) => r with
        {
            Issues = issues[i], HasBlockingIssue = issues[i].Any(x => x.Severity == ImportSeverity.Error),
        }).ToList();
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

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

    private async Task<Guid?> ResolveOrCreateCategoryAsync(
        string categoryName,
        CancellationToken ct
    )
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

    private static string? Get(IReadOnlyDictionary<string, string?> row, string column) =>
        row.TryGetValue(column, out var value) ? value?.Trim() : null;
}
