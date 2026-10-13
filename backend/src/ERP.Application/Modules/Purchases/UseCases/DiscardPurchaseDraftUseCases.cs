using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

/// <summary>
/// FASE 5 PURCHASE-UX — descarta un borrador de compra (soft: <c>Draft → Cancelled</c>, ver
/// <see cref="PurchaseInvoice.DiscardDraft"/>). A diferencia de <see cref="CancelPurchaseCommand"/>
/// (anula una compra CONFIRMADA y revierte inventario/CxP/contabilidad/retención), aquí no hay nada
/// que revertir: el handler solo depende de los repositorios de compras y de recepción.
/// Si el borrador nació de una recepción, esa recepción quedó <c>Processed</c> vinculada a él al
/// guardarlo (<c>CreatePurchaseDraftHandler</c> → <c>MarkProcessed</c>): se libera con
/// <c>UnmarkProcessed</c> (vuelve a <c>Verified</c>, <c>PurchaseId</c> null), mismo paso que
/// <c>CancelPurchaseHandler</c> 5b, para poder crear otra compra desde ella. El XML y la evidencia
/// de la recepción no se tocan.
/// </summary>
public sealed record DiscardPurchaseDraftCommand(Guid PurchaseInvoiceId, string? Reason = null)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

public sealed class DiscardPurchaseDraftValidator : AbstractValidator<DiscardPurchaseDraftCommand>
{
    public DiscardPurchaseDraftValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
        RuleFor(x => x.Reason)
            .MaximumLength(PurchaseInvoice.CancelReasonMaxLen)
            .WithMessage(
                $"El motivo no puede superar {PurchaseInvoice.CancelReasonMaxLen} caracteres."
            );
    }
}

public sealed class DiscardPurchaseDraftHandler(
    IPurchaseInvoiceRepository repo,
    IPurchaseReceptionDocumentRepository receptions,
    ICurrentTenant tenant,
    ICurrentBranch branch,
    ICurrentUser user
) : IRequestHandler<DiscardPurchaseDraftCommand, Result<PurchaseInvoiceDto>>
{
    public async Task<Result<PurchaseInvoiceDto>> Handle(
        DiscardPurchaseDraftCommand cmd,
        CancellationToken ct
    )
    {
        // Mismo alcance que el resto de Purchases: tenant + empresa (repositorio) y sucursal activa.
        var inv = await repo.GetByIdAsync(tenant.TenantId, cmd.PurchaseInvoiceId, ct);
        if (inv is null || inv.BranchId != branch.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Status != PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                inv.Status == PurchaseStatus.Confirmed
                    ? "Una compra confirmada no se descarta: use \"Anular compra\"."
                    : "Este borrador ya fue descartado o la compra ya fue anulada."
            );

        inv.DiscardDraft(user.UserId, cmd.Reason);

        // Liberar la recepción de origen — se busca por la misma AccessKey que la vinculó y solo se
        // desvincula si apunta a ESTE borrador (nunca una recepción vinculada a otra compra).
        if (!string.IsNullOrWhiteSpace(inv.AccessKey))
        {
            var reception = await receptions.GetByAccessKeyAsync(tenant.TenantId, inv.AccessKey, ct);
            if (reception is not null && reception.PurchaseId == inv.Id)
                reception.UnmarkProcessed(user.UserId);
        }

        // Mismo DbContext: compra y recepción se persisten en un único SaveChanges.
        await repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
