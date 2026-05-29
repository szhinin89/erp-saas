using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Tests.Billing;

/// <summary>
/// Reusable test factories for SaaS commercial flow scenarios.
/// Use these instead of constructing entities inline in tests.
/// </summary>
public static class SaasTestFactories
{
    // ── Subscribers ───────────────────────────────────────────────────────────

    public static Subscriber ActiveSubscriber(string name = "Corp", string slug = "corp", string? planCode = "starter")
        => Subscriber.Create(name, slug, Guid.NewGuid(), planCode: planCode);

    public static Subscriber SuspendedSubscriber(string reason = "non-payment")
    {
        var s = ActiveSubscriber();
        s.Suspend(reason, Guid.NewGuid());
        return s;
    }

    public static Subscriber TrialSubscriber()
    {
        var s = ActiveSubscriber();
        s.SetTrial(Guid.NewGuid());
        return s;
    }

    public static Subscriber GracePeriodSubscriber()
    {
        var s = TrialSubscriber();
        s.EnterGracePeriod(Guid.NewGuid());
        return s;
    }

    // ── Billing Accounts ──────────────────────────────────────────────────────

    public static SubscriberBillingAccount ActiveBillingAccount(Guid subscriberId)
        => SubscriberBillingAccount.Create(subscriberId, $"{subscriberId}@test.com", Guid.NewGuid());

    public static SubscriberBillingAccount TrialingBillingAccount(Guid subscriberId, int trialDays = 14)
        => SubscriberBillingAccount.Create(
            subscriberId, $"{subscriberId}@test.com", Guid.NewGuid(),
            trialState: BillingTrialState.Active,
            trialEndsAtUtc: DateTime.UtcNow.AddDays(trialDays));

    public static SubscriberBillingAccount ExpiredTrialAccount(Guid subscriberId)
        => SubscriberBillingAccount.Create(
            subscriberId, $"{subscriberId}@test.com", Guid.NewGuid(),
            trialState: BillingTrialState.Active,
            trialEndsAtUtc: DateTime.UtcNow.AddDays(-1));

    public static SubscriberBillingAccount GracePeriodAccount(Guid subscriberId, int graceDays = 7)
    {
        var a = ActiveBillingAccount(subscriberId);
        a.EnterGracePeriod(DateTime.UtcNow.AddDays(graceDays), Guid.NewGuid());
        return a;
    }

    public static SubscriberBillingAccount ExpiredGracePeriodAccount(Guid subscriberId)
    {
        var a = ActiveBillingAccount(subscriberId);
        a.EnterGracePeriod(DateTime.UtcNow.AddSeconds(-1), Guid.NewGuid());
        return a;
    }

    public static SubscriberBillingAccount SuspendedBillingAccount(Guid subscriberId)
    {
        var a = ActiveBillingAccount(subscriberId);
        a.Suspend(Guid.NewGuid());
        return a;
    }

    public static SubscriberBillingAccount CancelledBillingAccount(Guid subscriberId)
    {
        var a = ActiveBillingAccount(subscriberId);
        a.Cancel(BillingRenewalState.Cancelled, Guid.NewGuid());
        return a;
    }

    // ── Invoices ──────────────────────────────────────────────────────────────

    public static SaasBillingInvoice DraftInvoice(Guid subscriberId, string planCode = "starter", decimal amount = 49.00m)
    {
        var now = DateTime.UtcNow;
        var inv = SaasBillingInvoice.CreateDraft(
            subscriberId:   subscriberId,
            invoiceNumber:  $"INV-{now:yyyyMM}-TEST-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            periodStartUtc: now,
            periodEndUtc:   now.AddMonths(1),
            currencyCode:   "USD",
            createdBy:      Guid.NewGuid());

        var line = SaasBillingInvoiceLine.Create(
            subscriberId:     subscriberId,
            billingInvoiceId: inv.Id,
            lineType:         SaasBillingInvoiceLineType.Subscription,
            description:      $"Plan {planCode} — {now:MMM yyyy}",
            quantity:         1m,
            unitAmount:       amount);

        inv.AddLine(line);
        return inv;
    }

    public static SaasBillingInvoice OpenInvoice(Guid subscriberId, string planCode = "starter", decimal amount = 49.00m)
    {
        var inv = DraftInvoice(subscriberId, planCode, amount);
        inv.Issue(DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        return inv;
    }

    public static SaasBillingInvoice PaidInvoice(Guid subscriberId, string planCode = "starter")
    {
        var inv = OpenInvoice(subscriberId, planCode);
        inv.MarkPaid(Guid.NewGuid());
        return inv;
    }

    // ── Scenarios (composed) ──────────────────────────────────────────────────

    /// <summary>Fully operational subscriber: Active lifecycle + Active billing account.</summary>
    public static (Subscriber subscriber, SubscriberBillingAccount account) ActiveTenant()
    {
        var sub     = ActiveSubscriber();
        var account = ActiveBillingAccount(sub.Id);
        return (sub, account);
    }

    /// <summary>Trial tenant: Trial lifecycle + Trialing billing account.</summary>
    public static (Subscriber subscriber, SubscriberBillingAccount account) TrialTenant(int trialDays = 14)
    {
        var sub     = TrialSubscriber();
        var account = TrialingBillingAccount(sub.Id, trialDays);
        return (sub, account);
    }

    /// <summary>Blocked tenant: Suspended lifecycle + Suspended billing account.</summary>
    public static (Subscriber subscriber, SubscriberBillingAccount account) SuspendedTenant()
    {
        var sub     = SuspendedSubscriber();
        var account = SuspendedBillingAccount(sub.Id);
        return (sub, account);
    }

    /// <summary>ReadOnly tenant: GracePeriod lifecycle + GracePeriod billing account.</summary>
    public static (Subscriber subscriber, SubscriberBillingAccount account) GracePeriodTenant(int graceDays = 7)
    {
        var sub     = GracePeriodSubscriber();
        var account = GracePeriodAccount(sub.Id, graceDays);
        return (sub, account);
    }
}
