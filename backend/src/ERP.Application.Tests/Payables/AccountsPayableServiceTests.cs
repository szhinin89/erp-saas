using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

public sealed class AccountsPayableServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid OriginId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CreateAccountsPayableFromOriginRequest ValidRequest(
        decimal totalAmount = 115m,
        Guid? originId = null
    ) =>
        new(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            AccountsPayableOriginType.ExpenseDocument,
            originId ?? OriginId,
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 27),
            new DateOnly(2026, 8, 27),
            new[] { new AccountsPayableInstallmentInput(1, new DateOnly(2026, 9, 26), totalAmount) }
        );

    private sealed class Mocks
    {
        public Mock<IAccountsPayableRepository> Repo { get; } = new();

        public Mocks()
        {
            Repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.AddAsync(It.IsAny<AccountsPayable>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public AccountsPayableService BuildService() => new(Repo.Object);
    }

    [Fact]
    public async Task Crear_CxP_desde_origen_valido_genera_una_cuota_por_el_total()
    {
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.GetByOriginAsync(TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument, OriginId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccountsPayable?)null);

        var payable = await m.BuildService().CreateFromOriginAsync(ValidRequest(), UserId, CancellationToken.None);

        payable.TotalAmount.Should().Be(115m);
        payable.OutstandingAmount.Should().Be(115m);
        payable.Status.Should().Be(AccountsPayableStatus.Pending);
        payable.Installments.Should().ContainSingle();
        payable.OriginType.Should().Be(AccountsPayableOriginType.ExpenseDocument);
        payable.OriginId.Should().Be(OriginId);
        m.Repo.Verify(r => r.AddAsync(It.IsAny<AccountsPayable>(), It.IsAny<CancellationToken>()), Times.Once);
        m.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reintento_con_el_mismo_origen_no_duplica_devuelve_la_existente()
    {
        var m = new Mocks();
        var existing = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.ExpenseDocument, OriginId,
            "01", "001-001-000000001",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
        );
        existing.AddInstallment(1, new DateOnly(2026, 9, 26), 115m);
        m.Repo
            .Setup(r =>
                r.GetByOriginAsync(TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument, OriginId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        var result = await m.BuildService().CreateFromOriginAsync(ValidRequest(), UserId, CancellationToken.None);

        result.Should().BeSameAs(existing);
        m.Repo.Verify(r => r.AddAsync(It.IsAny<AccountsPayable>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bloquea_total_menor_o_igual_a_cero()
    {
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.GetByOriginAsync(TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument, OriginId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccountsPayable?)null);

        var act = async () =>
            await m.BuildService().CreateFromOriginAsync(ValidRequest(totalAmount: 0m), UserId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        m.Repo.Verify(r => r.AddAsync(It.IsAny<AccountsPayable>(), It.IsAny<CancellationToken>()), Times.Never);
        m.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bloquea_total_negativo()
    {
        var m = new Mocks();
        m.Repo
            .Setup(r =>
                r.GetByOriginAsync(TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument, OriginId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccountsPayable?)null);

        var act = async () =>
            await m.BuildService().CreateFromOriginAsync(ValidRequest(totalAmount: -10m), UserId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
