using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — costo de ventas bajo la política operativa: la aplicabilidad se ancla a la
/// emisión de la factura. Una venta nacida en Disabled no genera obligaciones de costo (tampoco sus
/// coberturas/devoluciones); una venta nacida sujeta conserva la obligación de sus ajustes posteriores.
/// </summary>
public sealed class AccountingContinuity05Tests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly DisabledFrom = new(2026, 9, 1);

    private static AccountingContinuityTestHarness EnabledThenDisabled()
    {
        var harness = new AccountingContinuityTestHarness(TenantId, CompanyId);
        var enabled = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Enabled,
            new DateOnly(2000, 1, 1), null, AccountingContinuityTestHarness.UserId);
        enabled.EndBefore(DisabledFrom, AccountingContinuityTestHarness.UserId);
        var disabled = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Disabled,
            DisabledFrom, null, AccountingContinuityTestHarness.UserId);
        harness.Policies
            .Setup(p => p.ListAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([enabled, disabled]);
        return harness;
    }

    [Fact]
    public async Task C05_Disabled_evita_obligaciones_de_costo_nuevas_y_conserva_las_de_ventas_sujetas()
    {
        var h = EnabledThenDisabled();
        var precision = new Mock<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>();
        precision.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ERP.Application.Modules.Companies.UseCases.PrecisionPolicy.EffectivePrecisionPolicyDto(
                "StandardCommercial", 2, 4, 4, 2, 6, 6, 6, 0.01m, false, null, null, 2, 2, 2, 2, 2, 2, 2));
        var costs = new SaleCostPostingRecorder(h.Recorder, Mock.Of<ISalesInvoiceRepository>(), precision.Object);
        var afterDisabled = new DateOnly(2026, 10, 1);

        // Venta emitida en vigencia Disabled: ni reconocimiento ni cobertura posterior.
        var disabledInvoice = Guid.NewGuid();
        await costs.RecordAsync(TenantId, CompanyId, disabledInvoice, "001-001-1", new DateOnly(2026, 9, 10),
            disabledInvoice, 6m, new DateOnly(2026, 9, 10), CancellationToken.None);
        await costs.RecordAsync(TenantId, CompanyId, disabledInvoice, "001-001-1", new DateOnly(2026, 9, 10),
            Guid.NewGuid(), 3m, afterDisabled, CancellationToken.None);
        h.Added.Should().BeEmpty();

        // Venta emitida sujeta: su cobertura posterior (ya en Disabled) sigue siendo obligación.
        var subjectInvoice = Guid.NewGuid();
        var coverageId = Guid.NewGuid();
        await costs.RecordAsync(TenantId, CompanyId, subjectInvoice, "001-001-2", new DateOnly(2026, 8, 20),
            coverageId, -1.25m, afterDisabled, CancellationToken.None);

        var posting = h.Added.Should().ContainSingle().Subject;
        posting.SourceModule.Should().Be("Sales");
        posting.FactType.Should().Be("CostOfGoodsSoldReversed");
        posting.SourceEventId.Should().Be(coverageId);
        posting.SourceDocumentId.Should().Be(subjectInvoice);
        posting.BusinessDate.Should().Be(new DateOnly(2026, 8, 20));
        posting.EntryDate.Should().Be(afterDisabled);
        h.SinglePayload().HistoricalCostTotal.Should().Be(1.25m);
    }
}
