using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;

namespace ERP.Application.Modules.Purchases.UseCases;

/// <summary>Solo lee evidencia persistida y el estado fiscal oficial; nunca ejecuta una reversa.</summary>
public sealed class PurchaseCreditNoteReprocessingGuard(
    IPurchaseCreditNoteRepository notes, IPurchaseReturnRepository returns,
    IAccountsPayableRepository payables, ISupplierCreditRepository credits, IStockRepository stock,
    IJournalEntryRepository journal, IAccountingPostingRepository postings,
    AccountingOperationalDecision accounting, ISriSettingsRepository settings, ISriDocumentStatusQuery sri,
    IUnitOfWork unitOfWork, ICompanyPrecisionPolicyProvider precision)
{
    public async Task<string?> CheckAsync(Guid tenantId, Guid companyId, Guid supplierId,
        string number, string? accessKey, Guid? receptionId, CancellationToken ct)
    {
        var cancelled = await notes.GetCancelledByIdentityAsync(tenantId, supplierId,
            number.Trim(), string.IsNullOrWhiteSpace(accessKey) ? null : accessKey.Trim(), receptionId, ct);
        if (cancelled.Count > 0)
        {
            await unitOfWork.BeginTransactionAsync(ct);
            try
            {
                foreach (var invoiceId in cancelled.Select(n => n.PurchaseInvoiceId).Distinct().Order())
                    await returns.AcquireFinancialLockAsync(tenantId, invoiceId, ct);
                foreach (var prior in cancelled)
                {
                    var blocker = await CheckReversalAsync(prior, ct);
                    if (blocker is not null)
                    {
                        await unitOfWork.RollbackAsync(ct);
                        return $"Reprocesamiento bloqueado: NC {prior.CreditNoteNumber} ({prior.Id}): {blocker}";
                    }
                }
                await unitOfWork.CommitAsync(ct);
            }
            catch
            {
                await unitOfWork.RollbackAsync(ct);
                throw;
            }
        }
        // Cancelación interna ≠ anulación tributaria. Para documentos electrónicos se exige
        // AUTORIZADO de ConsultaComprobante; no se infiere vigencia desde el XML descargado.
        foreach (var key in cancelled.Select(n => n.AccessKey).Append(accessKey)
            .Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal))
        {
            if (cancelled.Count == 0) break;
            var config = await settings.GetByCompanyIdAsync(companyId, ct);
            if (config is null || string.IsNullOrWhiteSpace(config.WsdlUrl))
                return "Reprocesamiento bloqueado: no se puede verificar la vigencia tributaria ante el SRI.";
            var state = await sri.QueryAsync(key!, config.WsdlUrl, ct);
            if (state.FiscalStatus == SriFiscalStatus.Annulled)
                return "Reprocesamiento bloqueado: el comprobante consta ANULADO ante el SRI.";
            if (!state.IsSuccess || state.FiscalStatus != SriFiscalStatus.Authorized
                || state.AccessKey != key || state.DocumentType != SriDocumentTypeCodes.CreditNote)
                return "Reprocesamiento bloqueado: el SRI no confirma la vigencia de esta nota de crédito.";
        }
        return null;
    }

    private async Task<string?> CheckReversalAsync(PurchaseCreditNote prior, CancellationToken ct)
    {
        var tenantId = prior.TenantId;
        var payable = await payables.GetByOriginAsync(tenantId, prior.CompanyId,
            AccountsPayableOriginType.PurchaseInvoice, prior.PurchaseInvoiceId, ct);
        if (payable is null) return "no se encuentra la CxP para comprobar la reversa.";
        var recognized = await notes.GetRecognizedCreditsAsync(tenantId, prior.PurchaseInvoiceId, ct);
        if (payable.ReturnCreditAmount != recognized.ReturnCredit || payable.CreditNoteAmount != recognized.DiscountCredit)
            return "la CxP no concilia con las NC y devoluciones que siguen autorizadas.";

        var sourceId = prior.Id;
        var fact = "PurchaseCreditNoteAuthorized";
        var businessDate = prior.IssueDate;
        var wasAuthorized = prior.AuthorizedAtUtc.HasValue;
        if (prior.LinkedPurchaseReturnId is { } returnId)
        {
            var originalReturn = await returns.GetByIdAsync(tenantId, returnId, ct);
            if (originalReturn is null || originalReturn.Status != PurchaseReturnStatus.Cancelled
                || originalReturn.CompanyId != prior.CompanyId || originalReturn.PurchaseInvoiceId != prior.PurchaseInvoiceId)
                return "la devolución vinculada no está cancelada íntegramente.";
            sourceId = returnId;
            fact = "PurchaseReturn";
            wasAuthorized |= originalReturn.AuthorizedAtUtc.HasValue;
            var movements = await stock.GetMovementsByDocumentAsync(tenantId, returnId, "PurchaseReturn", ct);
            if (!wasAuthorized && movements.Count != 0) return "el borrador cancelado tiene movimientos de inventario.";
            if (wasAuthorized)
            {
                // INVENTORY-V1-B3: comparación monetaria (AccountingDecimals); un movimiento histórico
                // con más decimales se compara por su importe, sin modificarlo.
                var moneyDecimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
                decimal? Money(decimal? value) => value is null ? null
                    : Math.Round(value.Value, moneyDecimals, MidpointRounding.AwayFromZero);
                if (movements.Count != originalReturn.Lines.Count * 2) return "faltan movimientos de reversa de inventario.";
                foreach (var line in originalReturn.Lines)
                {
                    var pair = movements.Where(m => m.SourceDocLineId == line.Id).ToList();
                    var exit = pair.FirstOrDefault(m => m.Quantity < 0m);
                    var entry = pair.FirstOrDefault(m => m.Quantity > 0m);
                    if (pair.Count != 2 || exit is null || entry is null || exit.Quantity != -entry.Quantity
                        || exit.ProductId != entry.ProductId || exit.WarehouseId != entry.WarehouseId
                        || exit.ProductId != line.ItemId || exit.WarehouseId != line.WarehouseId
                        || exit.UomCode != entry.UomCode || exit.TotalCost is null
                        || Money(exit.TotalCost) != Money(entry.TotalCost)
                        || Money(exit.TotalCost) != line.HistoricalCostAmount)
                        return "cantidad, bodega o costo de inventario no quedaron íntegramente revertidos.";
                    businessDate = exit.EffectiveDate;
                }
            }
            var credit = await credits.GetBySourcePurchaseReturnIdAsync(tenantId, returnId, ct);
            if (originalReturn.SupplierCreditAmount is > 0m)
            {
                if (credit is null || credit.AvailableAmount != 0m
                    || credit.OriginalAmount != originalReturn.SupplierCreditAmount
                    || credit.Movements.Count(m => m.MovementType == SupplierCreditMovementType.SourceReturnCancelled
                        && m.Amount == credit.OriginalAmount) != 1)
                    return "el SupplierCredit de origen no tiene una reversa íntegra verificable.";
                foreach (var movement in credit.Movements.Where(m => m.MovementType
                    is SupplierCreditMovementType.Application or SupplierCreditMovementType.Refund))
                {
                    var reverseType = movement.MovementType == SupplierCreditMovementType.Application
                        ? SupplierCreditMovementType.ReversalOfApplication : SupplierCreditMovementType.ReversalOfRefund;
                    if (credit.Movements.Count(m => m.ReversalOfMovementId == movement.Id
                        && m.MovementType == reverseType && m.Amount == movement.Amount
                        && m.TargetPurchasePayableId == movement.TargetPurchasePayableId) != 1)
                        return "el SupplierCredit conserva una aplicación o reembolso sin reversa verificable.";
                }
            }
            else if (credit is not null) return "existe un SupplierCredit no explicado por el origen cancelado.";
        }

        var obligation = await postings.FindByKeyAsync(tenantId, prior.CompanyId, "Purchases", fact, sourceId, ct);
        var original = await journal.FindByKeyAsync(tenantId, prior.CompanyId, "Purchases", fact, sourceId, ct);
        if (original is null)
        {
            if (obligation is not null)
                return obligation.Status == AccountingPostingStatus.Canceled && obligation.JournalEntryId is null
                    ? null : "la obligación contable no está cancelada o conserva un asiento sin verificar.";
            if (!wasAuthorized) return null;
            return await accounting.ResolveAsync(tenantId, prior.CompanyId, businessDate, ct) == AccountingApplicability.NotApplicable
                ? null : "no se puede comprobar la ausencia de efectos contables del origen.";
        }
        if (obligation is { IsResolved: false }) return "la contabilización original sigue pendiente o fallida.";
        var reversal = await journal.FindReversalOfAsync(tenantId, prior.CompanyId, original.Id, ct);
        if (reversal is not null)
            reversal = await journal.GetByIdAsync(tenantId, prior.CompanyId, reversal.Id, ct);
        var reversalObligation = await postings.FindByKeyAsync(tenantId, prior.CompanyId, "Accounting", "Reversal", original.Id, ct);
        if (original.Status != JournalEntryStatus.Posted || reversal?.Status != JournalEntryStatus.Posted
            || reversalObligation is { IsResolved: false } || original.Lines.Count == 0
            || await journal.FindReversalOfAsync(tenantId, prior.CompanyId, reversal.Id, ct) is not null)
            return "el reverso contable no está publicado íntegramente o fue revertido.";
        var expected = original.Lines.Select(l => (l.AccountId, Debit: l.Credit, Credit: l.Debit))
            .OrderBy(l => l.AccountId).ThenBy(l => l.Debit).ThenBy(l => l.Credit);
        var actual = reversal.Lines.Select(l => (l.AccountId, l.Debit, l.Credit))
            .OrderBy(l => l.AccountId).ThenBy(l => l.Debit).ThenBy(l => l.Credit);
        return expected.SequenceEqual(actual) ? null : "las partidas del reverso no son el espejo del asiento original.";
    }
}
