using ERP.Application.Common;
using ERP.Application.Modules.Pricing.UseCases.PriceLists;
using ERP.Application.Tests.OrgConfig;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01 — Create/Update conservan su contrato y su regla ("ya existe otra
/// predeterminada activa" → 422), pero la bandera <c>IsDefault</c> se aplica solo vía
/// <see cref="IPriceListDefaultAssigner"/>: auditada y sin dejar listas deshabilitadas con la marca.
/// </summary>
public sealed class PriceListCreateUpdateDefaultFlagTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IPriceListRepository> _repo = new();
    private readonly Mock<IConfigurationChangeLogger> _changeLogger = new();
    private readonly Mock<IUnitOfWork> _uow = CompanySalesPreferencesIntegrityTests.TransactionalUnitOfWork();
    private readonly Mock<ICurrentTenant> _tenant = new();
    private readonly Mock<ICurrentCompany> _company = new();
    private readonly Mock<ICurrentUser> _user = new();

    public PriceListCreateUpdateDefaultFlagTests()
    {
        _tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        _company.SetupGet(c => c.CompanyId).Returns(CompanyId);
        _user.SetupGet(u => u.UserId).Returns(UserId);
        _repo.Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceList>());
    }

    private PriceListDefaultAssigner Assigner() => new(_repo.Object, _changeLogger.Object, _uow.Object);

    private CreatePriceListHandler CreateHandler() =>
        new(_repo.Object, _uow.Object, Assigner(), _tenant.Object, _company.Object, _user.Object);

    private UpdatePriceListHandler UpdateHandler() =>
        new(_repo.Object, _uow.Object, Assigner(), _tenant.Object, _user.Object);

    private static PriceList NewList(string code, bool isDefault) =>
        PriceList.Create(TenantId, CompanyId, code, code, "USD", isDefault, UserId);

    private void Exists(PriceList list) =>
        _repo.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);

    private void VerifyLogs(int times) =>
        _changeLogger.Verify(
            l => l.LogAsync(It.IsAny<ConfigurationChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Exactly(times)
        );

    [Fact]
    public async Task Create_default_desmarca_lista_deshabilitada_con_la_bandera_y_audita()
    {
        var stale = NewList("ANTIGUA", isDefault: true);
        stale.Disable(UserId);
        _repo.Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { stale });
        PriceList? added = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()))
            .Callback<PriceList, CancellationToken>((p, _) =>
            {
                added = p;
                Exists(p);
            })
            .Returns(Task.CompletedTask);

        var result = await CreateHandler()
            .Handle(new CreatePriceListCommand("NUEVA", "Nueva", "USD", IsDefault: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeTrue();
        added!.IsDefault.Should().BeTrue();
        stale.IsDefault.Should().BeFalse();
        VerifyLogs(2);
    }

    [Fact]
    public async Task Create_default_con_otra_predeterminada_activa_se_rechaza_como_antes()
    {
        _repo.Setup(r => r.DefaultExistsAsync(TenantId, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateHandler()
            .Handle(new CreatePriceListCommand("NUEVA", "Nueva", "USD", IsDefault: true), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        _repo.Verify(r => r.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_no_default_no_toca_banderas_ni_audita()
    {
        var result = await CreateHandler()
            .Handle(new CreatePriceListCommand("NUEVA", "Nueva", "USD", IsDefault: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeFalse();
        VerifyLogs(0);
    }

    [Fact]
    public async Task Update_que_desmarca_la_predeterminada_la_quita_y_audita()
    {
        var current = NewList("GENERAL", isDefault: true);
        Exists(current);
        _repo.Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { current });

        var result = await UpdateHandler()
            .Handle(new UpdatePriceListCommand(current.Id, "General", "USD", IsDefault: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        current.IsDefault.Should().BeFalse();
        VerifyLogs(1);
    }

    [Fact]
    public async Task Update_que_marca_una_lista_deshabilitada_se_rechaza_y_descarta_la_edicion()
    {
        var disabled = NewList("MAYORISTA", isDefault: false);
        disabled.Disable(UserId);
        Exists(disabled);

        var result = await UpdateHandler()
            .Handle(new UpdatePriceListCommand(disabled.Id, "Otro nombre", "USD", IsDefault: true), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be(PriceListDefaultMessages.Inactive);
        disabled.IsDefault.Should().BeFalse();
        _uow.Verify(u => u.ClearChangeTracker(), Times.Once);
        VerifyLogs(0);
    }

    [Fact]
    public async Task Update_sin_cambio_de_bandera_no_audita_IsDefault()
    {
        var current = NewList("GENERAL", isDefault: true);
        Exists(current);

        var result = await UpdateHandler()
            .Handle(new UpdatePriceListCommand(current.Id, "General renombrada", "USD", IsDefault: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("General renombrada");
        current.IsDefault.Should().BeTrue();
        VerifyLogs(0);
    }
}
