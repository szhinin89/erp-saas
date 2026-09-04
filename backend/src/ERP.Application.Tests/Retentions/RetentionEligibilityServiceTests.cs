using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — cubre las reglas de "Comportamiento esperado" de
/// docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § Elegibilidad para emitir retenciones. Solo
/// lectura: ningún test de este archivo persiste nada ni toca AccountsPayable/contabilidad — el
/// servicio bajo prueba no depende de IUnitOfWork ni de ningún repositorio de escritura.
/// </summary>
public sealed class RetentionEligibilityServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    [Fact]
    public async Task Empresa_sin_WithholdsVat_no_es_elegible_para_IVA_con_razon_clara()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: false, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303");
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        result.CanRetainVat.Should().BeFalse();
        result.Reasons.Should().Contain(r => r.Contains("WithholdsVat=false"));
        result.CanRetainIncome.Should().BeTrue("la empresa sí retiene renta y el resto de condiciones se cumplen");
    }

    [Fact]
    public async Task Empresa_con_WithholdsVat_y_proveedor_no_exento_y_base_y_codigo_activo_es_elegible()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303");
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        result.CanRetainVat.Should().BeTrue();
        result.SuggestedVatRetentionCode.Should().Be("725");
        result.MissingRetentionCode.Should().BeFalse();
    }

    [Fact]
    public async Task Proveedor_exento_no_es_elegible_aunque_empresa_retenga()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: true, vatCode: "725", incomeCode: "303");
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        result.CanRetainVat.Should().BeFalse();
        result.CanRetainIncome.Should().BeFalse();
        result.IsSupplierExempt.Should().BeTrue();
        result.Reasons.Should().Contain(r => r.Contains("exento"));
    }

    [Fact]
    public async Task Sin_base_retenible_no_es_elegible()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303");
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 0m, 0m, CancellationToken.None);

        result.CanRetainVat.Should().BeFalse();
        result.CanRetainIncome.Should().BeFalse();
        result.HasRetainableBase.Should().BeFalse();
        result.Reasons.Should().Contain(r => r.Contains("base retenible"));
    }

    [Fact]
    public async Task Sin_codigo_activo_no_es_elegible_y_marca_MissingRetentionCode()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303");
        // Ningún código activo configurado en el catálogo (resolver siempre null).

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        result.CanRetainVat.Should().BeFalse();
        result.CanRetainIncome.Should().BeFalse();
        result.MissingRetentionCode.Should().BeTrue();
        result.Reasons.Should().Contain(r => r.Contains("no está activo en el catálogo SRI"));
    }

    [Fact]
    public async Task IsRequiredToKeepAccounting_se_expone_como_dato_informativo_sin_alterar_elegibilidad()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303", isRequiredToKeepAccounting: true);
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        var result = await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        result.IsSupplierRequiredToKeepAccounting.Should().BeTrue();
        result.CanRetainVat.Should().BeTrue("IsRequiredToKeepAccounting no debe bloquear ni cambiar la elegibilidad en esta subfase");
        result.CanRetainIncome.Should().BeTrue();
    }

    [Fact]
    public async Task No_persiste_ni_escribe_nada_solo_lee_repositorios_inyectados()
    {
        var fx = new Fixture();
        fx.SetupCompany(withholdsVat: true, withholdsRenta: true);
        fx.SetupSupplierRole(isExempt: false, vatCode: "725", incomeCode: "303");
        fx.SetupActiveCode("725", "IVA", 30m);
        fx.SetupActiveCode("303", "RENTA", 1.75m);

        await fx.Service.EvaluateAsync(TenantId, CompanyId, SupplierId, 100m, 500m, CancellationToken.None);

        // El servicio solo depende de repos de lectura (GetByIdAsync/GetByTypeAsync/GetRetentionCodeAsync) —
        // no existe ningún método de escritura en las interfaces inyectadas para verificar "no llamado".
        fx.CompanyRepo.Verify(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()), Times.Once);
        fx.RoleRepo.Verify(r => r.GetByTypeAsync(SupplierId, RoleType.Supplier, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture
    {
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IRetentionCodeResolver> RetResolver { get; } = new();

        public IRetentionEligibilityService Service =>
            new RetentionEligibilityService(CompanyRepo.Object, RoleRepo.Object, RetResolver.Object);

        public void SetupCompany(bool withholdsVat, bool withholdsRenta)
        {
            var company = new Company
            {
                Id = CompanyId,
                TenantId = TenantId,
                TaxIdentificationNumber = "1791352688001",
                LegalName = "Empresa Demo S.A.",
                WithholdsVat = withholdsVat,
                WithholdsRenta = withholdsRenta,
            };
            CompanyRepo.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        }

        public void SetupSupplierRole(
            bool isExempt,
            string? vatCode,
            string? incomeCode,
            bool isRequiredToKeepAccounting = false
        )
        {
            var config = SupplierRoleConfig.Create(
                paymentTermId: PaymentTermId,
                defaultRetentionVatCode: vatCode,
                defaultRetentionIncomeCode: incomeCode,
                isRetentionExempt: isExempt,
                isRequiredToKeepAccounting: isRequiredToKeepAccounting
            );
            var role = BusinessPartnerRole.Create(TenantId, SupplierId, RoleType.Supplier, UserId, supplierConfig: config);
            RoleRepo
                .Setup(r => r.GetByTypeAsync(SupplierId, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);
        }

        public void SetupActiveCode(string code, string taxType, decimal percentage) =>
            RetResolver
                .Setup(r => r.GetRetentionCodeAsync(code, taxType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RetentionCodeInfo(code, $"Retención {taxType} {percentage}%", percentage));
    }
}
