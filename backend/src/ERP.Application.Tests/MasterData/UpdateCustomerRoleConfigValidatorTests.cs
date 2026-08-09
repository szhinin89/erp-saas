using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// CLASS-BP-CATALOGS-01 — <see cref="UpdateCustomerRoleConfigValidator"/> ya no valida contra un
/// HashSet fijo de Domain: valida de forma async contra los catálogos persistidos
/// tenant+company-scoped. Cubre: código sembrado válido, código fuera del catálogo, null
/// permitido, y el bypass de CustomerClassification cuando el valor no cambia respecto a BD.
/// </summary>
public sealed class UpdateCustomerRoleConfigValidatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private readonly Mock<ICustomerCategoryRepository> _categoryRepo = new();
    private readonly Mock<ICustomerSegmentRepository> _segmentRepo = new();
    private readonly Mock<ICustomerCreditRatingRepository> _creditRatingRepo = new();
    private readonly Mock<ILoyaltyTierRepository> _loyaltyTierRepo = new();
    private readonly Mock<ICustomerInvoiceFormatRepository> _invoiceFormatRepo = new();
    private readonly Mock<ICustomerClassificationRepository> _classificationRepo = new();
    private readonly Mock<IBusinessPartnerRoleRepository> _roleRepo = new();
    private readonly Mock<ICurrentTenant> _tenant = new();
    private readonly Mock<ICurrentCompany> _company = new();

    public UpdateCustomerRoleConfigValidatorTests()
    {
        _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _company.SetupGet(x => x.CompanyId).Returns(CompanyId);
    }

    private UpdateCustomerRoleConfigValidator CreateValidator() =>
        new(
            _categoryRepo.Object,
            _segmentRepo.Object,
            _creditRatingRepo.Object,
            _loyaltyTierRepo.Object,
            _invoiceFormatRepo.Object,
            _classificationRepo.Object,
            _roleRepo.Object,
            _tenant.Object,
            _company.Object
        );

    [Fact]
    public async Task CustomerCategory_con_codigo_sembrado_es_valido()
    {
        _categoryRepo
            .Setup(r => r.CodeExistsActiveAsync(TenantId, CompanyId, "Retail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = new UpdateCustomerRoleConfigCommand(
            RoleId,
            CustomerRoleConfig.Create(customerCategory: "Retail")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("CustomerCategory"));
    }

    [Fact]
    public async Task CustomerCategory_fuera_del_catalogo_de_la_empresa_es_invalido()
    {
        _categoryRepo
            .Setup(r => r.CodeExistsActiveAsync(TenantId, CompanyId, "NoExiste", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cmd = new UpdateCustomerRoleConfigCommand(
            RoleId,
            CustomerRoleConfig.Create(customerCategory: "NoExiste")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("CustomerCategory"));
    }

    [Fact]
    public async Task CustomerCategory_null_es_valido_sin_consultar_el_catalogo()
    {
        var cmd = new UpdateCustomerRoleConfigCommand(RoleId, CustomerRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("CustomerCategory"));
        _categoryRepo.Verify(
            r => r.CodeExistsActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task CustomerClassification_sin_cambios_respecto_a_BD_no_exige_catalogo()
    {
        // Valor legacy fuera del catálogo sembrado, pero el usuario NO lo está cambiando.
        var role = BusinessPartnerRole.Create(
            TenantId,
            Guid.NewGuid(),
            RoleType.Customer,
            ActorId,
            customerConfig: CustomerRoleConfig.Create(customerClassification: "LegacyValue")
        );
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        var cmd = new UpdateCustomerRoleConfigCommand(
            RoleId,
            CustomerRoleConfig.Create(customerClassification: "LegacyValue")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("CustomerClassification"));
        _classificationRepo.Verify(
            r => r.CodeExistsActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task CustomerClassification_cambiada_a_valor_fuera_del_catalogo_es_invalida()
    {
        var role = BusinessPartnerRole.Create(
            TenantId,
            Guid.NewGuid(),
            RoleType.Customer,
            ActorId,
            customerConfig: CustomerRoleConfig.Create(customerClassification: "Nacional")
        );
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _classificationRepo
            .Setup(r =>
                r.CodeExistsActiveAsync(TenantId, CompanyId, "OtroValor", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var cmd = new UpdateCustomerRoleConfigCommand(
            RoleId,
            CustomerRoleConfig.Create(customerClassification: "OtroValor")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("CustomerClassification"));
    }
}
