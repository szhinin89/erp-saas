using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.Banks;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// BANK-CATALOG-01: CQRS de Bancos (List/GetById/Create/Update/Enable/Disable). Los handlers son
/// <c>file sealed class</c> (mismo patrón que <c>PaymentTermUseCases</c>) — no instanciables
/// directamente desde un test; se invocan vía <see cref="IMediator"/> dentro de un contenedor DI
/// real (mismo mecanismo que <c>AddMediatR</c> en producción), igual que
/// <c>BatchDownloadPurchaseReceptionXmlHandlerTests</c>.
/// </summary>
public sealed class BankUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed record Harness(
        IMediator Mediator,
        Mock<IBankRepository> Repo,
        Mock<IUnitOfWork> Uow,
        ServiceProvider Container
    ) : IDisposable
    {
        public void Dispose() => Container.Dispose();
    }

    private static Harness BuildHarness()
    {
        var repo = new Mock<IBankRepository>();
        var uow = new Mock<IUnitOfWork>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(UserId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(uow.Object);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(user.Object);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateBankCommand>());

        var container = services.BuildServiceProvider();
        var mediator = container.GetRequiredService<IMediator>();

        return new Harness(mediator, repo, uow, container);
    }

    private static Bank NewBank(string code = "PICHINCHA") =>
        Bank.Create(TenantId, code, "Banco Pichincha", "Pichincha", UserId);

    [Fact]
    public async Task List_devuelve_los_bancos_mapeados_a_dto()
    {
        using var h = BuildHarness();
        var bank = NewBank();
        h.Repo
            .Setup(r => r.ListAsync(TenantId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([bank]);

        var result = await h.Mediator.Send(new ListBanksQuery());

        result.Should().ContainSingle();
        result[0].Code.Should().Be("PICHINCHA");
        result[0].CountryCode.Should().Be("EC");
    }

    [Fact]
    public async Task GetById_no_encontrado_devuelve_NotFound()
    {
        using var h = BuildHarness();
        h.Repo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bank?)null);

        var result = await h.Mediator.Send(new GetBankByIdQuery(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Create_con_codigo_duplicado_devuelve_Conflict()
    {
        using var h = BuildHarness();
        h.Repo
            .Setup(r =>
                r.ExistsByCodeAsync(TenantId, "EC", "PICHINCHA", null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var result = await h.Mediator.Send(new CreateBankCommand("PICHINCHA", "Banco Pichincha", null));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        h.Repo.Verify(r => r.AddAsync(It.IsAny<Bank>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_exitoso_agrega_y_guarda()
    {
        using var h = BuildHarness();
        h.Repo
            .Setup(r =>
                r.ExistsByCodeAsync(TenantId, "EC", "GUAYAQUIL", null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var result = await h.Mediator.Send(new CreateBankCommand("GUAYAQUIL", "Banco Guayaquil", null));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("GUAYAQUIL");
        h.Repo.Verify(r => r.AddAsync(It.IsAny<Bank>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_modifica_name_y_short_name()
    {
        using var h = BuildHarness();
        var bank = NewBank();
        h.Repo
            .Setup(r => r.GetByIdAsync(TenantId, bank.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);

        var result = await h.Mediator.Send(
            new UpdateBankCommand(bank.Id, "Banco Pichincha S.A.", "BP")
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Banco Pichincha S.A.");
        result.Value!.ShortName.Should().Be("BP");
    }

    [Fact]
    public async Task Enable_y_Disable_cambian_IsActive()
    {
        using var h = BuildHarness();
        var bank = NewBank();
        h.Repo
            .Setup(r => r.GetByIdAsync(TenantId, bank.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bank);

        var disableResult = await h.Mediator.Send(new DisableBankCommand(bank.Id));
        disableResult.IsSuccess.Should().BeTrue();
        bank.IsActive.Should().BeFalse();

        var enableResult = await h.Mediator.Send(new EnableBankCommand(bank.Id));
        enableResult.IsSuccess.Should().BeTrue();
        bank.IsActive.Should().BeTrue();
    }
}
