using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;

public sealed class BulkMatchItemsHandler
    : IRequestHandler<BulkMatchItemsCommand, Result<BulkMatchItemsResultDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IItemMatchConfirmationService _confirmationService;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IReceptionMatchingAccess _access;

    public BulkMatchItemsHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        IItemRepository itemRepo,
        IItemMatchConfirmationService confirmationService,
        ICurrentTenant tenant,
        ICurrentUser user,
        IReceptionMatchingAccess access
    )
    {
        _access = access;
        _documentRepo = documentRepo;
        _itemRepo = itemRepo;
        _confirmationService = confirmationService;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<BulkMatchItemsResultDto>> Handle(
        BulkMatchItemsCommand request,
        CancellationToken cancellationToken
    )
    {
        if (!await _access.CanMatchInAnyContextAsync(cancellationToken))
            return Result<BulkMatchItemsResultDto>.Forbidden(ReceptionMatchingAccess.ForbiddenMessage);

        // H3 — el permiso de cada documento (crear vs. modificar su compra) y la regla de compra
        // confirmada se comprueban para TODO el lote antes de la primera escritura: nunca queda un lote
        // parcialmente conciliado.
        var checkedDocuments = new HashSet<Guid>();
        foreach (var entry in request.Matches)
        {
            var owner = await _documentRepo.GetByLineIdAsync(
                _tenant.TenantId,
                entry.PurchaseReceptionLineId,
                cancellationToken
            );
            if (owner is null || !checkedDocuments.Add(owner.Id))
                continue;
            var decision = await _access.DecideAsync(owner, cancellationToken);
            if (decision != ReceptionMatchingDecision.Allowed)
                return ReceptionMatchingAccess.Reject<BulkMatchItemsResultDto>(decision);
        }

        var results = new List<BulkMatchItemResultEntry>();
        var matchedAtUtc = DateTime.UtcNow;

        foreach (var entry in request.Matches)
        {
            try
            {
                var document = await _documentRepo.GetByLineIdAsync(
                    _tenant.TenantId,
                    entry.PurchaseReceptionLineId,
                    cancellationToken
                );
                if (document is null)
                {
                    results.Add(
                        new BulkMatchItemResultEntry(
                            entry.PurchaseReceptionLineId,
                            false,
                            "La línea de recepción no existe."
                        )
                    );
                    continue;
                }

                var line = document.Lines.First(l => l.Id == entry.PurchaseReceptionLineId);

                var item = await _itemRepo.GetByIdLightAsync(
                    entry.ItemId,
                    _tenant.TenantId,
                    cancellationToken
                );
                if (item is null)
                {
                    results.Add(
                        new BulkMatchItemResultEntry(
                            entry.PurchaseReceptionLineId,
                            false,
                            "El ítem seleccionado no existe."
                        )
                    );
                    continue;
                }

                await _confirmationService.ConfirmAsync(
                    document,
                    line,
                    entry.ItemId,
                    _user.UserId,
                    matchedAtUtc,
                    cancellationToken: cancellationToken
                );
                await _documentRepo.SaveChangesAsync(cancellationToken);

                results.Add(
                    new BulkMatchItemResultEntry(entry.PurchaseReceptionLineId, true, null)
                );
            }
            catch (ArgumentException ex)
            {
                // Una línea inválida dentro del lote no debe abortar las demás.
                results.Add(
                    new BulkMatchItemResultEntry(entry.PurchaseReceptionLineId, false, ex.Message)
                );
            }
        }

        return Result<BulkMatchItemsResultDto>.Success(new BulkMatchItemsResultDto(results));
    }
}
