using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
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
    // PURCHASE-P2-P3-CLEANUP-CLOSE-01 — mismo criterio semántico que el guard equivalente en
    // frontend (purchaseLinePresentation.ts: SUSPICIOUS_PURCHASE_MARGIN_THRESHOLD_PCT), sin
    // compartir archivo físico entre capas — solo el nombre y el valor deben mantenerse alineados.
    private const decimal SuspiciousPurchaseMarginThresholdPct = -50m;

    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IStockRepository _stockRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _whRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPostingEngine _postingEngine;
    private readonly IPricingResolver _pricingResolver;
    private readonly IAccountsPayableService _payables;
    private readonly ILogger<ConfirmPurchaseHandler> _logger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly IOperationalPreferencesResolver _preferences;

    public ConfirmPurchaseHandler(
        IPurchaseInvoiceRepository repo,
        IStockRepository stockRepo,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        IPostingEngine postingEngine,
        IPricingResolver pricingResolver,
        IAccountsPayableService payables,
        ILogger<ConfirmPurchaseHandler> logger,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences
    )
    {
        _repo = repo;
        _stockRepo = stockRepo;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _postingEngine = postingEngine;
        _pricingResolver = pricingResolver;
        _payables = payables;
        _logger = logger;
        _t = t;
        _c = c;
        _u = u;
        _preferences = preferences;
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

        // CONFIG-DYNAMIC-OPERATIONS-02 (purchases.allow_confirm_without_reception_xml): si está
        // desactivada, exige que al menos una línea provenga de Recepción Electrónica
        // (PurchaseReceptionLineId) antes de poder confirmar — no reemplaza ni afloja el guard de
        // presentación de STEP 0a (ese sigue aplicando por línea, aquí solo se exige que exista
        // al menos un vínculo con recepción).
        var preferences = await _preferences.ResolveAsync(ct);
        if (
            !preferences.Purchases.AllowConfirmWithoutReceptionXml
            && !inv.Lines.Any(l => l.PurchaseReceptionLineId.HasValue)
        )
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Esta empresa requiere un documento de recepción XML/TXT vinculado para confirmar la compra."
            );

        // ── STEP 0: Guard bodega↔sucursal (PURCHASE-WAREHOUSE-BRANCH-GUARD-01) ──
        // Defensa en profundidad: aunque Create/UpdatePurchaseDraft ya validan esto al guardar,
        // un borrador persistido antes de este guard (o alterado fuera del flujo normal) no debe
        // poder confirmarse con una bodega de otra sucursal.
        foreach (var line in inv.Lines)
        {
            var warehouseId = line.WarehouseId ?? inv.GlobalWarehouseId;
            if (warehouseId is null)
                continue;

            var whCheck = await WarehouseBranchGuard.ValidateAsync(
                _whRepo,
                tid,
                warehouseId.Value,
                inv.BranchId,
                ct
            );
            if (whCheck.Error is not null)
                return whCheck.Error;
        }

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

        // ── STEP 1b: Guard de presentación/costo sospechoso (PURCHASE-BACKEND-SUSPICIOUS-PACKAGING-COST-GUARD-01) ──
        // Defensa en profundidad del guard equivalente en frontend
        // (PURCHASE-LINE-PACKAGING-SUSPICIOUS-COST-GUARD-01): una presentación de proveedor mal
        // configurada (ej. factor de conversión incorrecto) puede dejar LandedUnitCost muy por
        // encima del precio de venta vigente. Confirmar así arrastra ese costo erróneo a
        // StockMovement/Kardex/costo promedio de forma irreversible, así que se bloquea aquí,
        // antes de STEP 2 (Confirm/FreezeCosts) y de STEP 3 (movimientos de inventario).
        // LandedUnitCost ya está calculado por inv.DistributeCosts()/RecalcCosts() en este punto.
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is not { } marginItemId || line.LandedUnitCost <= 0)
                continue;

            var marginItem = await _itemRepo.GetByIdLightAsync(marginItemId, tid, ct);
            if (marginItem is null || !marginItem.StockConfig.TracksStock)
                continue;

            var pricingResult = await _pricingResolver.ResolveAsync(marginItemId, ct: ct);
            if (!pricingResult.IsSuccess || pricingResult.Value is null)
                continue;

            var salePrice = pricingResult.Value.UnitPrice;
            if (salePrice <= 0)
                continue;

            var marginPct = (salePrice - line.LandedUnitCost) / salePrice * 100m;
            if (marginPct < SuspiciousPurchaseMarginThresholdPct)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"No se puede confirmar la compra. La línea '{line.Description}' tiene una "
                        + $"presentación/costo sospechoso: costo unitario {line.LandedUnitCost:0.####}, "
                        + $"precio de venta {salePrice:0.####}, margen {marginPct:0.##}%. "
                        + "Revise la presentación del proveedor antes de confirmar."
                );
        }

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

        // ── STEP 4: Cuenta por pagar (PAYABLES-PURCHASE-MIGRATION-10) ────
        // AccountsPayable reemplaza a PurchasePayable — única fuente de saldo/CxP, ver
        // AccountsPayable.cs. StageFromOriginAsync solo deja la CxP en staging (nunca comitea por
        // su cuenta): se persiste atómicamente junto con el resto de la confirmación en el
        // SaveChangesWithSequenceRetryAsync de STEP 7 (mismo criterio transaccional que el
        // PurchasePayable original, que tampoco comiteaba por separado — _repo.TrackPayable solo
        // agregaba al ChangeTracker). Un fallo aquí (p. ej. cronograma inválido) aborta la
        // confirmación completa — "hardening": nunca un warning silencioso para CxP obligatoria.
        if (inv.GrandTotal > 0)
        {
            try
            {
                await _payables.StageFromOriginAsync(
                    new CreateAccountsPayableFromOriginRequest(
                        tid,
                        cid,
                        inv.BranchId,
                        inv.SupplierId,
                        AccountsPayableOriginType.PurchaseInvoice,
                        inv.Id,
                        inv.DocTypeCode,
                        inv.InvoiceNumber,
                        inv.IssueDate,
                        inv.IssueDate,
                        inv.PaymentSchedules
                            .Select(s => new AccountsPayableInstallmentInput(
                                s.InstallmentNumber,
                                s.DueDate,
                                s.Amount
                            ))
                            .ToList()
                    ),
                    uid,
                    ct
                );
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo generar la cuenta por pagar para la compra {InvoiceId}: {Reason}",
                    inv.Id,
                    ex.Message
                );
                return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
            }
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
