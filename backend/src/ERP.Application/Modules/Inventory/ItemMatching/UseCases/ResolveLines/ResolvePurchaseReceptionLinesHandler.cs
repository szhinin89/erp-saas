using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;

/// <summary>
/// COMPRAS-METODO-ZH-01B — orquesta, sin motores propios: los Items se crean con
/// <see cref="CreateItemCommand"/>, sus presentaciones con <see cref="ReplaceItemPackagingLevelsCommand"/>
/// y cada vínculo línea↔Item + equivalencia de proveedor con <see cref="IItemMatchConfirmationService"/>
/// (mismo efecto que la vinculación individual). Primero valida TODO el lote y devuelve todos los
/// errores por fila; solo si no hay ninguno ejecuta dentro de una única transacción.
/// </summary>
public sealed class ResolvePurchaseReceptionLinesHandler(
    IPurchaseReceptionDocumentRepository documents,
    IItemRepository items,
    IItemMatchConfirmationService confirmation,
    IPurchaseReceptionAutoMatcher autoMatcher,
    IEnumerable<IValidator<CreateItemCommand>> createItemValidators,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    IRuntimePermissionAuthorizer authorizer,
    ICurrentTenant tenant,
    ICurrentCompany company,
    ICurrentBranch branch,
    ICurrentUser user,
    IDatabaseExceptionTranslator databaseExceptions
)
    : IRequestHandler<
        ResolvePurchaseReceptionLinesCommand,
        Result<ResolvePurchaseReceptionLinesResultDto>
    >
{
    private const string BasePresentationName = "Unidad";

    public async Task<Result<ResolvePurchaseReceptionLinesResultDto>> Handle(
        ResolvePurchaseReceptionLinesCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (cmd.Lines.Count == 0)
            return Result<ResolvePurchaseReceptionLinesResultDto>.ValidationFailure(
                "Debe incluir al menos una línea para resolver."
            );

        var document = await documents.GetByLineIdAsync(
            tenant.TenantId,
            cmd.Lines[0].PurchaseReceptionLineId,
            cancellationToken
        );
        if (
            document is null
            || document.CompanyId != company.CompanyId
            || document.BranchId != branch.BranchId
        )
            return Result<ResolvePurchaseReceptionLinesResultDto>.NotFound(
                "El documento de recepción no existe."
            );
        if (
            document.Status
            is not (
                PurchaseReceptionDocumentStatus.Verified
                or PurchaseReceptionDocumentStatus.Processed
            )
        )
            return Result<ResolvePurchaseReceptionLinesResultDto>.ValidationFailure(
                "Solo se pueden resolver líneas de un documento con XML autorizado."
            );

        if (
            cmd.NewItems.Count > 0
            && !await authorizer.IsAuthorizedAsync(
                InventoryPermissions.ItemsCreate,
                user.UserId,
                user.Role ?? string.Empty,
                cancellationToken
            )
        )
            return Result<ResolvePurchaseReceptionLinesResultDto>.Forbidden(
                "No tiene permiso para crear productos."
            );

        var errors = await ValidateAsync(document, cmd, cancellationToken);
        if (errors.Count > 0)
            return Rejected(errors);

        var outcome = new ExecutionOutcome();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                ct => ApplyAsync(document, cmd, outcome, ct),
                cancellationToken
            );
        }
        catch (RowFailureException ex)
        {
            unitOfWork.ClearChangeTracker();
            return Rejected([ex.Error]);
        }
        catch (ValidationException ex)
        {
            unitOfWork.ClearChangeTracker();
            return Rejected([
                new ResolveReceptionRowError(
                    outcome.CurrentLineId,
                    outcome.CurrentItemKey,
                    string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct())
                ),
            ]);
        }
        catch (Exception ex) when (databaseExceptions.TryGetUniqueViolation(ex, out _))
        {
            unitOfWork.ClearChangeTracker();
            return Rejected([
                new ResolveReceptionRowError(
                    outcome.CurrentLineId,
                    outcome.CurrentItemKey,
                    "Otro proceso registró el producto o código de proveedor. Actualice la factura y revise esta fila."
                ),
            ]);
        }

        return Result<ResolvePurchaseReceptionLinesResultDto>.Success(
            new ResolvePurchaseReceptionLinesResultDto(
                true,
                [],
                outcome.Lines,
                outcome.ItemsCreated,
                cmd.Lines.Count,
                outcome.EquivalencesLearned,
                outcome.LinesAutoMatched
            )
        );
    }

    private static Result<ResolvePurchaseReceptionLinesResultDto> Rejected(
        IReadOnlyList<ResolveReceptionRowError> errors
    ) =>
        Result<ResolvePurchaseReceptionLinesResultDto>.Success(
            new ResolvePurchaseReceptionLinesResultDto(false, errors, [], 0, 0, 0, 0)
        );

    // ── Validación completa del lote (nada se persiste aquí) ─────────────────────────────────

    private async Task<List<ResolveReceptionRowError>> ValidateAsync(
        PurchaseReceptionDocument document,
        ResolvePurchaseReceptionLinesCommand cmd,
        CancellationToken ct
    )
    {
        var errors = new List<ResolveReceptionRowError>();
        var newItemsByKey = new Dictionary<string, ResolveReceptionNewItemInput>(
            StringComparer.Ordinal
        );
        foreach (var newItem in cmd.NewItems)
        {
            if (
                string.IsNullOrWhiteSpace(newItem.Key)
                || !newItemsByKey.TryAdd(newItem.Key, newItem)
            )
                errors.Add(
                    new(null, newItem.Key, "Cada producto nuevo debe tener una referencia única.")
                );
        }

        var seenLines = new HashSet<Guid>();
        var targetsByCode = new Dictionary<string, (string Target, Guid LineId)>(
            StringComparer.Ordinal
        );
        foreach (var input in cmd.Lines)
        {
            var lineError = await ValidateLineAsync(document, input, newItemsByKey, seenLines, ct);
            if (lineError is not null)
            {
                errors.Add(new(input.PurchaseReceptionLineId, input.NewItemKey, lineError));
                continue;
            }

            // Un mismo código de proveedor = una sola equivalencia (Item + presentación).
            var line = document.Lines.Single(l => l.Id == input.PurchaseReceptionLineId);
            if (string.IsNullOrWhiteSpace(line.SupplierCode))
                continue;
            var target = input.ItemId is { } existing
                ? $"item:{existing}:{input.PackagingLevelId}"
                : $"new:{input.NewItemKey}:{input.PresentationFactor}";
            var normalizedCode = line.SupplierCode.Trim().ToUpperInvariant();
            if (
                targetsByCode.TryGetValue(normalizedCode, out var previous)
                && previous.Target != target
            )
                errors.Add(
                    new(
                        input.PurchaseReceptionLineId,
                        input.NewItemKey,
                        $"El código de proveedor '{line.SupplierCode}' aparece en varias líneas con productos o presentaciones distintas."
                    )
                );
            else
                targetsByCode[normalizedCode] = (target, input.PurchaseReceptionLineId);
        }

        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var barcodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var newItem in newItemsByKey.Values)
        {
            var itemLines = cmd.Lines.Where(l => l.NewItemKey == newItem.Key).ToList();
            foreach (
                var message in await ValidateNewItemAsync(newItem, itemLines, skus, barcodes, ct)
            )
                errors.Add(new(null, newItem.Key, message));
        }
        return errors;
    }

    private async Task<string?> ValidateLineAsync(
        PurchaseReceptionDocument document,
        ResolveReceptionLineInput input,
        IReadOnlyDictionary<string, ResolveReceptionNewItemInput> newItemsByKey,
        HashSet<Guid> seenLines,
        CancellationToken ct
    )
    {
        var line = document.Lines.FirstOrDefault(l => l.Id == input.PurchaseReceptionLineId);
        if (line is null)
            return "La línea no pertenece a este documento de recepción.";
        if (!seenLines.Add(line.Id))
            return "La línea está repetida en el lote.";
        if (line.ItemId is not null)
            return "La línea ya está resuelta. Actualice la factura antes de continuar.";
        if (input.ItemId.HasValue == !string.IsNullOrWhiteSpace(input.NewItemKey))
            return "Indique un producto existente o un producto nuevo (solo uno).";

        var registered =
            document.SupplierId is { } supplierId && !string.IsNullOrWhiteSpace(line.SupplierCode)
                ? await items.GetSupplierCodeMatchAsync(
                    supplierId,
                    line.SupplierCode,
                    document.TenantId,
                    ct
                )
                : null;

        if (input.ItemId is { } itemId)
        {
            var item = await items.GetByIdLightAsync(itemId, document.TenantId, ct);
            if (item is null || !item.IsActive)
                return "El producto seleccionado no existe o está deshabilitado.";
            if (registered is not null && registered.ItemId != itemId)
                return "El código de proveedor ya está asociado a otro ítem.";
            if (
                input.PackagingLevelId is { } packagingLevelId
                && !await items.PackagingLevelBelongsToItemAsync(
                    itemId,
                    packagingLevelId,
                    document.TenantId,
                    ct
                )
            )
                return "La presentación seleccionada no pertenece al ítem.";
            if (
                item.ParticipatesInInventory
                && input.PackagingLevelId is null
                && registered?.PackagingLevelId is null
            )
                return "Seleccione la presentación que entrega el proveedor para este producto inventariable.";
            return null;
        }

        if (!newItemsByKey.ContainsKey(input.NewItemKey!))
            return "El producto nuevo indicado no existe en el lote.";
        if (registered is not null)
            return "El código de proveedor ya está vinculado a un producto existente. Use \"Vincular\" en lugar de crear.";
        if (input.PresentationFactor <= 0)
            return "La cantidad por presentación debe ser mayor a cero.";
        if (
            input.PresentationFactor != 1m
            && (
                string.IsNullOrWhiteSpace(input.PresentationName)
                || string.IsNullOrWhiteSpace(input.PresentationUomCode)
            )
        )
            return "Indique nombre y unidad de la presentación (p. ej. Caja x12).";
        return null;
    }

    private async Task<List<string>> ValidateNewItemAsync(
        ResolveReceptionNewItemInput newItem,
        IReadOnlyList<ResolveReceptionLineInput> itemLines,
        HashSet<string> skus,
        HashSet<string> barcodes,
        CancellationToken ct
    )
    {
        var messages = new List<string>();
        if (itemLines.Count == 0)
            messages.Add("El producto nuevo no está asignado a ninguna línea.");
        if (newItem.BaseSalePrice <= 0)
            messages.Add("El precio de venta debe ser mayor a cero.");

        var command = ToCreateItemCommand(newItem);
        foreach (var validator in createItemValidators)
        {
            var result = await validator.ValidateAsync(command, ct);
            messages.AddRange(result.Errors.Select(e => e.ErrorMessage));
        }

        var sku = newItem.Sku.Trim();
        if (sku.Length > 0 && !skus.Add(sku))
            messages.Add($"El SKU '{sku}' está repetido en el lote.");
        else if (
            sku.Length > 0
            && await items.ExistsBySkuAsync(sku, tenant.TenantId, cancellationToken: ct)
        )
            messages.Add($"Ya existe un ítem con SKU '{sku}'.");

        var barcode = newItem.Barcode.Trim();
        if (barcode.Length > 0 && !barcodes.Add(barcode))
            messages.Add($"El código de barras '{barcode}' está repetido en el lote.");
        else if (barcode.Length > 0 && await items.BarcodeExistsAsync(barcode, tenant.TenantId, company.CompanyId, ct))
            messages.Add($"El código de barras '{barcode}' ya está asignado a otro ítem.");

        var conflictingFactor = itemLines
            .Where(l => l.PresentationFactor != 1m)
            .GroupBy(l => l.PresentationFactor)
            .FirstOrDefault(g => g.Select(l => (Name(l), Uom(l))).Distinct().Count() > 1);
        if (conflictingFactor is not null)
            messages.Add(
                $"La presentación de {conflictingFactor.Key} unidades tiene nombres o unidades distintas entre líneas."
            );
        return messages.Distinct().ToList();
    }

    // ── Ejecución atómica ────────────────────────────────────────────────────────────────────

    private async Task ApplyAsync(
        PurchaseReceptionDocument document,
        ResolvePurchaseReceptionLinesCommand cmd,
        ExecutionOutcome outcome,
        CancellationToken ct
    )
    {
        var levelsByNewItem =
            new Dictionary<string, (Guid ItemId, IReadOnlyDictionary<decimal, Guid> Levels)>();
        foreach (var newItem in cmd.NewItems)
        {
            outcome.CurrentItemKey = newItem.Key;
            var created = await mediator.Send(ToCreateItemCommand(newItem), ct);
            if (!created.IsSuccess || created.Value is null)
                throw new RowFailureException(
                    new(null, newItem.Key, created.Error ?? "No se pudo crear el producto.")
                );

            var itemLines = cmd.Lines.Where(l => l.NewItemKey == newItem.Key).ToList();
            var packaging = await mediator.Send(
                new ReplaceItemPackagingLevelsCommand(
                    created.Value.Id,
                    BuildPresentations(newItem, itemLines)
                ),
                ct
            );
            if (!packaging.IsSuccess || packaging.Value is null)
                throw new RowFailureException(
                    new(
                        null,
                        newItem.Key,
                        packaging.Error ?? "No se pudieron crear las presentaciones."
                    )
                );

            levelsByNewItem[newItem.Key] = (
                created.Value.Id,
                packaging.Value.PackagingLevels.ToDictionary(p => p.BaseQuantity, p => p.Id)
            );
            outcome.ItemsCreated++;
        }

        var matchedAt = DateTime.UtcNow;
        var learnedCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in cmd.Lines)
        {
            outcome.CurrentItemKey = input.NewItemKey;
            outcome.CurrentLineId = input.PurchaseReceptionLineId;
            var line = document.Lines.Single(l => l.Id == input.PurchaseReceptionLineId);
            var (itemId, packagingLevelId) = input.ItemId is { } existingItemId
                ? (existingItemId, input.PackagingLevelId)
                : (
                    levelsByNewItem[input.NewItemKey!].ItemId,
                    (Guid?)levelsByNewItem[input.NewItemKey!].Levels[input.PresentationFactor]
                );

            var learns =
                document.SupplierId is { } supplierId
                && !string.IsNullOrWhiteSpace(line.SupplierCode)
                && (
                    learnedCodes.Contains(line.SupplierCode)
                    || !await items.SupplierCodeExistsAsync(
                        supplierId,
                        line.SupplierCode.Trim().ToUpperInvariant(),
                        document.TenantId,
                        ct
                    )
                );
            // Recheck after product creation: another batch may have learned this code meanwhile.
            var registered =
                document.SupplierId is { } supplier
                && !string.IsNullOrWhiteSpace(line.SupplierCode)
                && await items.SupplierCodeExistsAsync(
                    supplier,
                    line.SupplierCode.Trim().ToUpperInvariant(),
                    document.TenantId,
                    ct
                )
                    ? await items.GetSupplierCodeMatchAsync(
                        supplier,
                        line.SupplierCode,
                        document.TenantId,
                        ct
                    )
                    : null;
            if (
                registered is not null
                && (
                    registered.ItemId != itemId
                    || (
                        packagingLevelId.HasValue && registered.PackagingLevelId != packagingLevelId
                    )
                )
            )
                throw new RowFailureException(
                    new(
                        line.Id,
                        input.NewItemKey,
                        "El código de proveedor ya está asociado a otro producto o presentación. Actualice la factura."
                    )
                );
            await confirmation.ConfirmAsync(
                document,
                line,
                itemId,
                user.UserId,
                matchedAt,
                packagingLevelId,
                ct
            );
            if (learns)
                learnedCodes.Add(line.SupplierCode!);

            outcome.Lines.Add(
                await ToResolvedLineAsync(document, line, itemId, packagingLevelId, learns, ct)
            );
        }

        outcome.EquivalencesLearned = learnedCodes.Count;
        await documents.SaveChangesAsync(ct);
        // Other pending lines of this document sharing a code just learned resolve automatically.
        var pendingIds = document.Lines.Where(l => l.ItemId is null).Select(l => l.Id).ToHashSet();
        outcome.LinesAutoMatched = await autoMatcher.RefreshAsync(document, ct);
        foreach (
            var line in document.Lines.Where(l => pendingIds.Contains(l.Id) && l.ItemId is not null)
        )
            outcome.Lines.Add(
                await ToResolvedLineAsync(document, line, line.ItemId!.Value, null, false, ct)
            );
    }

    private async Task<ResolvedReceptionLineDto> ToResolvedLineAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid itemId,
        Guid? packagingLevelId,
        bool learned,
        CancellationToken ct
    )
    {
        var item =
            await items.GetByIdAsync(itemId, document.TenantId, ct)
            ?? throw new RowFailureException(
                new(line.Id, null, "El producto vinculado no existe.")
            );
        var effectiveLevelId =
            packagingLevelId
            ?? (
                document.SupplierId is { } supplierId
                && !string.IsNullOrWhiteSpace(line.SupplierCode)
                    ? (
                        await items.GetSupplierCodeMatchAsync(
                            supplierId,
                            line.SupplierCode,
                            document.TenantId,
                            ct
                        )
                    )?.PackagingLevelId
                    : null
            );
        var level = item.PackagingLevels.FirstOrDefault(p => p.Id == effectiveLevelId);
        return new ResolvedReceptionLineDto(
            line.Id,
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            ItemMatchingMapper.ToStatusCode(line.MatchStatus),
            level?.Id,
            level?.UomCode ?? item.DefaultUomCode,
            item.DefaultUomCode,
            level?.BaseQuantity ?? 1m,
            learned
        );
    }

    /// <summary>
    /// Presentaciones del Item nuevo: siempre la unidad base (factor 1, UOM del Item) más una por
    /// cada factor distinto que usan sus códigos de proveedor. Reutiliza el mismo contrato de
    /// presentaciones del módulo Items (no hay motor de conversiones propio).
    /// </summary>
    private static List<PackagingLevelInput> BuildPresentations(
        ResolveReceptionNewItemInput newItem,
        IReadOnlyList<ResolveReceptionLineInput> itemLines
    )
    {
        var purchaseFactor = itemLines[0].PresentationFactor;
        var baseLine = itemLines.FirstOrDefault(l => l.PresentationFactor == 1m);
        var levels = new List<PackagingLevelInput>
        {
            new(
                null,
                string.IsNullOrWhiteSpace(baseLine?.PresentationName)
                    ? BasePresentationName
                    : baseLine.PresentationName.Trim(),
                1,
                1m,
                newItem.DefaultUomCode.Trim(),
                IsBaseUnit: true,
                IsPurchaseDefault: purchaseFactor == 1m,
                IsSaleDefault: true
            ),
        };
        var level = 2;
        foreach (
            var group in itemLines
                .Where(l => l.PresentationFactor != 1m)
                .GroupBy(l => l.PresentationFactor)
                .OrderBy(g => g.Key)
        )
        {
            var first = group.First();
            levels.Add(
                new(
                    null,
                    Name(first),
                    level++,
                    group.Key,
                    Uom(first),
                    IsPurchaseDefault: group.Key == purchaseFactor
                )
            );
        }
        return levels;
    }

    private static string Name(ResolveReceptionLineInput line) =>
        line.PresentationName?.Trim() ?? string.Empty;

    private static string Uom(ResolveReceptionLineInput line) =>
        line.PresentationUomCode?.Trim().ToUpperInvariant() ?? string.Empty;

    private static CreateItemCommand ToCreateItemCommand(ResolveReceptionNewItemInput newItem) =>
        new(
            newItem.Sku.Trim(),
            newItem.ShortName.Trim(),
            newItem.Description.Trim(),
            newItem.ItemTypeId,
            newItem.DefaultUomCode.Trim(),
            newItem.CategoryNodeId,
            newItem.BrandId,
            [new CreateItemBarcodeDto(newItem.Barcode.Trim(), newItem.BarcodeType, true)],
            newItem.SaleVatCode,
            newItem.PurchaseVatCode,
            newItem.ExciseTaxCode,
            BaseSalePrice: newItem.BaseSalePrice
        );

    private sealed class ExecutionOutcome
    {
        public Guid? CurrentLineId { get; set; }
        public string? CurrentItemKey { get; set; }
        public List<ResolvedReceptionLineDto> Lines { get; } = [];
        public int ItemsCreated { get; set; }
        public int EquivalencesLearned { get; set; }
        public int LinesAutoMatched { get; set; }
    }

    /// <summary>Aborta la transacción del lote identificando la fila que falló.</summary>
    private sealed class RowFailureException(ResolveReceptionRowError error)
        : InvalidOperationException(error.Message)
    {
        public ResolveReceptionRowError Error { get; } = error;
    }
}
