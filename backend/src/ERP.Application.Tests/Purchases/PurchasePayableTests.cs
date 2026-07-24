using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;
using Xunit;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchasePayableTests
{
    private static readonly Guid TenantId   = Guid.NewGuid();
    private static readonly Guid CompanyId  = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId     = Guid.NewGuid();

    private static List<PurchasePaymentSchedule> ThreeInstallmentSchedule(decimal total)
    {
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var each = Math.Round(total / 3, 2);
        var last = total - each * 2;
        return new List<PurchasePaymentSchedule>
        {
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 1, issueDate.AddDays(30), each),
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 2, issueDate.AddDays(60), each),
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 3, issueDate.AddDays(90), last),
        };
    }

    [Fact]
    public void GenerateInstallments_mirrors_the_confirmed_payment_schedule()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);

        payable.GenerateInstallments(schedule);

        payable.Installments.Should().HaveCount(3);
        payable.Installments.Select(i => i.DueDate).Should().BeEquivalentTo(schedule.Select(s => s.DueDate));
        payable.Installments.Sum(i => i.Amount).Should().Be(300m);
    }

    [Fact]
    public void ApplyRetention_reprorates_across_existing_installments_without_collapsing_to_one()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);
        payable.GenerateInstallments(schedule);

        payable.ApplyRetention(30m, schedule);

        payable.TotalRetained.Should().Be(30m);
        payable.BalanceDue.Should().Be(270m);
        payable.Installments.Should().HaveCount(3, "la retención no debe colapsar el cronograma a una sola cuota");
        payable.Installments.Select(i => i.DueDate).Should().BeEquivalentTo(schedule.Select(s => s.DueDate));
        payable.Installments.Sum(i => i.Amount).Should().Be(270m);
    }

    [Fact]
    public void ReverseRetention_restores_the_original_installment_amounts()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);
        payable.GenerateInstallments(schedule);
        payable.ApplyRetention(30m, schedule);

        payable.ReverseRetention(schedule);

        payable.TotalRetained.Should().Be(0m);
        payable.Installments.Should().HaveCount(3);
        payable.Installments.Sum(i => i.Amount).Should().Be(300m);
    }
}
