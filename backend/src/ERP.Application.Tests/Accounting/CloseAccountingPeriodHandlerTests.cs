using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Tests.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// Fase 5.5 / ACCOUNTING-CONTINUITY-06 — CloseAccountingPeriodHandler. La precondición cross-aggregate
/// se resuelve con el readiness único (<see cref="IAccountingCompletenessEvaluator.EvaluateClosureAsync"/>)
/// bajo el lock exclusivo y se pasa a AccountingPeriod.Close — el handler nunca decide directamente.
/// </summary>
public sealed class CloseAccountingPeriodHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IAccountingPeriodRepository> Periods { get; } = new();
        public Mock<IAccountingCompletenessEvaluator> Completeness { get; } = new();
        public Mock<IAccountingOperationalPolicyRepository> AccountingLock { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
        }

        public AccountingPeriod Seed(params string[] reasonCodes)
        {
            var period = OpenPeriod();
            Periods
                .Setup(r => r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(period);
            Completeness
                .Setup(e => e.EvaluateClosureAsync(TenantId, CompanyId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new AccountingCompleteness(
                        reasonCodes.Length == 0,
                        period.EndDate,
                        reasonCodes.Select(c => new AccountingCompletenessReason(c, 1)).ToList()
                    )
                );
            return period;
        }

        public CloseAccountingPeriodHandler BuildHandler() =>
            new(
                Periods.Object,
                Completeness.Object,
                AccountingLock.Object,
                UnitOfWork.Object,
                Tenant.Object,
                Company.Object,
                User.Object
            );
    }

    [Fact]
    public async Task Cierre_correcto_cuando_la_readiness_esta_lista()
    {
        var m = new Mocks();
        var period = m.Seed();

        var result = await m.BuildHandler()
            .Handle(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PeriodStatus.Closed.ToString());
        period.Status.Should().Be(PeriodStatus.Closed);
        m.Periods.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AccountingCompletenessCodes.JournalEntriesNotFinal, "sin publicar")]
    [InlineData(AccountingCompletenessCodes.JournalEntriesWithoutNumber, "número de asiento")]
    [InlineData(AccountingCompletenessCodes.ReversalsIncomplete, "reversos contables incompletos")]
    [InlineData(AccountingCompletenessCodes.PostingsUnresolved, "contabilizaciones pendientes o fallidas")]
    [InlineData(AccountingCompletenessCodes.OpeningAsiMissing, "apertura contable")]
    [InlineData(AccountingCompletenessCodes.SaleCostPending, "costo de ventas pendiente")]
    [InlineData(AccountingCompletenessCodes.OriginsWithoutAccounting, "sin contabilización registrada")]
    [InlineData(AccountingCompletenessCodes.PreviousPeriodOpen, "período anterior todavía abierto")]
    public async Task Falla_por_cada_motivo_del_readiness_unico(string code, string message)
    {
        var m = new Mocks();
        var period = m.Seed(code);

        var result = await m.BuildHandler()
            .HandleWithDomainRules(new CloseAccountingPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(message);
        period.Status.Should().Be(PeriodStatus.Open);
        m.AccountingLock.Verify(l => l.AcquireExclusiveLockAsync(CompanyId, It.IsAny<CancellationToken>()), Times.Once);
        m.Periods.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Periodo_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.Periods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccountingPeriod?)null);

        var result = await m.BuildHandler()
            .Handle(new CloseAccountingPeriodCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        m.Completeness.Verify(
            e => e.EvaluateClosureAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<AccountingPeriod>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }
}
