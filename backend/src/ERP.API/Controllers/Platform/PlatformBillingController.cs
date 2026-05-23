using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Domain.Billing.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — facturación SaaS agregada (control plane).</summary>
[ApiController]
[Route("api/platform/billing")]
[Authorize(Roles = PlatformAuthorizationRoles.BillingReaders)]
[Tags("Platform")]
public sealed class PlatformBillingController : ControllerBase
{
    private readonly ISubscriberRepository _subscribers;
    private readonly ISubscriberBillingRepository _billing;

    public PlatformBillingController(ISubscriberRepository subscribers, ISubscriberBillingRepository billing)
    {
        _subscribers = subscribers;
        _billing = billing;
    }

    /// <summary>Resumen agregado de cuentas SaaS (grace, suspendidos, overdue).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var all = await _subscribers.GetAllAsync(ct);
        var suspended = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Suspended);
        var grace = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod);
        var trial = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Trial);

        var pastDue = await _billing.GetPastDueAccountsAsync(ct);

        return this.ApiOk(new
        {
            totals = new
            {
                subscribers = all.Count,
                suspended,
                gracePeriod = grace,
                trial,
                overdueAccounts = pastDue.Count,
            },
            note = "Detalle por suscriptor vía impersonación → /saas/billing",
        });
    }

    /// <summary>Facturas SaaS recientes (cross-tenant, control plane).</summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Invoices([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var invoices = await _billing.GetRecentInvoicesPlatformAsync(take, ct);
        var subscriberMap = (await _subscribers.GetAllAsync(ct)).ToDictionary(s => s.Id, s => s.Name);

        var rows = invoices.Select(i => new
        {
            i.Id,
            i.SubscriberId,
            subscriberName = subscriberMap.GetValueOrDefault(i.SubscriberId),
            i.InvoiceNumber,
            status = i.Status.ToString(),
            i.Total,
            i.CurrencyCode,
            issuedAtUtc = i.IssuedAtUtc,
            dueAtUtc = i.DueAtUtc,
            paidAtUtc = i.PaidAtUtc,
        });

        return this.ApiOk(new { invoices = rows });
    }

    /// <summary>Cuentas en mora / past due.</summary>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overdue(CancellationToken ct)
    {
        var pastDue = await _billing.GetPastDueAccountsAsync(ct);
        var all = await _subscribers.GetAllAsync(ct);
        var map = all.ToDictionary(s => s.Id);

        var rows = pastDue.Select(a =>
        {
            map.TryGetValue(a.SubscriberId, out var sub);
            return new
            {
                a.SubscriberId,
                subscriberName = sub?.Name,
                lifecycle = sub?.LifecycleStatus.ToString(),
                a.Status,
                a.GracePeriodEndsAtUtc,
                a.CurrentPeriodEndUtc,
            };
        });

        return this.ApiOk(new { accounts = rows });
    }
}
