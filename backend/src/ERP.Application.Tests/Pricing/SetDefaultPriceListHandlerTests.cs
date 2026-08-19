using ERP.Application.Common;
using ERP.Application.Modules.Pricing.UseCases.PriceLists;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// Verifica que <see cref="SetDefaultPriceListHandler"/> es el único punto de escritura para
/// "lista de precios predeterminada" (consumido por Configuración → Ventas): reasigna el default
/// de forma atómica sin tocar Name/CurrencyCode/etc. de ninguna de las dos listas involucradas —
/// ver auditoría COMPANY-SETTINGS-SALES-SECTION-01.
/// </summary>
public sealed class SetDefaultPriceListHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PriceList NewList(string code, bool isDefault) =>
        PriceList.Create(TenantId, CompanyId, code, code, "USD", isDefault, UserId);

    private static (
        SetDefaultPriceListHandler Handler,
        Mock<IPriceListRepository> Repo,
        Mock<IConfigurationChangeLogger> ChangeLogger
    ) BuildHandler()
    {
        var repo = new Mock<IPriceListRepository>();
        var changeLogger = new Mock<IConfigurationChangeLogger>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(UserId);

        var handler = new SetDefaultPriceListHandler(
            repo.Object,
            changeLogger.Object,
            tenant.Object,
            user.Object
        );
        return (handler, repo, changeLogger);
    }

    [Fact]
    public async Task Handle_lista_no_encontrada_devuelve_NotFound()
    {
        var (handler, repo, changeLogger) = BuildHandler();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);

        var result = await handler.Handle(new SetDefaultPriceListCommand(id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Handle_lista_deshabilitada_devuelve_ValidationFailure()
    {
        var (handler, repo, changeLogger) = BuildHandler();
        var target = NewList("MAYORISTA", isDefault: false);
        target.Disable(UserId);
        repo.Setup(r => r.GetByIdAsync(TenantId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var result = await handler.Handle(
            new SetDefaultPriceListCommand(target.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ya_es_default_no_hace_writes_y_devuelve_Success()
    {
        var (handler, repo, changeLogger) = BuildHandler();
        var target = NewList("GENERAL", isDefault: true);
        repo.Setup(r => r.GetByIdAsync(TenantId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var result = await handler.Handle(
            new SetDefaultPriceListCommand(target.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeTrue();
        repo.Verify(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_reasigna_default_desmarca_la_lista_anterior_y_marca_la_nueva()
    {
        var (handler, repo, changeLogger) = BuildHandler();
        var previousDefault = NewList("GENERAL", isDefault: true);
        var newDefault = NewList("MAYORISTA", isDefault: false);

        repo.Setup(r => r.GetByIdAsync(TenantId, newDefault.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newDefault);
        repo.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousDefault);

        var result = await handler.Handle(
            new SetDefaultPriceListCommand(newDefault.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        newDefault.IsDefault.Should().BeTrue();
        previousDefault.IsDefault.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // CONFIG-FOUNDATION-P2-01: el flip completo se audita — la lista anterior desmarcada y
        // la nueva marcada, ambas con EntityType="PriceList"/FieldName="IsDefault".
        changeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.EntityType == "PriceList"
                        && e.EntityId == previousDefault.Id
                        && e.FieldName == "IsDefault"
                        && e.OldValue == "true"
                        && e.NewValue == "false"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        changeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.EntityType == "PriceList"
                        && e.EntityId == newDefault.Id
                        && e.FieldName == "IsDefault"
                        && e.OldValue == "false"
                        && e.NewValue == "true"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_ya_es_default_no_op_no_genera_ningun_log()
    {
        var (handler, repo, changeLogger) = BuildHandler();
        var target = NewList("GENERAL", isDefault: true);
        repo.Setup(r => r.GetByIdAsync(TenantId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        await handler.Handle(new SetDefaultPriceListCommand(target.Id), CancellationToken.None);

        changeLogger.Verify(
            l => l.LogAsync(It.IsAny<ConfigurationChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
