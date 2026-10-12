using ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

public sealed partial class ResolvePurchaseReceptionLinesIntegrationTests
{
    /// <summary>
    /// FASE 3 — "Corregir relación" contra PostgreSQL real: la equivalencia aprendida al resolver
    /// (N-000 → producto nuevo B) se corrige a otro producto (C). La línea queda en C, la relación
    /// de B se deshabilita (trazabilidad, sin DELETE), C la aprende sin chocar con el índice único
    /// (filtrado a activos) y la siguiente factura se reconoce con C.
    /// </summary>
    [Fact]
    public async Task Correcting_a_learned_relation_relinks_the_line_and_the_next_invoice_uses_the_new_product()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var outcome = await ResolveAsync(BuildBatch(document, "CR-"));
        outcome.Applied.Should().BeTrue(string.Join(" | ", outcome.Errors.Select(e => e.Message)));
        var wrong = await LoadCreatedAsync("CR-000");
        var (rightId, rightLevelId) = _linkTargets["L-000"];
        var lineId = (await LoadAsync(document.Id)).Lines.Single(l => l.SupplierCode == "N-000").Id;

        await using (var scope = _services.CreateAsyncScope())
        {
            var result = await scope
                .ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CorrectReceptionLineMatchCommand(lineId, rightId, rightLevelId));
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.Lines.Should()
                .ContainSingle(l => l.PurchaseReceptionLineId == lineId)
                .Which.ItemId.Should()
                .Be(rightId);
        }

        (await LoadAsync(document.Id)).Lines.Single(l => l.Id == lineId).ItemId.Should().Be(rightId);
        await using (var scope = _services.CreateAsyncScope())
        {
            var codes = await scope
                .ServiceProvider.GetRequiredService<ErpDbContext>()
                .Set<ItemSupplierCode>()
                .Where(c => c.SupplierId == _supplierId && c.Code == "N-000")
                .ToListAsync();
            codes.Should().ContainSingle(c => c.IsActive).Which.ItemId.Should().Be(rightId);
            codes.Should().ContainSingle(c => !c.IsActive).Which.ItemId.Should().Be(wrong.Id);
        }

        var next = await LoadAsync(await ReceiveInvoiceAsync());
        next.Lines.Single(l => l.SupplierCode == "N-000")
            .Should()
            .Match<ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionLine>(l =>
                l.ItemId == rightId && l.MatchStatus == ItemMatchStatus.AutoMatched
            );
    }

    /// <summary>
    /// Cierre FASE 3 — borrador guardado con B; la recepción se corrige a C: Confirmar se rechaza
    /// (sin tocar el borrador) hasta guardar de nuevo con C; luego confirma normalmente.
    /// </summary>
    [Fact]
    public async Task Draft_saved_before_a_correction_cannot_be_confirmed_until_saved_again()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        (await ResolveAsync(BuildBatch(document, "CF-"))).Applied.Should().BeTrue();
        var warehouseId = await SeedPurchasePrerequisitesAsync();
        var lineId = (await LoadAsync(document.Id)).Lines.Single(l => l.SupplierCode == "N-000").Id;
        var wrongId = (await LoadCreatedAsync("CF-000")).Id;
        var (rightId, rightLevelId) = _linkTargets["L-000"];

        PurchaseDraftDto draft;
        Guid purchaseId;
        await using (var scope = _services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var loaded = await mediator.Send(new CreatePurchaseReceptionDraftCommand(document.Id));
            loaded.IsSuccess.Should().BeTrue(loaded.Error);
            draft = loaded.Value!;
            draft.Lines.Single(l => l.PurchaseReceptionLineId == lineId).ItemId.Should().Be(wrongId);
            var created = await mediator.Send(
                new CreatePurchaseDraftCommand(
                    _supplierId,
                    "01",
                    draft.InvoiceNumber,
                    draft.IssueDate,
                    DraftLines(draft, warehouseId),
                    AccessKey: draft.AccessKey,
                    GlobalWarehouseId: warehouseId,
                    PaymentTermId: _paymentTerm.Id
                )
            );
            created.IsSuccess.Should().BeTrue(created.Error);
            purchaseId = created.Value!.Id;

            var corrected = await mediator.Send(
                new CorrectReceptionLineMatchCommand(lineId, rightId, rightLevelId)
            );
            corrected.IsSuccess.Should().BeTrue(corrected.Error);
        }

        await using (var scope = _services.CreateAsyncScope())
        {
            var rejected = await scope
                .ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ConfirmPurchaseCommand(purchaseId));
            rejected.IsSuccess.Should().BeFalse();
            rejected.Error.Should().Contain("Guarde nuevamente la compra");
        }
        await using (var scope = _services.CreateAsyncScope())
        {
            // Nada se modificó solo: el borrador sigue en Draft y con el producto guardado (B).
            var saved = await scope
                .ServiceProvider.GetRequiredService<ErpDbContext>()
                .PurchaseInvoices.Include(p => p.Lines)
                .SingleAsync(p => p.Id == purchaseId);
            saved.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
            saved.Lines.Single(l => l.PurchaseReceptionLineId == lineId).ItemId.Should().Be(wrongId);
        }

        await using (var scope = _services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var lines = DraftLines(draft, warehouseId)
                .Select(l =>
                    l.PurchaseReceptionLineId == lineId
                        ? l with { ItemId = rightId, PackagingLevelId = rightLevelId }
                        : l
                )
                .ToList();
            var updated = await mediator.Send(
                new UpdatePurchaseDraftCommand(
                    purchaseId,
                    _supplierId,
                    "01",
                    draft.InvoiceNumber,
                    draft.IssueDate,
                    lines,
                    AccessKey: draft.AccessKey,
                    GlobalWarehouseId: warehouseId,
                    PaymentTermId: _paymentTerm.Id
                )
            );
            updated.IsSuccess.Should().BeTrue(updated.Error);
        }
        await using (var scope = _services.CreateAsyncScope())
        {
            var confirmed = await scope
                .ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ConfirmPurchaseCommand(purchaseId));
            confirmed.IsSuccess.Should().BeTrue(confirmed.Error);
        }
    }

    private static List<PurchaseLineInput> DraftLines(PurchaseDraftDto draft, Guid warehouseId) =>
        draft
            .Lines.Select(l => new PurchaseLineInput(
                l.ItemId,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.VatCode,
                WarehouseId: warehouseId,
                DiscountPct: l.DiscountPct,
                PurchaseReceptionLineId: l.PurchaseReceptionLineId,
                PackagingLevelId: l.PackagingLevelId
            ))
            .ToList();

    /// <summary>Condición de pago, bodega y rol de proveedor — mismo seed que el ciclo de vida.</summary>
    private async Task<Guid> SeedPurchasePrerequisitesAsync()
    {
        await using var seed = _services.CreateAsyncScope();
        var db = seed.ServiceProvider.GetRequiredService<ErpDbContext>();
        _paymentTerm = PaymentTerm.Create(_tenantId, "CONT", "Contado", 1, 0, _userId);
        var warehouse = Warehouse.Create(
            _tenantId,
            _branchId,
            "Principal",
            "B01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            _companyId,
            isMain: true
        );
        db.Set<PaymentTerm>().Add(_paymentTerm);
        db.Warehouses.Add(warehouse);
        db.Set<BusinessPartnerRole>()
            .Add(
                BusinessPartnerRole.Create(
                    _tenantId,
                    _supplierId,
                    RoleType.Supplier,
                    _userId,
                    supplierConfig: SupplierRoleConfig.Create()
                )
            );
        await db.SaveChangesAsync();
        return warehouse.Id;
    }
}
