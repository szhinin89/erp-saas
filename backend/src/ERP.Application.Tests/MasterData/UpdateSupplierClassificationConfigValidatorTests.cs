using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// CLASS-BP-CATALOGS-01 — <see cref="UpdateSupplierClassificationConfigValidator"/> ya no valida
/// contra los HashSet fijos eliminados de <c>SupplierClassificationConfig</c>: valida de forma
/// async contra los 6 catálogos persistidos de proveedor.
/// </summary>
public sealed class UpdateSupplierClassificationConfigValidatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    private readonly Mock<ISupplierCategoryRepository> _categoryRepo = new();
    private readonly Mock<ISupplierTypeRepository> _typeRepo = new();
    private readonly Mock<ISupplierRiskRepository> _riskRepo = new();
    private readonly Mock<ISupplierRatingRepository> _ratingRepo = new();
    private readonly Mock<IPrimaryGoodTypeRepository> _goodTypeRepo = new();
    private readonly Mock<ISupplierSegmentRepository> _segmentRepo = new();
    private readonly Mock<ICurrentTenant> _tenant = new();
    private readonly Mock<ICurrentCompany> _company = new();

    public UpdateSupplierClassificationConfigValidatorTests()
    {
        _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _company.SetupGet(x => x.CompanyId).Returns(CompanyId);
    }

    private UpdateSupplierClassificationConfigValidator CreateValidator() =>
        new(
            _categoryRepo.Object,
            _typeRepo.Object,
            _riskRepo.Object,
            _ratingRepo.Object,
            _goodTypeRepo.Object,
            _segmentRepo.Object,
            _tenant.Object,
            _company.Object
        );

    [Fact]
    public async Task SupplierRisk_con_codigo_sembrado_es_valido()
    {
        _riskRepo
            .Setup(r => r.CodeExistsActiveAsync(TenantId, CompanyId, "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = new UpdateSupplierClassificationConfigCommand(
            RoleId,
            SupplierClassificationConfig.Create(supplierRisk: "High")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("SupplierRisk"));
    }

    [Fact]
    public async Task SupplierRisk_fuera_del_catalogo_de_la_empresa_es_invalido()
    {
        _riskRepo
            .Setup(r =>
                r.CodeExistsActiveAsync(TenantId, CompanyId, "Inventado", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var cmd = new UpdateSupplierClassificationConfigCommand(
            RoleId,
            SupplierClassificationConfig.Create(supplierRisk: "Inventado")
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SupplierRisk"));
    }

    [Fact]
    public async Task Todos_los_campos_null_son_validos_sin_consultar_ningun_catalogo()
    {
        var cmd = new UpdateSupplierClassificationConfigCommand(
            RoleId,
            SupplierClassificationConfig.Create()
        );

        var result = await CreateValidator().ValidateAsync(cmd);

        result.IsValid.Should().BeTrue();
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
}
