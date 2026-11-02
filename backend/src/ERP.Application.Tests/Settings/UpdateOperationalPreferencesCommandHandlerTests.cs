using ERP.Application.Common;
using ERP.Application.Modules.Settings.Operations.UseCases.UpdateOperationalPreferences;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Settings;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — UpdateOperationalPreferencesCommand no recibe ningún
/// TenantId/CompanyId externo: todas las escrituras usan ICurrentTenant.TenantId /
/// ICurrentCompany.CompanyId ambientales. Este test prueba que, sin importar qué grupo del
/// command se envíe, cada OrgSetting escrito lleva siempre el tenant/company activo — nunca
/// "otro" tenant/company, cerrando la posibilidad de que un cambio futuro introduzca un
/// parámetro de scope tomado del body en vez del contexto autenticado.
/// </summary>
public sealed class UpdateOperationalPreferencesCommandHandlerTests
{
    private static readonly Guid ActiveTenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid ActiveCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Todas_las_escrituras_usan_el_tenant_y_la_empresa_activos_nunca_otros()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();
        var resolver = new Mock<IOperationalPreferencesResolver>();

        tenant.Setup(t => t.TenantId).Returns(ActiveTenantId);
        company.Setup(c => c.CompanyId).Returns(ActiveCompanyId);
        user.Setup(u => u.UserId).Returns(UserId);

        var written = new List<OrgSetting>();
        repo.Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) => written.Add(s))
            .Returns(Task.CompletedTask);
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    new SalesPosPreferences(true, false, false, 0, null, false, false, null, null),
                    new CashPreferences(true, false, 0, false, false, false),
                    new PurchasesPreferences(null, false, false, false, false),
                    new InventoryPreferences(false, false, false, 0),
                    new PrintingPreferences("Ticket", 1, "58mm", false, false, false, false),
                    new ElectronicDocumentsPreferences(false, 3, false, false),
                    new NotificationsPreferences(false, "es")
                )
            );

        var handler = new UpdateOperationalPreferencesCommandHandler(
            repo.Object,
            tenant.Object,
            company.Object,
            user.Object,
            resolver.Object,
            InlineUnitOfWork().Object,
            ERP.Application.Tests.TestSupport.RetentionElectronicTestDoubles.Emission()
        );

        var command = new UpdateOperationalPreferencesCommand(
            SalesPos: new SalesPosPreferencesInput(
                true,
                false,
                false,
                0,
                null,
                false,
                false,
                null,
                null
            ),
            Cash: new CashPreferencesInput(true, false, 0, false, false, false),
            Purchases: null,
            Inventory: new InventoryPreferencesInput(false, true, false, 0m, StockControlEnabled: false),
            Printing: null,
            ElectronicDocuments: null,
            Notifications: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        written.Should().NotBeEmpty();
        written.Should().OnlyContain(s => s.TenantId == ActiveTenantId);
        written.Should().OnlyContain(s => s.CompanyId == ActiveCompanyId);
        bool.Parse(written.Single(s => s.Key == ERP.Domain.Configuration.Constants.OrgSettingKeys.Inventory.StockControlEnabled).Value!).Should().BeFalse();
        written
            .Should()
            .NotContain(s => s.TenantId == OtherTenantId || s.CompanyId == OtherCompanyId);
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public async Task SRI_TOGGLE_el_interruptor_de_emision_se_escribe_en_la_empresa_activa_via_el_repositorio_auditado(
        bool enabled,
        string persisted
    )
    {
        var repo = new Mock<IOrgSettingsRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        var user = new Mock<ICurrentUser>();
        var resolver = new Mock<IOperationalPreferencesResolver>();
        tenant.Setup(t => t.TenantId).Returns(ActiveTenantId);
        company.Setup(c => c.CompanyId).Returns(ActiveCompanyId);
        user.Setup(u => u.UserId).Returns(UserId);
        var written = new List<OrgSetting>();
        repo.Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) => written.Add(s))
            .Returns(Task.CompletedTask);
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    new SalesPosPreferences(true, false, false, 0, null, false, false, null, null),
                    new CashPreferences(true, false, 0, false, false, false),
                    new PurchasesPreferences(null, false, false, false, false),
                    new InventoryPreferences(false, false, false, 0),
                    new PrintingPreferences("Ticket", 1, "58mm", false, false, false, false),
                    new ElectronicDocumentsPreferences(true, 3, true, true, enabled),
                    new NotificationsPreferences(false, "es")
                )
            );
        var order = new List<string>();
        var unitOfWork = InlineUnitOfWork(order);
        var emission = (ERP.Application.Tests.TestSupport.RetentionElectronicTestDoubles.FixedEmissionSwitch)
            ERP.Application.Tests.TestSupport.RetentionElectronicTestDoubles.Emission();
        repo.Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) =>
            {
                written.Add(s);
                if (s.Key == ERP.Domain.Configuration.Constants.OrgSettingKeys.ElectronicDocuments.EmissionEnabled)
                    order.Add($"upsert:locks={emission.ChangeLocks.Count}");
            })
            .Returns(Task.CompletedTask);
        var handler = new UpdateOperationalPreferencesCommandHandler(
            repo.Object, tenant.Object, company.Object, user.Object, resolver.Object, unitOfWork.Object, emission);

        var result = await handler.Handle(
            new UpdateOperationalPreferencesCommand(
                SalesPos: null, Cash: null, Purchases: null, Inventory: null, Printing: null,
                ElectronicDocuments: new ElectronicDocumentsPreferencesInput(true, 3, true, true, enabled),
                Notifications: null),
            CancellationToken.None);

        result.Value!.ElectronicDocuments.EmissionEnabled.Should().Be(enabled);
        var setting = written.Single(s =>
            s.Key == ERP.Domain.Configuration.Constants.OrgSettingKeys.ElectronicDocuments.EmissionEnabled);
        setting.Value.Should().Be(persisted);
        setting.TenantId.Should().Be(ActiveTenantId);
        setting.CompanyId.Should().Be(ActiveCompanyId);
        setting.ScopeId.Should().Be(ActiveCompanyId);
        // SRI-TOGGLE-04 — lock exclusivo de la empresa activa tomado dentro de la transacción, antes de escribir.
        emission.ChangeLocks.Should().Equal((ActiveTenantId, ActiveCompanyId));
        order.Should().Equal("tx:begin", "upsert:locks=1", "tx:commit");
    }

    private static Mock<IUnitOfWork> InlineUnitOfWork(List<string>? order = null)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (operation, ct) =>
            {
                order?.Add("tx:begin");
                await operation(ct);
                order?.Add("tx:commit");
            });
        return unitOfWork;
    }
}
