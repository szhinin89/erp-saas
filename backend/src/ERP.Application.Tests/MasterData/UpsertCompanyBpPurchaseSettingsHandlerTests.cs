using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>ADR-033, Fase 3d.</summary>
public sealed class UpsertCompanyBpPurchaseSettingsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICompanyBpPurchaseSettingsRepository> SettingsRepo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IPaymentTermRepository> PtRepo { get; } = new();
        public Mock<IOperationalContext> Ctx { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public BusinessPartner Supplier { get; } =
            BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor Demo", UserId);

        public PaymentTerm ActivePaymentTerm { get; } =
            PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);

        public Fixture()
        {
            Ctx.Setup(c => c.TenantId).Returns(TenantId);
            Ctx.Setup(c => c.CompanyId).Returns(CompanyId);
            Ctx.Setup(c => c.UserId).Returns(UserId);
            Ctx.Setup(c => c.HasCompany).Returns(true);

            BpRepo
                .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);

            var role = BusinessPartnerRole.Create(
                TenantId, SupplierId, RoleType.Supplier, UserId,
                ERP.Domain.MasterData.ValueObjects.SupplierRoleConfig.Create()
            );
            RoleRepo
                .Setup(r => r.GetByTypeAsync(SupplierId, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            PtRepo
                .Setup(r => r.GetByIdAsync(TenantId, ActivePaymentTerm.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActivePaymentTerm);

            SettingsRepo
                .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CompanyBpPurchaseSettings?)null);
        }

        public UpsertCompanyBpPurchaseSettingsHandler BuildHandler() =>
            new(SettingsRepo.Object, BpRepo.Object, RoleRepo.Object, PtRepo.Object, Ctx.Object, DbEx.Object);
    }

    [Fact]
    public async Task Crea_configuracion_cuando_no_existe()
    {
        var f = new Fixture();
        var handler = f.BuildHandler();

        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, f.ActivePaymentTerm.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentTermId.Should().Be(f.ActivePaymentTerm.Id);
        result.Value.HasCustomConfiguration.Should().BeTrue();
        f.SettingsRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyBpPurchaseSettings>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Actualiza_configuracion_existente()
    {
        var f = new Fixture();
        var existing = CompanyBpPurchaseSettings.Create(TenantId, CompanyId, SupplierId, null, UserId);
        f.SettingsRepo
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, f.ActivePaymentTerm.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentTermId.Should().Be(f.ActivePaymentTerm.Id);
        f.SettingsRepo.Verify(
            r => r.AddAsync(It.IsAny<CompanyBpPurchaseSettings>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PaymentTermId_null_limpia_el_default()
    {
        var f = new Fixture();
        var existing = CompanyBpPurchaseSettings.Create(
            TenantId, CompanyId, SupplierId, f.ActivePaymentTerm.Id, UserId
        );
        f.SettingsRepo
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PaymentTermId.Should().BeNull();
    }

    [Fact]
    public async Task Rechaza_proveedor_inexistente()
    {
        var f = new Fixture();
        f.BpRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Rechaza_proveedor_inactivo()
    {
        var f = new Fixture();
        f.Supplier.Deactivate(UserId);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactivo");
    }

    [Fact]
    public async Task Rechaza_sin_rol_Supplier_activo()
    {
        var f = new Fixture();
        f.RoleRepo
            .Setup(r => r.GetByTypeAsync(SupplierId, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartnerRole?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rol de Proveedor");
    }

    [Fact]
    public async Task Rechaza_PaymentTerm_inexistente()
    {
        var f = new Fixture();
        var missingId = Guid.NewGuid();
        f.PtRepo
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTerm?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe");
    }

    [Fact]
    public async Task Rechaza_PaymentTerm_inactivo()
    {
        var f = new Fixture();
        f.ActivePaymentTerm.Disable(UserId);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, f.ActivePaymentTerm.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
    }

    [Fact]
    public async Task Conflicto_de_unicidad_devuelve_Conflict()
    {
        var f = new Fixture();
        f.SettingsRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("duplicate key"));
        var violationInfo = new DatabaseUniqueViolationInfo("23505", "uq_cbps_company_bp", "master_company_bp_purchase_settings", null);
        f.DbEx
            .Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out violationInfo))
            .Returns(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
    }

    [Fact]
    public async Task Aislamiento_empresa_A_vs_empresa_B_no_se_mezclan()
    {
        // El aislamiento tenant/company real ya está probado contra Postgres en
        // CompanyBpPurchaseSettingsRepositoryTests (Fase 3a). Aquí se confirma que el handler usa
        // _ctx.CompanyId (empresa activa del contexto) al crear, sin mezclar con otra empresa.
        var companyA = Guid.NewGuid();
        var f = new Fixture();
        f.Ctx.Setup(c => c.CompanyId).Returns(companyA);
        CompanyBpPurchaseSettings? captured = null;
        f.SettingsRepo
            .Setup(r => r.AddAsync(It.IsAny<CompanyBpPurchaseSettings>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyBpPurchaseSettings, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        await handler.Handle(
            new UpsertCompanyBpPurchaseSettingsCommand(SupplierId, f.ActivePaymentTerm.Id),
            CancellationToken.None
        );

        captured!.CompanyId.Should().Be(companyA);
    }
}
