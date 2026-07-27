using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// Fase 5.5 — CloseAccountingPeriodHandler. La precondición cross-aggregate (JournalEntry Draft,
/// sin EntryNumber, reversos incompletos) se resuelve vía IJournalEntryRepository.GetClosureReadinessAsync
/// y se pasa a AccountingPeriod.Close — el handler nunca decide directamente, solo orquesta.
/// </summary>
public sealed class CloseAccountingPeriodHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static readonly JournalEntryClosureReadiness Ready = new(false, false, false);

    private static AccountingPeriod OpenPeriod() => AccountingPeriod.Create(
        TenantId, CompanyId, 2026, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CreatedBy);

    private sealed class Mocks
    {
        public Mock<IAccountingPeriodRepository> Periods { get; } = new();
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
        }

        public CloseAccountingPeriodHandler BuildHandler() => new(
            Periods.Object, JournalEntries.Object, Tenant.Object, Company.Object, User.Object);
    }

    [Fact]
    public async Task Cierre_correcto_cuando_la_readiness_esta_lista()
    {
        var period = OpenPeriod();
        var m = new Mocks();
        m.Periods.Setup(r => r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        m.JournalEntries.Setup(r => r.GetClosureReadinessAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ready);

        var result = await m.BuildHandler().Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PeriodStatus.Closed.ToString());
        period.Status.Should().Be(PeriodStatus.Closed);
        m.Periods.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Falla_por_asiento_Draft()
    {
        var period = OpenPeriod();
        var m = new Mocks();
        m.Periods.Setup(r => r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        m.JournalEntries.Setup(r => r.GetClosureReadinessAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ready with { HasDraftOrNonFinalEntries = true });

        var result = await m.BuildHandler().Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sin publicar");
        period.Status.Should().Be(PeriodStatus.Open);
        m.Periods.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Falla_por_asiento_sin_EntryNumber()
    {
        var period = OpenPeriod();
        var m = new Mocks();
        m.Periods.Setup(r => r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        m.JournalEntries.Setup(r => r.GetClosureReadinessAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ready with { HasEntriesWithoutEntryNumber = true });

        var result = await m.BuildHandler().Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("número de asiento");
        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Falla_por_reverso_incompleto()
    {
        var period = OpenPeriod();
        var m = new Mocks();
        m.Periods.Setup(r => r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        m.JournalEntries.Setup(r => r.GetClosureReadinessAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ready with { HasIncompleteReversals = true });

        var result = await m.BuildHandler().Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("reversos contables incompletos");
        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Periodo_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.Periods.Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountingPeriod?)null);

        var result = await m.BuildHandler().Handle(new CloseAccountingPeriodCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        m.JournalEntries.Verify(
            r => r.GetClosureReadinessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
