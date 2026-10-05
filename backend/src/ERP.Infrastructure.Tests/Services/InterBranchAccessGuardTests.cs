using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// ZH-SCOPE-ERROR-SEMANTICS-01 — <see cref="InterBranchAccessGuard"/> (transferencias entre
/// bodegas): todo fallo lleva código, la sucursal operativa pasa por la validación de contexto y
/// una bodega de otra empresa es indistinguible de una inexistente (también si está deshabilitada).
/// </summary>
public sealed class InterBranchAccessGuardTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OperatingBranch = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICompanyAccessGuard> CompanyGuard { get; } = new();
        public Mock<IBranchAccessGuard> BranchGuard { get; } = new();
        public Mock<IWarehouseRepository> Warehouses { get; } = new();
        public Mock<ICurrentBranch> CurrentBranch { get; } = new();

        public Fixture()
        {
            CompanyGuard
                .Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Result<CompanyAccessContext>.Success(
                        new CompanyAccessContext(UserId, TenantId, CompanyA, "Admin", true, true)
                    )
                );
            CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
            CurrentBranch.Setup(b => b.BranchId).Returns(OperatingBranch);
            BranchGuard
                .Setup(g => g.RequireCurrentBranchAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Allowed(OperatingBranch));
            BranchGuard
                .Setup(g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Allowed(id));
        }

        public Warehouse Add(Guid companyId, bool active = true)
        {
            var w = Warehouse.Create(
                TenantId,
                Guid.NewGuid(),
                "Bodega",
                "B1",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                UserId,
                companyId
            );
            if (!active)
                w.Disable(UserId);
            Warehouses
                .Setup(r => r.GetByIdAsync(TenantId, w.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(w);
            return w;
        }

        public InterBranchAccessGuard Build() =>
            new(CompanyGuard.Object, BranchGuard.Object, Warehouses.Object, CurrentBranch.Object);
    }

    private static Result<BranchAccessContext> Allowed(Guid branchId) =>
        Result<BranchAccessContext>.Success(
            new BranchAccessContext(UserId, TenantId, CompanyA, branchId, "S", false)
        );

    [Fact]
    public async Task Bodega_de_otra_empresa_activa_o_deshabilitada_es_identica_a_inexistente()
    {
        var f = new Fixture();
        var target = f.Add(CompanyA);
        var foreignActive = f.Add(CompanyB);
        var foreignDisabled = f.Add(CompanyB, active: false);
        var guard = f.Build();

        var nonexistent = await guard.RequireInterBranchAccessAsync(Guid.NewGuid(), target.Id);
        var foreign = await guard.RequireInterBranchAccessAsync(foreignActive.Id, target.Id);
        var foreignOff = await guard.RequireInterBranchAccessAsync(foreignDisabled.Id, target.Id);

        nonexistent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (foreign.Code, foreign.Error).Should().Be((nonexistent.Code, nonexistent.Error));
        (foreignOff.Code, foreignOff.Error).Should().Be((nonexistent.Code, nonexistent.Error));
    }

    [Fact]
    public async Task Bodega_propia_deshabilitada_es_VALIDATION_ERROR()
    {
        var f = new Fixture();
        var source = f.Add(CompanyA, active: false);
        var target = f.Add(CompanyA);

        var result = await f.Build().RequireInterBranchAccessAsync(source.Id, target.Id);

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Sucursal_operativa_invalida_falla_cerrado_con_BRANCH_SCOPE_FORBIDDEN_sin_leer_bodegas()
    {
        var f = new Fixture();
        f.BranchGuard.Setup(g => g.RequireCurrentBranchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Failure(
                    "Sucursal no encontrada.",
                    ApiResponseCodes.Common.BranchScopeForbidden
                )
            );

        var result = await f.Build().RequireInterBranchAccessAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Code.Should().Be(ApiResponseCodes.Common.BranchScopeForbidden);
        f.Warehouses.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_acceso_a_la_sucursal_de_la_bodega_propaga_el_codigo_del_guard()
    {
        var f = new Fixture();
        var source = f.Add(CompanyA);
        var target = f.Add(CompanyA);
        f.BranchGuard.Setup(g =>
                g.RequireBranchAsync(source.BranchId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<BranchAccessContext>.Forbidden(
                    "No tiene autorización para operar en esta sucursal."
                )
            );

        var result = await f.Build().RequireInterBranchAccessAsync(source.Id, target.Id);

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        result
            .Error.Should()
            .Be("Sucursal de origen: No tiene autorización para operar en esta sucursal.");
    }

    [Fact]
    public async Task Empresa_invalida_propaga_su_codigo()
    {
        var f = new Fixture();
        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Failure(
                    "No tiene acceso a esta empresa.",
                    ApiResponseCodes.Common.CompanyScopeForbidden
                )
            );

        var result = await f.Build().RequireInterBranchAccessAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Code.Should().Be(ApiResponseCodes.Common.CompanyScopeForbidden);
    }
}
