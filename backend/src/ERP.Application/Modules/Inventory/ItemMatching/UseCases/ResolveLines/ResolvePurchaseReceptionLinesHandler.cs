using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
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

    /// <summary>
    /// Tipo del catálogo global <c>barcode_types</c> para el código provisional: Items exige ≥1
    /// código de barras, y sin EAN/UPC real de la unidad se usa el código del proveedor como
    /// identificación interna hasta registrar el real desde la ficha del Item.
    /// </summary>
    private const string ProvisionalBarcodeType = "Internal";

    /// <summary>Mismo límite que el código de barras del Item (CreateItemCommandValidator).</summary>
    private const int MaxBarcodeLength = 100;

    /// <summary>
    /// FASE 5 PURCHASE-UX — índice único de BD (item_id, uom_code): dos presentaciones de un mismo
    /// Item no pueden compartir unidad de medida. Se pre-valida (ver DuplicatePresentationUom); si
    /// igual llega a la BD, se informa como unidad duplicada, nunca como concurrencia.
    /// </summary>
    private const string PackagingUomUniqueConstraint = "uq_item_packaging_uom";

    private const string DuplicatePresentationUomHint =
        "Cada presentación del producto necesita su propia unidad: elija otra (p. ej. Paquete o Caja).";

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
        catch (Exception ex) when (databaseExceptions.TryGetUniqueViolation(ex, out var violation))
        {
            unitOfWork.ClearChangeTracker();
            // FASE 5 PURCHASE-UX — unidad duplicada entre presentaciones del mismo producto: es un
            // dato del lote, no una colisión con otro proceso. El mensaje de concurrencia queda solo
            // para SKU / código de barras / código de proveedor.
            var message = string.Equals(
                violation?.ConstraintName,
                PackagingUomUniqueConstraint,
                StringComparison.OrdinalIgnoreCase
            )
                ? $"Dos presentaciones del producto usan la misma unidad de medida. {DuplicatePresentationUomHint}"
                : "Otro proceso registró el producto o código de proveedor. Actualice la factura y revise esta fila.";
            return Rejected([
                new ResolveReceptionRowError(
                    outcome.CurrentLineId,
                    outcome.CurrentItemKey,
                    message
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
                var message in await ValidateNewItemAsync(
                    document,
                    newItem,
                    itemLines,
                    skus,
                    barcodes,
                    ct
                )
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
        PurchaseReceptionDocument document,
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

        var conflictingBarcode = itemLines
            .GroupBy(l => l.PresentationFactor)
            .FirstOrDefault(g =>
                g.Select(l => NormalizedBarcode(l.Barcode))
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1
            );
        if (conflictingBarcode is not null)
            messages.Add(
                $"La presentación de {conflictingBarcode.Key} unidades tiene códigos de barras distintos entre líneas."
            );

        var codes = ResolveBarcodes(document, itemLines);
        if (codes.Presentations.Values.Any(code => code.Length > MaxBarcodeLength))
            messages.Add(
                $"El código de barras de la presentación no puede exceder {MaxBarcodeLength} caracteres."
            );
        if (itemLines.Count > 0 && codes.Base is null)
            messages.Add(
                "Ingrese el código de barras de la unidad: la línea no trae un código del proveedor que pueda usarse como código Interno provisional."
            );

        var command = ToCreateItemCommand(newItem, codes.Base);
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

        foreach (var (barcode, provisional) in codes.All())
        {
            if (!barcodes.Add(barcode))
                messages.Add(
                    provisional
                        ? $"El código del proveedor '{barcode}' se usaría como código de barras Interno provisional, pero está repetido en el lote. Ingrese el código de barras de la unidad."
                        : $"El código de barras '{barcode}' está repetido en el lote."
                );
            else if (
                await items.BarcodeExistsAsync(barcode, tenant.TenantId, company.CompanyId, ct)
            )
                messages.Add(
                    provisional
                        ? $"El código del proveedor '{barcode}' se usaría como código de barras Interno provisional, pero ya está asignado a otro ítem. Ingrese el código de barras de la unidad."
                        : $"El código de barras '{barcode}' ya está asignado a otro ítem."
                );
        }

        var conflictingFactor = itemLines
            .Where(l => l.PresentationFactor != 1m)
            .GroupBy(l => l.PresentationFactor)
            .FirstOrDefault(g => g.Select(l => (Name(l), Uom(l))).Distinct().Count() > 1);
        if (conflictingFactor is not null)
            messages.Add(
                $"La presentación de {conflictingFactor.Key} unidades tiene nombres o unidades distintas entre líneas."
            );

        var duplicateUom = DuplicatePresentationUom(newItem, itemLines);
        if (duplicateUom is not null)
            messages.Add(duplicateUom);
        return messages.Distinct().ToList();
    }

    /// <summary>
    /// FASE 5 PURCHASE-UX — mismas presentaciones que <see cref="BuildPresentations"/> (unidad base con
    /// la UoM del Item + una por factor): ninguna puede repetir unidad de medida (índice único
    /// <c>uq_item_packaging_uom</c>). Devuelve el mensaje de la primera repetida, o null.
    /// </summary>
    private static string? DuplicatePresentationUom(
        ResolveReceptionNewItemInput newItem,
        IReadOnlyList<ResolveReceptionLineInput> itemLines
    )
    {
        var used = new Dictionary<string, string>(StringComparer.Ordinal);
        var baseUom = newItem.DefaultUomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (baseUom.Length > 0)
            used[baseUom] = "la unidad base";
        foreach (
            var group in itemLines
                .Where(l => l.PresentationFactor != 1m)
                .GroupBy(l => l.PresentationFactor)
                .OrderBy(g => g.Key)
        )
        {
            var first = group.First();
            var uom = Uom(first);
            if (uom.Length == 0)
                continue;
            var label = $"la presentación '{Name(first)}' (x{group.Key:0.######})";
            if (used.TryGetValue(uom, out var other))
                return $"La presentación '{Name(first)}' (x{group.Key:0.######}) usa la misma unidad de medida que {other}. {DuplicatePresentationUomHint}";
            used[uom] = label;
        }
        return null;
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
            var itemLines = cmd.Lines.Where(l => l.NewItemKey == newItem.Key).ToList();
            var codes = ResolveBarcodes(document, itemLines);
            var created = await mediator.Send(ToCreateItemCommand(newItem, codes.Base), ct);
            if (!created.IsSuccess || created.Value is null)
                throw new RowFailureException(
                    new(null, newItem.Key, created.Error ?? "No se pudo crear el producto.")
                );

            var packaging = await mediator.Send(
                new ReplaceItemPackagingLevelsCommand(
                    created.Value.Id,
                    BuildPresentations(newItem, itemLines, codes.Presentations)
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
    ) =>
        await ResolvedReceptionLineFactory.BuildAsync(
            items,
            document,
            line,
            itemId,
            packagingLevelId,
            learned,
            ct
        )
        ?? throw new RowFailureException(new(line.Id, null, "El producto vinculado no existe."));

    /// <summary>
    /// Presentaciones del Item nuevo: siempre la unidad base (factor 1, UOM del Item) más una por
    /// cada factor distinto que usan sus códigos de proveedor. Reutiliza el mismo contrato de
    /// presentaciones del módulo Items (no hay motor de conversiones propio). El código de barras de
    /// una caja/pack queda en SU presentación; el de la unidad vive en el Item (CreateItem).
    /// </summary>
    private static List<PackagingLevelInput> BuildPresentations(
        ResolveReceptionNewItemInput newItem,
        IReadOnlyList<ResolveReceptionLineInput> itemLines,
        IReadOnlyDictionary<decimal, string> presentationBarcodes
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
                    Barcode: presentationBarcodes.GetValueOrDefault(group.Key),
                    IsPurchaseDefault: group.Key == purchaseFactor
                )
            );
        }
        return levels;
    }

    /// <summary>Códigos de barras del Item nuevo, cada uno en su presentación.</summary>
    private sealed record NewItemBarcodes(
        CreateItemBarcodeDto? Base,
        bool BaseIsProvisional,
        IReadOnlyDictionary<decimal, string> Presentations
    )
    {
        public IEnumerable<(string Code, bool Provisional)> All() =>
            (Base is null ? [] : new[] { (Base.Code, BaseIsProvisional) }).Concat(
                Presentations.Values.Select(code => (code, false))
            );
    }

    /// <summary>
    /// Código real de la unidad = el de una línea de factor 1. Sin él, el Item (que exige ≥1 código)
    /// recibe el código del proveedor como Interno provisional — de una línea de unidad primero, y
    /// nunca uno que ya es el código de barras de una caja/pack del mismo producto.
    /// </summary>
    private static NewItemBarcodes ResolveBarcodes(
        PurchaseReceptionDocument document,
        IReadOnlyList<ResolveReceptionLineInput> itemLines
    )
    {
        var presentations = itemLines
            .Where(l => l.PresentationFactor != 1m && NormalizedBarcode(l.Barcode) is not null)
            .GroupBy(l => l.PresentationFactor)
            .ToDictionary(g => g.Key, g => NormalizedBarcode(g.First().Barcode)!);

        var unit = itemLines.FirstOrDefault(l =>
            l.PresentationFactor == 1m && NormalizedBarcode(l.Barcode) is not null
        );
        if (unit is not null)
            return new(
                new CreateItemBarcodeDto(
                    NormalizedBarcode(unit.Barcode)!,
                    unit.BarcodeType?.Trim() ?? string.Empty,
                    true
                ),
                false,
                presentations
            );

        var supplierCode = itemLines
            .OrderBy(l => l.PresentationFactor == 1m ? 0 : 1)
            .Select(l =>
                NormalizedBarcode(
                    document.Lines.FirstOrDefault(d => d.Id == l.PurchaseReceptionLineId)?.SupplierCode
                )
            )
            .FirstOrDefault(code =>
                code is not null
                && !presentations.Values.Contains(code, StringComparer.OrdinalIgnoreCase)
            );
        return new(
            supplierCode is null
                ? null
                : new CreateItemBarcodeDto(supplierCode, ProvisionalBarcodeType, true),
            true,
            presentations
        );
    }

    private static string? NormalizedBarcode(string? value) =>
        Trimmed(value) is { } code && code != "0" ? code : null;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Name(ResolveReceptionLineInput line) =>
        line.PresentationName?.Trim() ?? string.Empty;

    private static string Uom(ResolveReceptionLineInput line) =>
        line.PresentationUomCode?.Trim().ToUpperInvariant() ?? string.Empty;

    private static CreateItemCommand ToCreateItemCommand(
        ResolveReceptionNewItemInput newItem,
        CreateItemBarcodeDto? unitBarcode
    ) =>
        new(
            newItem.Sku.Trim(),
            newItem.ShortName.Trim(),
            newItem.Description.Trim(),
            newItem.ItemTypeId,
            newItem.DefaultUomCode.Trim(),
            newItem.CategoryNodeId,
            newItem.BrandId,
            unitBarcode is null ? [] : [unitBarcode],
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
