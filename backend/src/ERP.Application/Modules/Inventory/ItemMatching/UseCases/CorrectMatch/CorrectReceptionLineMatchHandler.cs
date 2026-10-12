using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;

/// <summary>
/// Orquesta, sin motor propio, el mismo efecto que "Desvincular" + "Vincular" pero atómico:
/// <see cref="IItemMatchConfirmationService.UnconfirmAsync"/> deshabilita la equivalencia anterior
/// (trazabilidad: queda inactiva en el Item anterior) y
/// <see cref="IItemMatchConfirmationService.ConfirmAsync"/> aprende la nueva. Las otras líneas del
/// documento con el mismo código se re-vinculan (vinculadas al producto anterior) o se auto-concilian
/// (pendientes) — nunca queda una equivalencia contradictoria.
/// </summary>
public sealed class CorrectReceptionLineMatchHandler(
    IPurchaseReceptionDocumentRepository documents,
    IPurchaseInvoiceRepository purchases,
    IItemRepository items,
    IItemMatchConfirmationService confirmation,
    IPurchaseReceptionAutoMatcher autoMatcher,
    IUnitOfWork unitOfWork,
    ICurrentTenant tenant,
    ICurrentCompany company,
    ICurrentBranch branch,
    ICurrentUser user,
    IDatabaseExceptionTranslator databaseExceptions
) : IRequestHandler<CorrectReceptionLineMatchCommand, Result<CorrectReceptionLineMatchResultDto>>
{
    public async Task<Result<CorrectReceptionLineMatchResultDto>> Handle(
        CorrectReceptionLineMatchCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var document = await documents.GetByLineIdAsync(
            tenant.TenantId,
            cmd.PurchaseReceptionLineId,
            cancellationToken
        );
        if (
            document is null
            || document.CompanyId != company.CompanyId
            || document.BranchId != branch.BranchId
        )
            return Result<CorrectReceptionLineMatchResultDto>.NotFound(
                "La línea de recepción no existe."
            );
        if (
            document.Status
            is not (
                PurchaseReceptionDocumentStatus.Verified
                or PurchaseReceptionDocumentStatus.Processed
            )
        )
            return Result<CorrectReceptionLineMatchResultDto>.ValidationFailure(
                "Solo se pueden corregir líneas de un documento con XML autorizado."
            );

        var line = document.Lines.Single(l => l.Id == cmd.PurchaseReceptionLineId);
        if (line.ItemId is not { } previousItemId)
            return Result<CorrectReceptionLineMatchResultDto>.ValidationFailure(
                "La línea no tiene un producto vinculado. Use \"Resolver productos\"."
            );

        var purchase = await purchases.GetByAccessKeyAsync(
            tenant.TenantId,
            document.AccessKey,
            cancellationToken
        );
        if (purchase is { Status: PurchaseStatus.Confirmed })
            return Result<CorrectReceptionLineMatchResultDto>.Conflict(
                "La compra de este comprobante ya está confirmada: la relación no se corrige desde aquí. Corrija el código del proveedor en la ficha del producto."
            );

        var error = await ValidateTargetAsync(document, line, previousItemId, cmd, cancellationToken);
        if (error is not null)
            return Result<CorrectReceptionLineMatchResultDto>.ValidationFailure(error);

        var changed = new List<ResolvedReceptionLineDto>();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                ct => ApplyAsync(document, line, previousItemId, cmd, changed, ct),
                cancellationToken
            );
        }
        catch (CorrectionRejectedException ex)
        {
            unitOfWork.ClearChangeTracker();
            return Result<CorrectReceptionLineMatchResultDto>.ValidationFailure(ex.Message);
        }
        catch (Exception ex) when (databaseExceptions.TryGetUniqueViolation(ex, out _))
        {
            unitOfWork.ClearChangeTracker();
            return Result<CorrectReceptionLineMatchResultDto>.Conflict(
                "Otro proceso modificó la relación de este código de proveedor. Actualice la factura e intente de nuevo."
            );
        }

        return Result<CorrectReceptionLineMatchResultDto>.Success(
            new CorrectReceptionLineMatchResultDto(changed, HasSupplierCode(document, line))
        );
    }

    private async Task<string?> ValidateTargetAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid previousItemId,
        CorrectReceptionLineMatchCommand cmd,
        CancellationToken ct
    )
    {
        var item = await items.GetByIdLightAsync(cmd.ItemId, document.TenantId, ct);
        if (item is null || !item.IsActive)
            return "El producto seleccionado no existe o está deshabilitado.";
        if (
            cmd.PackagingLevelId is { } levelId
            && !await items.PackagingLevelBelongsToItemAsync(cmd.ItemId, levelId, document.TenantId, ct)
        )
            return "La presentación seleccionada no pertenece al ítem.";
        if (item.ParticipatesInInventory && cmd.PackagingLevelId is null)
            return "Seleccione la presentación que entrega el proveedor para este producto inventariable.";

        if (!HasSupplierCode(document, line))
            return null;
        var code = Normalize(line.SupplierCode)!;
        if (!await items.SupplierCodeExistsAsync(document.SupplierId!.Value, code, document.TenantId, ct))
            return null;
        var registered = await items.GetSupplierCodeMatchAsync(
            document.SupplierId.Value,
            line.SupplierCode!,
            document.TenantId,
            ct
        );
        // Solo se reemplaza la relación que esta línea usaba (o la ya correcta); la de un tercer
        // producto es otra decisión del usuario y no se pisa en silencio.
        return registered is not null
            && registered.ItemId != previousItemId
            && registered.ItemId != cmd.ItemId
            ? "El código de proveedor está asociado a otro producto distinto al de esta línea. Revíselo en la ficha de ese producto."
            : null;
    }

    private async Task ApplyAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid previousItemId,
        CorrectReceptionLineMatchCommand cmd,
        List<ResolvedReceptionLineDto> changed,
        CancellationToken ct
    )
    {
        var code = Normalize(line.SupplierCode);
        var siblings = code is null
            ? []
            : document
                .Lines.Where(l =>
                    l.Id != line.Id && l.ItemId == previousItemId && Normalize(l.SupplierCode) == code
                )
                .ToList();

        if (previousItemId != cmd.ItemId)
        {
            await confirmation.UnconfirmAsync(document, line, user.UserId, ct);
            if (
                code is not null
                && document.SupplierId is { } supplierId
                && await items.SupplierCodeExistsAsync(supplierId, code, document.TenantId, ct)
                && (await items.GetSupplierCodeMatchAsync(supplierId, code, document.TenantId, ct))
                    ?.ItemId == previousItemId
            )
                throw new CorrectionRejectedException(
                    "El código del proveedor está marcado como principal en la ficha del producto anterior. Quite esa marca desde la ficha antes de corregir la relación."
                );
        }

        var matchedAt = DateTime.UtcNow;
        foreach (var target in siblings.Prepend(line))
        {
            await confirmation.ConfirmAsync(
                document,
                target,
                cmd.ItemId,
                user.UserId,
                matchedAt,
                cmd.PackagingLevelId,
                ct
            );
        }
        await documents.SaveChangesAsync(ct);

        var pendingIds = document.Lines.Where(l => l.ItemId is null).Select(l => l.Id).ToHashSet();
        await autoMatcher.RefreshAsync(document, ct);

        foreach (var target in siblings.Prepend(line))
            changed.Add(await BuildAsync(document, target, cmd.PackagingLevelId, ct));
        foreach (
            var autoMatched in document.Lines.Where(l =>
                pendingIds.Contains(l.Id) && l.ItemId is not null
            )
        )
            changed.Add(await BuildAsync(document, autoMatched, null, ct));
    }

    private async Task<ResolvedReceptionLineDto> BuildAsync(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Guid? packagingLevelId,
        CancellationToken ct
    ) =>
        await ResolvedReceptionLineFactory.BuildAsync(
            items,
            document,
            line,
            line.ItemId!.Value,
            packagingLevelId,
            HasSupplierCode(document, line),
            ct
        ) ?? throw new CorrectionRejectedException("El producto vinculado no existe.");

    private static bool HasSupplierCode(PurchaseReceptionDocument document, PurchaseReceptionLine line) =>
        document.SupplierId.HasValue && Normalize(line.SupplierCode) is not null;

    private static string? Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private sealed class CorrectionRejectedException(string message)
        : InvalidOperationException(message);
}
