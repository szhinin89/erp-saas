using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.Services;
using ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// ZH-API-THIN-BP-ROLES-01 — la construcción de SupplierRoleConfig/CarrierRoleConfig/
/// CustomerRoleConfig pasó del controller a Application (<see cref="RoleConfigFactory"/>). Cubre:
/// el mensaje del invariante sigue siendo exactamente el de Domain con el code histórico
/// (BAD_REQUEST → 400), la config se evalúa antes de resolver rol/BP, y los validators siguen viendo
/// los valores normalizados por Domain con las mismas claves de error.
/// </summary>
public sealed class RoleConfigApplicationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BpId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static string Long(int n) => new('X', n);

    private static string DomainMessage(Action create) =>
        FluentActions.Invoking(create).Should().Throw<ArgumentException>().Which.Message;

    private static SupplierRoleConfigDto Supplier(string? taxSupport = null, string? paymentMethod = null, string? refundType = null) =>
        new(taxSupport, paymentMethod, refundType, false, false);

    private static CarrierRoleConfigDto Carrier(string? auth = null, decimal? capacity = null) => new(auth, capacity);

    private static CustomerRoleConfigDto Customer(
        string? category = null,
        string? segment = null,
        string? salesZone = null,
        string? creditRating = null,
        string? loyaltyTier = null,
        string? invoiceFormat = null,
        string? classification = null
    ) => new(category, segment, salesZone, creditRating, loyaltyTier, invoiceFormat, classification);

    // ── RoleConfigFactory ────────────────────────────────────────────────────

    public static TheoryData<string> InvalidConfigs =>
        new()
        {
            "supplier.taxSupport", "supplier.paymentMethod", "supplier.refundType",
            "carrier.auth", "carrier.capacityZero", "carrier.capacityNegative",
            "customer.category", "customer.segment", "customer.salesZone", "customer.creditRating",
            "customer.loyaltyTier", "customer.invoiceFormat", "customer.classification",
        };

    private static (string? FactoryError, string DomainError) Case(string name) =>
        name switch
        {
            "supplier.taxSupport" => (RoleConfigFactory.Build(Supplier(taxSupport: Long(6))).Error, DomainMessage(() => SupplierRoleConfig.Create(Long(6)))),
            "supplier.paymentMethod" => (RoleConfigFactory.Build(Supplier(paymentMethod: Long(6))).Error, DomainMessage(() => SupplierRoleConfig.Create(defaultPaymentMethodCode: Long(6)))),
            "supplier.refundType" => (RoleConfigFactory.Build(Supplier(refundType: Long(6))).Error, DomainMessage(() => SupplierRoleConfig.Create(refundProviderTypeCode: Long(6)))),
            "carrier.auth" => (RoleConfigFactory.Build(Carrier(auth: Long(51))).Error, DomainMessage(() => CarrierRoleConfig.Create(Long(51)))),
            "carrier.capacityZero" => (RoleConfigFactory.Build(Carrier(capacity: 0m)).Error, DomainMessage(() => CarrierRoleConfig.Create(vehicleCapacityTons: 0m))),
            "carrier.capacityNegative" => (RoleConfigFactory.Build(Carrier(capacity: -1m)).Error, DomainMessage(() => CarrierRoleConfig.Create(vehicleCapacityTons: -1m))),
            "customer.category" => (RoleConfigFactory.Build(Customer(category: Long(51))).Error, DomainMessage(() => CustomerRoleConfig.Create(customerCategory: Long(51)))),
            "customer.segment" => (RoleConfigFactory.Build(Customer(segment: Long(51))).Error, DomainMessage(() => CustomerRoleConfig.Create(customerSegment: Long(51)))),
            "customer.salesZone" => (RoleConfigFactory.Build(Customer(salesZone: Long(101))).Error, DomainMessage(() => CustomerRoleConfig.Create(salesZone: Long(101)))),
            "customer.creditRating" => (RoleConfigFactory.Build(Customer(creditRating: Long(11))).Error, DomainMessage(() => CustomerRoleConfig.Create(creditRating: Long(11)))),
            "customer.loyaltyTier" => (RoleConfigFactory.Build(Customer(loyaltyTier: Long(21))).Error, DomainMessage(() => CustomerRoleConfig.Create(loyaltyTier: Long(21)))),
            "customer.invoiceFormat" => (RoleConfigFactory.Build(Customer(invoiceFormat: Long(21))).Error, DomainMessage(() => CustomerRoleConfig.Create(preferredInvoiceFormat: Long(21)))),
            "customer.classification" => (RoleConfigFactory.Build(Customer(classification: Long(51))).Error, DomainMessage(() => CustomerRoleConfig.Create(customerClassification: Long(51)))),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void Cada_invariante_violado_devuelve_exactamente_el_mensaje_de_Domain(string name)
    {
        var (factoryError, domainError) = Case(name);

        factoryError.Should().Be(domainError);
    }

    [Fact]
    public void Config_valida_se_construye_normalizada_por_Domain()
    {
        var supplier = RoleConfigFactory.Build(new SupplierRoleConfigDto("  01  ", "", null, true, true));
        var carrier = RoleConfigFactory.Build(Carrier("  AUT-1 ", 3m));
        var customer = RoleConfigFactory.Build(Customer(salesZone: "  Norte "));

        supplier.IsValid.Should().BeTrue();
        supplier.Config!.DefaultTaxSupportCode.Should().Be("01");
        supplier.Config.DefaultPaymentMethodCode.Should().BeNull();
        supplier.Config.IsRetentionExempt.Should().BeTrue();
        carrier.Config!.TransportAuthorizationNumber.Should().Be("AUT-1");
        customer.Config!.SalesZone.Should().Be("Norte");
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static Mock<IOperationalContext> Ctx()
    {
        var ctx = new Mock<IOperationalContext>();
        ctx.Setup(c => c.TenantId).Returns(TenantId);
        ctx.Setup(c => c.UserId).Returns(UserId);
        return ctx;
    }

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public async Task Update_con_invariante_violado_responde_BAD_REQUEST_sin_resolver_el_rol(string name)
    {
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        var roleId = Guid.NewGuid();
        var expected = Case(name).DomainError;

        Result<BusinessPartnerRoleDto> result = name.Split('.')[0] switch
        {
            "supplier" => await new UpdateSupplierRoleConfigHandler(roleRepo.Object, Ctx().Object)
                .Handle(new UpdateSupplierRoleConfigCommand(BpId, roleId, SupplierDtoFor(name)), default),
            "carrier" => await new UpdateCarrierRoleConfigHandler(roleRepo.Object, Ctx().Object)
                .Handle(new UpdateCarrierRoleConfigCommand(BpId, roleId, CarrierDtoFor(name)), default),
            _ => await new UpdateCustomerRoleConfigHandler(roleRepo.Object, Ctx().Object)
                .Handle(new UpdateCustomerRoleConfigCommand(BpId, roleId, CustomerDtoFor(name)), default),
        };

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.BadRequest, "contrato histórico: 400, no 422");
        result.Error.Should().Be(expected);
        roleRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        roleRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SupplierRoleConfigDto SupplierDtoFor(string name) =>
        name switch
        {
            "supplier.taxSupport" => Supplier(taxSupport: Long(6)),
            "supplier.paymentMethod" => Supplier(paymentMethod: Long(6)),
            _ => Supplier(refundType: Long(6)),
        };

    private static CarrierRoleConfigDto CarrierDtoFor(string name) =>
        name switch
        {
            "carrier.auth" => Carrier(auth: Long(51)),
            "carrier.capacityZero" => Carrier(capacity: 0m),
            _ => Carrier(capacity: -1m),
        };

    private static CustomerRoleConfigDto CustomerDtoFor(string name) =>
        name switch
        {
            "customer.category" => Customer(category: Long(51)),
            "customer.segment" => Customer(segment: Long(51)),
            "customer.salesZone" => Customer(salesZone: Long(101)),
            "customer.creditRating" => Customer(creditRating: Long(11)),
            "customer.loyaltyTier" => Customer(loyaltyTier: Long(21)),
            "customer.invoiceFormat" => Customer(invoiceFormat: Long(21)),
            _ => Customer(classification: Long(51)),
        };

    [Fact]
    public async Task Update_con_config_valida_aplica_la_config_normalizada_al_rol()
    {
        var role = BusinessPartnerRole.Create(TenantId, BpId, RoleType.Carrier, UserId, carrierConfig: CarrierRoleConfig.Create());
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        roleRepo.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        var result = await new UpdateCarrierRoleConfigHandler(roleRepo.Object, Ctx().Object)
            .Handle(new UpdateCarrierRoleConfigCommand(BpId, role.Id, Carrier("  AUT-9 ", 7m)), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        role.CarrierConfig!.TransportAuthorizationNumber.Should().Be("AUT-9");
        role.CarrierConfig.VehicleCapacityTons.Should().Be(7m);
        roleRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_con_invariante_violado_responde_BAD_REQUEST_antes_de_leer_el_BP()
    {
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        var handler = new AssignBusinessPartnerRoleHandler(
            bpRepo.Object,
            roleRepo.Object,
            Mock.Of<IIdentificationUsageValidator>(),
            Ctx().Object,
            Mock.Of<IDatabaseExceptionTranslator>()
        );

        var result = await handler.Handle(
            new AssignBusinessPartnerRoleCommand(BpId, RoleType.Carrier, CarrierConfig: Carrier(capacity: 0m)),
            default
        );

        result.Code.Should().Be(ApiResponseCodes.Common.BadRequest);
        result.Error.Should().Be(DomainMessage(() => CarrierRoleConfig.Create(vehicleCapacityTons: 0m)));
        bpRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        roleRepo.Verify(r => r.AddAsync(It.IsAny<BusinessPartnerRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assign_con_config_valida_crea_el_rol_con_la_config_normalizada()
    {
        var bp = BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Empresa", UserId);
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo.Setup(r => r.GetByIdAsync(bp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bp);
        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        BusinessPartnerRole? added = null;
        roleRepo.Setup(r => r.AddAsync(It.IsAny<BusinessPartnerRole>(), It.IsAny<CancellationToken>()))
            .Callback<BusinessPartnerRole, CancellationToken>((r, _) => added = r);

        var usage = new Mock<IIdentificationUsageValidator>();
        usage.Setup(u => u.IsAllowedAsync(It.IsAny<string>(), It.IsAny<ERP.Domain.Modules.SriCatalogs.Enums.IdentificationUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new AssignBusinessPartnerRoleHandler(
                bpRepo.Object,
                roleRepo.Object,
                usage.Object,
                Ctx().Object,
                Mock.Of<IDatabaseExceptionTranslator>()
            )
            .Handle(new AssignBusinessPartnerRoleCommand(bp.Id, RoleType.Carrier, CarrierConfig: Carrier(" AUT ", 2m)), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        added!.CarrierConfig!.TransportAuthorizationNumber.Should().Be("AUT");
    }

    // ── Validators: paridad con la config normalizada ────────────────────────

    [Fact]
    public async Task Validator_supplier_consulta_el_catalogo_con_el_valor_normalizado_y_conserva_la_clave()
    {
        var catalog = new Mock<ISriCatalogLookupRepository>();
        catalog.Setup(r => r.TaxSupportCodeExistsActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new UpdateSupplierRoleConfigValidator(catalog.Object);

        var result = await validator.ValidateAsync(new UpdateSupplierRoleConfigCommand(BpId, Guid.NewGuid(), Supplier(taxSupport: "  99  ")));

        catalog.Verify(r => r.TaxSupportCodeExistsActiveAsync("99", It.IsAny<CancellationToken>()), Times.Once);
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Config.DefaultTaxSupportCode");
    }

    [Fact]
    public async Task Validator_supplier_trata_blanco_como_null_y_no_consulta_el_catalogo()
    {
        var catalog = new Mock<ISriCatalogLookupRepository>();
        var validator = new UpdateSupplierRoleConfigValidator(catalog.Object);

        var result = await validator.ValidateAsync(new UpdateSupplierRoleConfigCommand(BpId, Guid.NewGuid(), Supplier(taxSupport: "   ")));

        result.IsValid.Should().BeTrue();
        catalog.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Validators_no_duplican_el_error_de_un_invariante_de_Domain_lo_responde_el_handler()
    {
        var catalog = new Mock<ISriCatalogLookupRepository>();

        var supplier = await new UpdateSupplierRoleConfigValidator(catalog.Object)
            .ValidateAsync(new UpdateSupplierRoleConfigCommand(BpId, Guid.NewGuid(), Supplier(taxSupport: Long(6))));
        var carrier = await new UpdateCarrierRoleConfigValidator()
            .ValidateAsync(new UpdateCarrierRoleConfigCommand(BpId, Guid.NewGuid(), Carrier(capacity: 0m)));

        supplier.IsValid.Should().BeTrue();
        carrier.IsValid.Should().BeTrue();
        catalog.VerifyNoOtherCalls();
    }
}
