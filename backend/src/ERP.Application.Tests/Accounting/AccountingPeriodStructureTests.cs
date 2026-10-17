using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-07 — estructura de períodos: sin huecos ni períodos detrás de un Closed/Locked
/// (<see cref="AccountingPeriodStructure"/>) y generación mensual idempotente del ejercicio.
/// </summary>
public sealed class AccountingPeriodStructureTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AccountingPeriod Month(int year, int month) =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            year,
            month,
            new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            UserId
        );

    [Fact]
    public void A07_creacion_rechaza_hueco_y_periodo_historico_detras_de_Closed()
    {
        var march = Month(2027, 3);
        march.Close(UserId, ERP.Domain.Modules.Accounting.ValueObjects.JournalEntryClosureReadiness.Ready);
        var existing = new[] { Month(2027, 1), Month(2027, 2), march };

        // Hueco: mayo deja abril sin período.
        var gap = AccountingPeriodStructure.Validate(
            existing,
            new(2027, 4, new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31))
        );
        gap.Should().NotBeNull();
        gap!.IsOverlap.Should().BeFalse();
        gap.Message.Should().Contain("contiguos").And.Contain("2027-04-01");

        // Histórico detrás de un Closed: diciembre 2026 antes de enero..marzo 2027 (marzo Closed).
        var behindClosed = AccountingPeriodStructure.Validate(
            existing,
            new(2026, 12, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))
        );
        behindClosed.Should().NotBeNull();
        behindClosed!.Message.Should().Contain("anterior al período 2027-3").And.Contain("cerrado");

        // El siguiente contiguo sí es válido.
        AccountingPeriodStructure
            .Validate(existing, new(2027, 4, new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 30)))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task A07_GenerateFiscalYear_crea_12_meses_contiguos_e_idempotente()
    {
        var store = new List<AccountingPeriod>
        {
            AccountingPeriod.Create(TenantId, CompanyId, 2026, 1, new(2026, 1, 1), new(2026, 12, 31), UserId),
        };
        var repo = new Mock<IAccountingPeriodRepository>();
        repo.Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => store.ToList());
        repo.Setup(r => r.AddAsync(It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .Callback<AccountingPeriod, CancellationToken>((p, _) => store.Add(p))
            .Returns(Task.CompletedTask);
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        var handler = new GenerateAccountingFiscalYearHandler(
            repo.Object,
            tenant.Object,
            company.Object,
            user.Object,
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var first = await handler.Handle(new GenerateAccountingFiscalYearCommand(2027), CancellationToken.None);
        var second = await handler.Handle(new GenerateAccountingFiscalYearCommand(2027), CancellationToken.None);
        var annualYear = await handler.Handle(new GenerateAccountingFiscalYearCommand(2026), CancellationToken.None);

        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value!.CreatedCount.Should().Be(12);
        var months = first.Value.Periods;
        months.Select(p => p.PeriodNumber).Should().Equal(Enumerable.Range(1, 12));
        months[0].StartDate.Should().Be(new DateOnly(2027, 1, 1));
        months[1].EndDate.Should().Be(new DateOnly(2027, 2, 28));
        months[11].EndDate.Should().Be(new DateOnly(2027, 12, 31));
        months.Zip(months.Skip(1)).Should().OnlyContain(x => x.First.EndDate.AddDays(1) == x.Second.StartDate);

        second.IsSuccess.Should().BeTrue();
        second.Value!.CreatedCount.Should().Be(0);
        second.Value.Periods.Should().HaveCount(12);
        store.Count(p => p.FiscalYear == 2027).Should().Be(12);

        // 2026 anual no se divide: estructura incompatible.
        annualYear.IsSuccess.Should().BeFalse();
        annualYear.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
