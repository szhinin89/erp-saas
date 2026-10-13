using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.RefreshPurchaseReceptionStatus;

/// <summary>
/// FASE 5 PURCHASE-UX — "Actualizar estados" de la bandeja de Recepción: recalcula las filas ya
/// importadas (estado real de la compra, documento/procesamiento, proveedor, gasto, NC/factura
/// afectada) SIN volver a importar el TXT ni descargar XML. Estrictamente de solo lectura: no
/// guarda, no agrega documentos, no asigna proveedor y no consulta el SRI.
/// </summary>
public sealed record RefreshPurchaseReceptionStatusQuery(IReadOnlyList<Guid> DocumentIds)
    : IRequest<Result<IReadOnlyList<PurchaseReceptionItemDto>>>,
        IBranchScopedRequest;

public sealed class RefreshPurchaseReceptionStatusValidator
    : AbstractValidator<RefreshPurchaseReceptionStatusQuery>
{
    /// <summary>El frontend envía lotes de 200; el tope deja margen sin permitir consultas abusivas.</summary>
    public const int MaxDocumentIds = 500;

    public RefreshPurchaseReceptionStatusValidator()
    {
        RuleFor(x => x.DocumentIds)
            .NotNull()
            .Must(ids => ids is { Count: > 0 and <= MaxDocumentIds })
            .WithMessage($"Indique entre 1 y {MaxDocumentIds} recepciones para actualizar.");
        RuleForEach(x => x.DocumentIds).NotEqual(Guid.Empty);
    }
}

public sealed class RefreshPurchaseReceptionStatusHandler(
    IPurchaseReceptionDocumentRepository documents,
    IPurchaseReceptionVerifier verifier,
    IPurchaseCreditNoteRepository creditNotes,
    ICurrentTenant tenant
) : IRequestHandler<RefreshPurchaseReceptionStatusQuery, Result<IReadOnlyList<PurchaseReceptionItemDto>>>
{
    public async Task<Result<IReadOnlyList<PurchaseReceptionItemDto>>> Handle(
        RefreshPurchaseReceptionStatusQuery query,
        CancellationToken ct
    )
    {
        // Tenant + empresa (alcance del repositorio): los ids ajenos simplemente no vuelven.
        var loaded = await documents.GetByIdsAsync(tenant.TenantId, query.DocumentIds.Distinct().ToList(), ct);
        if (loaded.Count == 0)
            return Result<IReadOnlyList<PurchaseReceptionItemDto>>.Success([]);

        // Mismo verificador que la importación (proveedor, estado real de la compra, gasto, NC).
        var verified = await verifier.VerifyAsync(
            loaded.Select(PurchaseReceptionMapper.ToRecord).ToList(),
            ct
        );

        var items = new List<PurchaseReceptionItemDto>(loaded.Count);
        for (var i = 0; i < loaded.Count; i++)
        {
            var document = loaded[i];
            items.Add(
                await PurchaseReceptionItemAssembler.BuildAsync(
                    creditNotes,
                    tenant.TenantId,
                    verified[i],
                    document,
                    document.AuthorizationDate ?? default,
                    ct
                )
            );
        }
        return Result<IReadOnlyList<PurchaseReceptionItemDto>>.Success(items);
    }
}
