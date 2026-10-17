using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-06 — reglas del readiness único y del gate Closed/Locked que no dependen
/// de PostgreSQL (sujeción del costo por vigencia, rechazo previo a crear la obligación).
/// </summary>
public sealed class AccountingContinuity06Tests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Costo_pendiente_de_venta_sujeta_bloquea_y_el_de_venta_nacida_en_Disabled_no()
    {
        var enabled = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Enabled,
            new DateOnly(2026, 1, 1), null, UserId);
        enabled.EndBefore(new DateOnly(2026, 7, 16), UserId);
        var disabled = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Disabled,
            new DateOnly(2026, 7, 16), null, UserId);
        var policies = new Mock<IAccountingOperationalPolicyRepository>();
        policies.Setup(p => p.ListAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([enabled, disabled]);
        var costs = new Mock<IInventoryCostLedger>();
        costs.Setup(c => c.GetPendingObligationInvoiceDatesAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20), new DateOnly(2026, 8, 5)]);
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.LocalDateAsync(CompanyId, TenantId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateOnly(2026, 1, 1));
        var service = new AccountingCompletenessService(
            policies.Object,
            clock.Object,
            Mock.Of<IAccountingPostingRepository>(),
            new OpeningAccountingCompleteness(Mock.Of<IOpeningBalanceConstraintsReader>(),
                Mock.Of<IImportBatchRepository>(), Mock.Of<IOpeningBalancePostingRepository>(),
                Mock.Of<IOpeningJournalEntryPostingRepository>()),
            costs.Object,
            Mock.Of<IAccountingRelevantFactsReader>(),
            Mock.Of<IJournalEntryRepository>(),
            Mock.Of<IAccountingPeriodRepository>());

        var result = await service.EvaluateAsync(TenantId, CompanyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), default);

        result.IsComplete.Should().BeFalse();
        result.Reasons.Should().ContainSingle()
            .Which.Should().Be(new AccountingCompletenessReason(AccountingCompletenessCodes.SaleCostPending, 1),
                "solo la venta del 10/07 nació en Enabled; la del 20/07 es Disabled y la de agosto está fuera del alcance");
        AccountingCompletenessService.ToClosureReadiness(result)
            .Should().Be(JournalEntryClosureReadiness.Ready with { HasPendingSaleCost = true });
    }

    [Fact]
    public async Task Hecho_sujeto_con_fecha_en_periodo_Closed_se_rechaza_sin_crear_obligacion_y_en_Disabled_no_aplica()
    {
        var period = AccountingPeriod.Create(TenantId, CompanyId, 2026, 7,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), UserId);
        period.Close(UserId, JournalEntryClosureReadiness.Ready);
        var request = new AccountingPostingRequest(TenantId, CompanyId, "Purchases", "InvoiceReceived",
            Guid.NewGuid(), "001-001-000000001", new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10),
            new AccountingPostingPayload(100m, 15m, 0m, 0m, 115m));

        var enabled = new AccountingContinuityTestHarness(TenantId, CompanyId);
        enabled.Periods.Setup(p => p.FindContainingDateAsync(TenantId, CompanyId, request.EntryDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        var act = () => enabled.Recorder.RecordAsync(request, default);

        (await act.Should().ThrowAsync<DomainRuleViolationException>())
            .WithMessage($"*{AccountingPostingRecorder.PeriodClosedCode}*");
        enabled.Added.Should().BeEmpty();

        var disabledHarness = new AccountingContinuityTestHarness(TenantId, CompanyId, AccountingOperationalMode.Disabled);
        disabledHarness.Periods.Setup(p => p.FindContainingDateAsync(TenantId, CompanyId, request.EntryDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

        await disabledHarness.Recorder.RecordAsync(request, default);

        disabledHarness.Added.Should().BeEmpty();
        disabledHarness.Periods.Verify(p => p.FindContainingDateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
