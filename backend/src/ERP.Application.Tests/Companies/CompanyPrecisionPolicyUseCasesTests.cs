using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.Persistence.Repositories.CompanyConfig;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Usa un <see cref="FakeRepo"/> en memoria (implementa
/// <see cref="ICompanyPrecisionPolicyRepository"/> tal como lo haría EF) para poder ejercer el
/// provider real (<see cref="CompanyPrecisionPolicyProvider"/>, Infrastructure) y los handlers
/// reales sin necesitar PostgreSQL — el fail-closed multi-tenant/multi-company se prueba pasando
/// (TenantId, CompanyId) explícitos a cada operación del fake, igual que haría el repositorio EF
/// real filtrando por esas dos columnas.
/// </summary>
public sealed class CompanyPrecisionPolicyUseCasesTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FakeRepo : ICompanyPrecisionPolicyRepository
    {
        public readonly List<CompanyPrecisionPolicy> Rows = new();
        public bool HasOperations;

        public Task<CompanyPrecisionPolicy?> FindAsync(
            Guid tenantId,
            Guid companyId,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                Rows.FirstOrDefault(r => r.TenantId == tenantId && r.CompanyId == companyId)
            );

        public Task AddAsync(CompanyPrecisionPolicy policy, CancellationToken ct = default)
        {
            Rows.Add(policy);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasRealOperationsAsync(
            Guid tenantId,
            Guid companyId,
            CancellationToken ct = default
        ) => Task.FromResult(HasOperations);
    }

    private static (
        ICompanyPrecisionPolicyProvider Provider,
        FakeRepo Repo,
        Mock<ICurrentTenant> Tenant,
        Mock<ICurrentCompany> Company
    ) BuildProvider(Guid tenantId, Guid companyId, bool seed = true)
    {
        var repo = new FakeRepo();
        // ERP-PRECISION-POLICY-SSOT-CLEANUP-04: la fila la crea el bootstrap de empresa, nunca la lectura.
        if (seed)
            repo.Rows.Add(CompanyPrecisionPolicy.CreateStandardCommercial(tenantId, companyId, UserId));
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(companyId);
        company.Setup(c => c.HasCompanyContext).Returns(true);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var provider = new CompanyPrecisionPolicyProvider(
            repo,
            tenant.Object,
            company.Object,
            user.Object
        );
        return (provider, repo, tenant, company);
    }

    [Fact]
    public async Task Get_de_empresa_sin_fila_lanza_y_NO_crea_ninguna_policy()
    {
        var (provider, repo, _, _) = BuildProvider(TenantA, CompanyA, seed: false);

        var act = () => provider.GetEffectiveAsync();

        await act.Should().ThrowAsync<CompanyPrecisionPolicyMissingException>();
        repo.Rows.Should().BeEmpty("leer nunca escribe");
    }

    [Fact]
    public async Task Get_de_empresa_con_fila_devuelve_los_valores_de_la_empresa()
    {
        var (provider, _, _, _) = BuildProvider(TenantA, CompanyA);

        var dto = await provider.GetEffectiveAsync();

        dto.ProfileType.Should().Be("StandardCommercial");
        dto.SalesUnitPriceDecimals.Should().Be(PrecisionPolicyDefinitions.Standard.SalesUnitPriceDecimals);
    }

    [Fact]
    public async Task GetCompanyPrecisionPolicyHandler_retorna_la_policy_de_la_empresa_activa()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        var handler = new GetCompanyPrecisionPolicyHandler(provider, repo, tenant.Object, company.Object);

        var result = await handler.Handle(
            new GetCompanyPrecisionPolicyQuery(),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProfileType.Should().Be("StandardCommercial");
    }

    [Fact]
    public async Task GetHandler_de_empresa_sin_policy_retorna_NotFound_sin_crear()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA, seed: false);
        var handler = new GetCompanyPrecisionPolicyHandler(provider, repo, tenant.Object, company.Object);

        var result = await handler.Handle(new GetCompanyPrecisionPolicyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHandler_reporta_lock_efectivo_si_hay_operaciones_sin_escribir()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        repo.HasOperations = true;
        var handler = new GetCompanyPrecisionPolicyHandler(provider, repo, tenant.Object, company.Object);

        var result = await handler.Handle(new GetCompanyPrecisionPolicyQuery(), CancellationToken.None);

        result.Value!.IsLocked.Should().BeTrue();
        result.Value.LockedReason.Should().Be(GetCompanyPrecisionPolicyHandler.EffectiveLockReason);
        repo.Rows.Single().IsLocked.Should().BeFalse("GET no persiste el bloqueo");
    }

    [Fact]
    public async Task GetHandler_sin_operaciones_no_esta_bloqueada()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        var handler = new GetCompanyPrecisionPolicyHandler(provider, repo, tenant.Object, company.Object);

        var result = await handler.Handle(new GetCompanyPrecisionPolicyQuery(), CancellationToken.None);

        result.Value!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Update_de_empresa_sin_policy_retorna_NotFound_y_no_crea()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA, seed: false);
        var handler = new UpdateCompanyPrecisionPolicyHandler(repo, provider, tenant.Object, company.Object, MockUser());

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand("StandardCommercial", 2, 4, 4, 2, 6, 6, 6, 0.01m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Metadata_expone_definiciones_y_perfiles_de_PrecisionPolicyDefinitions()
    {
        var result = await new GetPrecisionPolicyMetadataHandler().Handle(
            new GetPrecisionPolicyMetadataQuery(),
            CancellationToken.None
        );

        var meta = result.Value!;
        meta.Fields.Select(f => f.Key).Should().Equal(PrecisionPolicyDefinitions.Fields.Select(f => f.Key));
        var sales = meta.Fields.Single(f => f.Key == "salesUnitPriceDecimals");
        (sales.Min, sales.Max, sales.DefaultValue).Should().Be((2m, 6m, 2m));
        // ERP-PRECISION-CAPACITY-05A: máximos aprobados expuestos por la metadata.
        meta.Fields.ToDictionary(f => f.Key, f => f.Max)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, decimal>
                {
                    ["salesUnitPriceDecimals"] = 6m,
                    ["purchaseUnitPriceDecimals"] = 10m,
                    ["unitCostDecimals"] = 10m,
                    ["averageCostDecimals"] = 10m,
                    ["conversionFactorDecimals"] = 10m,
                    ["quantityDecimals"] = 6m,
                    ["percentageDecimals"] = 6m,
                    ["settlementToleranceAmount"] = 0.02m,
                }
            );
        meta.Fields.Single(f => f.Key == "settlementToleranceAmount").Kind.Should().Be("Amount");
        meta.Profiles.Select(p => p.ProfileType).Should().Equal("StandardCommercial", "HighPrecision");
        meta.Profiles.Single(p => p.ProfileType == "HighPrecision").Values["conversionFactorDecimals"].Should().Be(8m);
        meta.Profiles.Single(p => p.ProfileType == "StandardCommercial").Values["purchaseUnitPriceDecimals"].Should().Be(4m);
    }

    [Fact]
    public void Definiciones_los_perfiles_predefinidos_respetan_los_rangos_y_el_validator_los_usa()
    {
        PrecisionPolicyDefinitions.Standard.EnsureWithinRange();
        PrecisionPolicyDefinitions.HighPrecision.EnsureWithinRange();

        var validator = new UpdateCompanyPrecisionPolicyCommandValidator();
        var ok = new UpdateCompanyPrecisionPolicyCommand("Custom", 6, 10, 0, 6, 10, 10, 10, 0.02m);
        validator.Validate(ok).IsValid.Should().BeTrue();
        validator.Validate(ok with { SalesUnitPriceDecimals = 1 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { SalesUnitPriceDecimals = 7 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { PurchaseUnitPriceDecimals = 11 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { UnitCostDecimals = 11 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { AverageCostDecimals = 11 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { ConversionFactorDecimals = 11 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { QuantityDecimals = 7 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { SettlementToleranceAmount = 0.03m }).IsValid.Should().BeFalse();
        validator.Validate(ok with { ProfileType = "Nope" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Get_sin_contexto_de_empresa_activa_falla_cerrado()
    {
        var repo = new FakeRepo();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantA);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.HasCompanyContext).Returns(false);
        var user = new Mock<ICurrentUser>();

        var provider = new CompanyPrecisionPolicyProvider(
            repo,
            tenant.Object,
            company.Object,
            user.Object
        );

        var act = () => provider.GetEffectiveAsync();

        await act.Should().ThrowAsync<CompanyScopeException>();
    }

    [Fact]
    public async Task Get_incluye_los_3_campos_fiscales_fijos_desde_FiscalPrecision()
    {
        var (provider, _, _, _) = BuildProvider(TenantA, CompanyA);

        var dto = await provider.GetEffectiveAsync();

        dto.MoneyDecimals.Should().Be(ERP.Domain.Common.FiscalPrecision.TaxAmount);
        dto.TaxDecimals.Should().Be(ERP.Domain.Common.FiscalPrecision.TaxAmount);
        dto.AccountingDecimals.Should().Be(ERP.Domain.Common.FiscalPrecision.TaxAmount);
    }

    [Fact]
    public async Task Update_perfil_HighPrecision_carga_los_valores_correctos()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        await provider.GetEffectiveAsync(); // materializa la fila (fallback)

        var handler = new UpdateCompanyPrecisionPolicyHandler(
            repo,
            provider,
            tenant.Object,
            company.Object,
            MockUser()
        );

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand(
                "HighPrecision",
                4,
                6,
                6,
                4,
                6,
                6,
                8,
                0.01m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProfileType.Should().Be("HighPrecision");
        result.Value!.SalesUnitPriceDecimals.Should().Be(4);
        result.Value!.ConversionFactorDecimals.Should().Be(8);
    }

    [Fact]
    public async Task Update_perfil_Custom_fuera_de_rango_falla_validacion()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        await provider.GetEffectiveAsync();

        var handler = new UpdateCompanyPrecisionPolicyHandler(
            repo,
            provider,
            tenant.Object,
            company.Object,
            MockUser()
        );

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand(
                "Custom",
                SalesUnitPriceDecimals: 1, // fuera de rango (min 2)
                PurchaseUnitPriceDecimals: 4,
                QuantityDecimals: 4,
                PercentageDecimals: 2,
                UnitCostDecimals: 6,
                AverageCostDecimals: 6,
                ConversionFactorDecimals: 6,
                SettlementToleranceAmount: 0.01m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Update_antes_de_operar_aplica_normalmente()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        await provider.GetEffectiveAsync();
        repo.HasOperations = false;

        var handler = new UpdateCompanyPrecisionPolicyHandler(
            repo,
            provider,
            tenant.Object,
            company.Object,
            MockUser()
        );

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand(
                "StandardCommercial",
                2,
                4,
                4,
                2,
                6,
                6,
                6,
                0.01m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        repo.Rows.Single().IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Update_despues_de_operar_retorna_409_y_marca_IsLocked()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        await provider.GetEffectiveAsync();
        repo.HasOperations = true; // la empresa ya tiene operación real

        var handler = new UpdateCompanyPrecisionPolicyHandler(
            repo,
            provider,
            tenant.Object,
            company.Object,
            MockUser()
        );

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand(
                "HighPrecision",
                4,
                6,
                6,
                4,
                6,
                6,
                8,
                0.01m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        repo.Rows.Single().IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Update_sobre_policy_ya_bloqueada_retorna_409_sin_reescribir_LockedAt()
    {
        var (provider, repo, tenant, company) = BuildProvider(TenantA, CompanyA);
        await provider.GetEffectiveAsync();
        repo.Rows.Single().Lock("ya bloqueada previamente", UserId);
        var lockedAt = repo.Rows.Single().LockedAt;

        var handler = new UpdateCompanyPrecisionPolicyHandler(
            repo,
            provider,
            tenant.Object,
            company.Object,
            MockUser()
        );

        var result = await handler.Handle(
            new UpdateCompanyPrecisionPolicyCommand(
                "HighPrecision",
                4,
                6,
                6,
                4,
                6,
                6,
                8,
                0.01m
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        repo.Rows.Single().LockedAt.Should().Be(lockedAt);
    }

    [Fact]
    public async Task Cross_company_fail_closed_empresa_A_no_ve_policy_de_empresa_B()
    {
        var (providerA, repoA, _, _) = BuildProvider(TenantA, CompanyA);
        await providerA.GetEffectiveAsync();

        // Misma fila "física" (mismo FakeRepo) consultada con CompanyB nunca debe encontrar la
        // fila de CompanyA — el filtro (TenantId, CompanyId) es la única fuente de aislamiento.
        var rowForOtherCompany = await repoA.FindAsync(TenantA, CompanyB);

        rowForOtherCompany.Should().BeNull();
    }

    [Fact]
    public async Task Cross_tenant_fail_closed_tenant_A_no_ve_policy_de_tenant_B_aunque_companyId_coincida()
    {
        var (providerA, repoA, _, _) = BuildProvider(TenantA, CompanyA);
        await providerA.GetEffectiveAsync();

        var rowForOtherTenant = await repoA.FindAsync(TenantB, CompanyA);

        rowForOtherTenant.Should().BeNull();
    }

    private static ICurrentUser MockUser()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        return user.Object;
    }
}
