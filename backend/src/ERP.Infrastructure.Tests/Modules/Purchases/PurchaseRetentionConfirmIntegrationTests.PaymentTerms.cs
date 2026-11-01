using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(11)]
    public async Task Payment_terms_saved_reopened_and_confirmed_keep_exact_payable_due_date(int days)
    {
        var invoiceId = await SeedPaymentTermsDraftAsync(days, credit: true);
        var expected = new DateOnly(2026, 10, 19).AddDays(days);
        await using (var reopened = CreateContext())
        {
            var invoice = await new PurchaseInvoiceRepository(reopened, new FixedCurrentCompany(_companyId))
                .GetByIdAsync(_tenantId, invoiceId);
            invoice!.PaymentSchedules.Should().ContainSingle(s => s.DueDate == expected);
            invoice.DueDate.Should().Be(expected);
        }

        var result = await ConfirmAsync(invoiceId, null);
        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = CreateContext();
        var persisted = await verify.PurchaseInvoices.AsNoTracking().Include(p => p.PaymentSchedules)
            .SingleAsync(p => p.Id == invoiceId);
        persisted.DueDate.Should().Be(expected);
        persisted.PaymentSchedules.Should().ContainSingle(s => s.DueDate == expected);
        var payable = await verify.Set<AccountsPayable>().AsNoTracking().Include(p => p.Installments)
            .SingleAsync(p => p.OriginId == invoiceId);
        payable.Installments.Should().ContainSingle(s => s.DueDate == expected && s.Amount == persisted.GrandTotal);
    }

    [Fact]
    public async Task Payment_terms_cash_cannot_confirm_credit_schedule_and_preserves_draft()
    {
        var invoiceId = await SeedPaymentTermsDraftAsync(8, credit: false);
        var result = await ConfirmAsync(invoiceId, null);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contado");
        var state = await ReadAsync(invoiceId);
        state.Status.Should().Be(PurchaseStatus.Draft);
        state.Payables.Should().BeEmpty();
        state.StockMovements.Should().Be(0);
    }

    [Fact]
    public async Task Payment_terms_inconsistent_saved_schedule_is_rejected_without_replacing_it()
    {
        var invoiceId = await SeedPaymentTermsDraftAsync(8, credit: true, amount: 1m);
        var result = await ConfirmAsync(invoiceId, null);
        result.IsSuccess.Should().BeFalse();
        var state = await ReadAsync(invoiceId);
        state.Status.Should().Be(PurchaseStatus.Draft);
        state.Payables.Should().BeEmpty();
        await using var db = CreateContext();
        var invoice = await db.PurchaseInvoices.Include(p => p.PaymentSchedules).SingleAsync(p => p.Id == invoiceId);
        invoice.PaymentSchedules.Should().ContainSingle(s => s.Amount == 1m);
    }

    private async Task<Guid> SeedPaymentTermsDraftAsync(int days, bool credit, decimal? amount = null)
    {
        var id = await SeedDraftPurchaseAsync();
        await using var db = CreateContext();
        var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var invoice = (await repo.GetByIdAsync(_tenantId, id))!;
        var issue = new DateOnly(2026, 10, 19);
        invoice.UpdateDraft(_supplierId, invoice.SupplierName, invoice.SupplierTaxId,
            invoice.DocTypeCode, invoice.InvoiceNumber, issue, _userId);
        if (credit)
        {
            var term = PaymentTerm.Create(_tenantId, "PAY-8", "8 días", 1, 8, _userId);
            db.PaymentTerms.Add(term);
            invoice.UpdatePaymentTermSnapshot(term.Id, term.Name, term.Installments, term.DaysBetweenInstallments);
        }
        invoice.ReplaceDraftPaymentSchedule([(1, issue.AddDays(days), amount ?? invoice.GrandTotal, null)]);
        repo.ReattachSchedulesAsAdded(invoice);
        await db.SaveChangesAsync();
        return id;
    }
}
