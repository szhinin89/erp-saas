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
    /// código de barras. Se genera independientemente del proveedor, hasta añadir el EAN real.
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
    ) => await HandleAttemptAsync(cmd, cancellationToken, 0);

    private async Task<Result<ResolvePurchaseReceptionLinesResultDto>> HandleAttemptAsync(
        ResolvePurchaseReceptionLinesCommand cmd, CancellationToken cancellationToken, int attempt
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

        var allocatedCodes = new Dictionary<string, NewItemBarcodes>(StringComparer.Ordinal);
        var errors = await ValidateAsync(document, cmd, allocatedCodes, cancellationToken);
        if (errors.Count > 0)
            return Rejected(errors);

        var outcome = new ExecutionOutcome();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                ct => ApplyAsync(document, cmd, allocatedCodes, outcome, ct),
                cancellationToken
            );
        }
        catch (GeneratedBarcodeCollisionException) when (attempt < 3)
        {
            // The whole batch was rolled back. Reload reception and regenerate only internal codes.
            unitOfWork.ClearChangeTracker();
            return await HandleAttemptAsync(cmd, cancellationToken, attempt + 1);
        }
        catch (GeneratedBarcodeCollisionException)
        {
            unitOfWork.ClearChangeTracker();
            return Rejected([new(null, outcome.CurrentItemKey,
                "No se pudo reservar un código Interno único. Reintente la operación.")]);
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
        Dictionary<string, NewItemBarcodes> allocatedCodes,
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
            if (NormalizedSupplierCode(line.SupplierCode) is null)
                continue;
            var target = input.ItemId is { } existing
                ? $"item:{existing}:{input.PackagingLevelId}"
                : $"new:{input.NewItemKey}:{input.PresentationFactor}";
            var normalizedCode = line.SupplierCode!.Trim().ToUpperInvariant();
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
                    allocatedCodes,
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
            document.SupplierId is { } supplierId && NormalizedSupplierCode(line.SupplierCode) is not null
                ? await items.GetSupplierCodeMatchAsync(
                    supplierId,
                    line.SupplierCode!,
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
        Dictionary<string, NewItemBarcodes> allocatedCodes,
        CancellationToken ct
    )
    {
        itemLines = ResolveXmlBarcodes(document, itemLines);
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

        var codes = ResolveBarcodes(itemLines);
        if (codes.Presentations.Values.Any(code => code.Length > MaxBarcodeLength))
            messages.Add(
                $"El código de barras de la presentación no puede exceder {MaxBarcodeLength} caracteres."
            );
        if (codes.BaseIsProvisional)
        {
            var reserved = false;
            for (var tries = 0; tries < 10; tries++)
            {
                if (!barcodes.Contains(codes.Base!.Code)
                    && !await items.BarcodeExistsAsync(codes.Base.Code, tenant.TenantId, company.CompanyId, ct))
                {
                    reserved = true;
                    break;
                }
                codes = codes with { Base = NewInternalBarcode() };
            }
            if (!reserved)
                messages.Add("No se pudo reservar un código Interno único. Reintente la operación.");
        }
        allocatedCodes[newItem.Key] = codes;

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
                messages.Add($"El código de barras '{barcode}' está repetido en el lote.");
            else if (!provisional && await items.BarcodeExistsAsync(barcode, tenant.TenantId, company.CompanyId, ct))
                messages.Add($"El código de barras '{barcode}' ya está asignado a otro ítem. Use Vincular en lugar de crear.");
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
        IReadOnlyDictionary<string, NewItemBarcodes> allocatedCodes,
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
            var codes = allocatedCodes[newItem.Key];
            var created = await mediator.Send(ToCreateItemCommand(newItem, codes.Base), ct);
            if (!created.IsSuccess && codes.BaseIsProvisional && created.Code == "BARCODE_DUPLICATE")
                throw new GeneratedBarcodeCollisionException();
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
                && NormalizedSupplierCode(line.SupplierCode) is not null
                && (
                    learnedCodes.Contains(line.SupplierCode!)
                    || !await items.SupplierCodeExistsAsync(
                        supplierId,
                        line.SupplierCode!.Trim().ToUpperInvariant(),
                        document.TenantId,
                        ct
                    )
                );
            // Recheck after product creation: another batch may have learned this code meanwhile.
            var registered =
                document.SupplierId is { } supplier
                && NormalizedSupplierCode(line.SupplierCode) is not null
                && await items.SupplierCodeExistsAsync(
                    supplier,
                    line.SupplierCode!.Trim().ToUpperInvariant(),
                    document.TenantId,
                    ct
                )
                    ? await items.GetSupplierCodeMatchAsync(
                        supplier,
                        line.SupplierCode!,
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
    /// recibe un Interno generado, independiente del proveedor y de los barcodes de caja/pack.
    /// </summary>
    private static IReadOnlyList<ResolveReceptionLineInput> ResolveXmlBarcodes(
        PurchaseReceptionDocument document,
        IReadOnlyList<ResolveReceptionLineInput> itemLines
    ) =>
        // The XML is authoritative even when a client omits its valid EAN from the request.
        itemLines.Select(input =>
        {
            if (NormalizedBarcode(input.Barcode) is not null) return input;
            var source = document.Lines.FirstOrDefault(l => l.Id == input.PurchaseReceptionLineId);
            var gtin = new[] { source?.SupplierAuxCode, source?.SupplierCode }
                .Select(Trimmed).FirstOrDefault(code => IsValidGtin(code) && GtinBarcodeType(code!) is not null);
            return gtin is null ? input : input with { Barcode = gtin, BarcodeType = GtinBarcodeType(gtin) };
        }).ToList();

    private static NewItemBarcodes ResolveBarcodes(IReadOnlyList<ResolveReceptionLineInput> itemLines)
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

        return new(NewInternalBarcode(), true, presentations);
    }

    private static CreateItemBarcodeDto NewInternalBarcode() =>
        new($"INT-{Guid.NewGuid():N}", ProvisionalBarcodeType, true);

    /// <summary>
    /// Un código del XML solo es barcode si es GTIN-8/12/13/14 con dígito verificador válido (misma
    /// regla que <c>isRealBarcode</c> del frontend). Cualquier otro valor es código del proveedor.
    /// </summary>
    private static bool IsValidGtin(string? value)
    {
        if (value is null || value.Length is not (8 or 12 or 13 or 14) || value.Any(c => c is < '0' or > '9'))
            return false;
        var sum = 0;
        for (var i = value.Length - 2; i >= 0; i--)
            sum += (value[i] - '0') * ((value.Length - 2 - i) % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10 == value[^1] - '0';
    }

    /// <summary>Tipo inferible solo para EAN-8/EAN-13 (igual que <c>suggestBarcodeType</c>); otro GTIN lo tipifica el usuario.</summary>
    private static string? GtinBarcodeType(string code) =>
        code.Length switch { 8 => "EAN8", 13 => "EAN13", _ => null };

    /// <summary>El barcode de la solicitud es explícito: solo el vacío equivale a ausencia.</summary>
    private static string? NormalizedBarcode(string? value) => Trimmed(value);

    private static string? NormalizedSupplierCode(string? value) =>
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

    private sealed class GeneratedBarcodeCollisionException : Exception;

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
