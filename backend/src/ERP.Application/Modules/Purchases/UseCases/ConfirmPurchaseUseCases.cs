using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.UseCases;

public sealed record ConfirmScheduleInput(
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    string? Notes = null
);

public sealed record ConfirmPurchaseCommand(
    Guid InvoiceId,
    List<ConfirmScheduleInput>? Schedule = null
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed class ConfirmPurchaseHandler
    : IRequestHandler<ConfirmPurchaseCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IStockRepository _stockRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<ConfirmPurchaseHandler> _logger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ConfirmPurchaseHandler(
        IPurchaseInvoiceRepository repo,
        IStockRepository stockRepo,
        IItemRepository itemRepo,
        ISriTaxResolver tax,
        IPostingEngine postingEngine,
        ILogger<ConfirmPurchaseHandler> logger,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _stockRepo = stockRepo;
        _itemRepo = itemRepo;
        _tax = tax;
        _postingEngine = postingEngine;
        _logger = logger;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        ConfirmPurchaseCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;
        var uid = _u.UserId;

        var inv = await _repo.GetByIdAsync(tid, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");

        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure("Esta compra ya fue confirmada.");

        // ── STEP 0a: Guard de presentación en compras XML ───────────────
        // Las líneas provenientes de Recepción Electrónica impactan inventario y auditoría fiscal.
        // Para ítems físicos inventariables, el factor debe venir de una presentación explícita
        // (incluida la unidad base x1); nunca se acepta factor 1 implícito por falta de configuración.
        foreach (var line in inv.Lines.Where(l => l.PurchaseReceptionLineId.HasValue))
        {
            if (line.ItemId is not { } itemId)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"La línea XML '{line.Description}' no tiene ítem vinculado."
                );

            var item = await _itemRepo.GetByIdAsync(itemId, tid, ct);
            if (item is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"La línea XML '{line.Description}' referencia un ítem inexistente."
                );

            if (item.StockConfig.TracksStock && line.PackagingLevelId is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"La línea XML '{line.Description}' corresponde a un ítem inventariable sin presentación vinculada. "
                        + "Asocie el código de proveedor a la presentación correcta antes de confirmar."
                );
        }

        // ── STEP 0: Guard IRBPNR (FLOW-READY-02F.2) ──────────────────────
        // El posting nunca revierte una confirmación ya persistida (ver PurchaseInvoiceConfirmedPostingTranslator
        // — un Result fallido de IPostingEngine.PostAsync solo se registra en log, jamás lanza) — por
        // eso la única forma confiable de exigir configuración contable es esta precondición, antes
        // de inv.Confirm()/SaveChanges. GrandTotal/PurchasePayable/el evento SÍ incluyen IRBPNR desde
        // esta fase, así que confirmar sin una PostingRuleLine para TaxIrbpnr generaría un asiento
        // descuadrado (línea de crédito GrandTotal sin su contrapartida de débito) — se bloquea.
        if (inv.Lines.Any(l => l.IrbpnrAmount > 0))
        {
            var irbpnrConfigured = await _postingEngine.IsAmountKindConfiguredAsync(
                tid,
                cid,
                "Purchases",
                "InvoiceReceived",
                PostingAmountKind.TaxIrbpnr,
                ct
            );
            if (!irbpnrConfigured)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    "Esta compra contiene IRBPNR (impuesto código 5), pero no existe una regla de "
                        + "contabilización (PostingRuleLine) configurada para IRBPNR en Compras. "
                        + "Configure la cuenta contable correspondiente antes de confirmar."
                );
        }

        // ── STEP 1: Recalcular impuestos con nombres ────────────────────
        foreach (var line in inv.Lines)
        {
            var vatResult = await _tax.GetVatRateWithNameAsync(line.VatCode, ct);
            if (vatResult is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Código IVA '{line.VatCode}' no encontrado."
                );

            decimal iceRate = 0;
            string? iceName = null;
            var iceCalculationType = ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Percentage;
            decimal? iceExactAmount = null;
            if (!string.IsNullOrWhiteSpace(line.IceCode))
            {
                // FLOW-READY-02F.1 — usa el catálogo completo (no el legacy GetIceRateWithNameAsync,
                // que exige Percentage y por eso nunca resuelve ICE "específico" como el código 3053)
                // para poder re-resolver también líneas con ICE de monto fijo sin romper Confirm.
                var iceEntry = await _tax.GetIceCatalogEntryAsync(line.IceCode, ct);
                if (iceEntry is null)
                    return Result<PurchaseInvoiceDto>.ValidationFailure(
                        $"Código ICE '{line.IceCode}' no encontrado."
                    );
                iceName = iceEntry.Name;
                iceCalculationType = iceEntry.CalculationType;
                if (iceEntry.CalculationType
                    == ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific)
                {
                    // El monto ya fue fijado al valor exacto del XML al crear/actualizar el borrador
                    // — Confirm lo preserva, nunca lo recalcula desde una tarifa porcentual.
                    iceExactAmount = line.IceAmount;
                }
                else
                {
                    iceRate = iceEntry.Percentage ?? 0m;
                }
            }
            line.ApplyTaxes(
                line.VatCode,
                vatResult.Rate,
                vatResult.Name,
                line.IceCode,
                iceRate,
                iceName,
                iceCalculationType,
                iceExactAmount
            );
        }
        inv.DistributeCosts(inv.TotalFreight, inv.TotalOtherCosts, uid);

        // ── STEP 2: Confirmar (cambia estado, valida invariantes, congela costos) ─
        try
        {
            inv.Confirm(uid);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Confirm rejected for invoice {InvoiceId} tenant {TenantId}: {Reason}",
                cmd.InvoiceId,
                tid,
                ex.Message
            );
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        // ── STEP 2b: Generar calendario de pagos ────────────────────────
        await _repo.ClearScheduleTrackingAsync(inv.Id, ct);
        try
        {
            if (cmd.Schedule is { Count: > 0 })
            {
                inv.ReplacePaymentSchedule(
                    cmd.Schedule.Select(s => (s.InstallmentNumber, s.DueDate, s.Amount, s.Notes))
                        .ToList()
                );
            }
            else
            {
                inv.GeneratePaymentSchedule();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                "Payment schedule rejected for invoice {InvoiceId}: {Reason}",
                cmd.InvoiceId,
                ex.Message
            );
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }
        _repo.ReattachSchedulesAsAdded(inv);

        // ── STEP 3: Movimientos de inventario ───────────────────────────
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null)
                continue;
            var warehouseId = line.WarehouseId ?? inv.GlobalWarehouseId;
            if (warehouseId is null)
                continue;

            if (!line.IsFrozen)
            {
                _logger.LogError(
                    "Attempted stock posting with unfrozen line {LineId} on invoice {InvoiceId}",
                    line.Id,
                    inv.Id
                );
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    "Error interno: no se puede registrar inventario con costos no congelados."
                );
            }

            await _stockRepo.AppendMovementAsync(
                tid,
                cid,
                line.ItemId.Value,
                warehouseId.Value,
                StockMovementType.PurchaseEntry,
                line.QuantityInBaseUom,
                line.BaseUomCode,
                inv.IssueDate,
                inv.InvoiceNumber,
                inv.Id,
                "PurchaseInvoice",
                uid,
                line.LandedUnitCost,
                cancellationToken: ct
            );
        }

        // ── STEP 4: Cuenta por pagar ────────────────────────────────────
        if (inv.GrandTotal > 0)
        {
            var payable = PurchasePayable.Create(
                tid,
                cid,
                inv.Id,
                inv.SupplierId,
                inv.GrandTotal,
                uid
            );
            payable.GenerateInstallments(inv.PaymentSchedules);
            _repo.TrackPayable(payable);
        }

        // ── STEP 5: Actualizar precio base del ítem (SSOT, Motor de Pricing) ──
        // Item.BaseSalePrice es la única fuente de verdad del precio — el PVP confirmado
        // en la compra se escribe ahí directamente, nunca como override de PriceList/PricingRule.
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null || line.SnapshotItemPvp <= 0)
                continue;

            var item = await _itemRepo.GetByIdLightAsync(line.ItemId.Value, tid, ct);
            if (item is null || item.BaseSalePrice == line.SnapshotItemPvp)
                continue;

            var oldPvp = item.BaseSalePrice ?? 0m;
            inv.RecordConfirmedItemPvpUpdate(item.Id, oldPvp, line.SnapshotItemPvp);
            item.UpdateBaseSalePrice(line.SnapshotItemPvp, uid);
        }

        // ── STEP 6: Comunicación futura ─────────────────────────────────
        var comm = PurchaseCommunication.Create(
            tid,
            cid,
            inv.Id,
            $"Seguimiento compra {inv.InvoiceNumber}",
            uid,
            $"Compra confirmada el {DateTime.UtcNow:yyyy-MM-dd}. Verificar recepción de mercadería."
        );
        _repo.TrackCommunication(comm);

        // ── STEP 7: Persistir (transacción atómica vía EF SaveChanges) ──
        // La auditoría de "purchase.confirmed" y "pvp.confirmed.update" se registra
        // automáticamente vía PurchaseInvoiceAuditHandler/PurchaseLinePvpAuditHandler,
        // disparados por los domain events levantados en inv.Confirm() e
        // inv.RecordConfirmedItemPvpUpdate() dentro de este mismo SaveChangesAsync.
        _logger.LogInformation(
            "Confirming purchase {InvoiceNumber} ({InvoiceId}) for tenant {TenantId}. Lines: {LineCount}, Total: {GrandTotal}",
            inv.InvoiceNumber,
            inv.Id,
            tid,
            inv.Lines.Count,
            inv.GrandTotal
        );

        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        _logger.LogInformation(
            "Purchase {InvoiceNumber} ({InvoiceId}) confirmed successfully",
            inv.InvoiceNumber,
            inv.Id
        );

        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
