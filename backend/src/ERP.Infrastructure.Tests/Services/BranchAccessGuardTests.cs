using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Companies;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// FASE 4B (ZH-AUTH-BACKEND-COMPANY-BRANCH-ISOLATION-04B) — prueba directamente la implementación
/// real de <see cref="BranchAccessGuard"/>. Caso crítico de aislamiento: un request puede llegar
/// con X-Company-Id de la Empresa A (correcto, con membership real) y X-Branch-Id de una sucursal
/// que en realidad pertenece a la Empresa B — este guard es el único punto que puede detectar esa
/// desincronización y debe rechazarla, sin exponer ningún dato de la sucursal cruzada.
/// </summary>
public sealed class BranchAccessGuardTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICompanyAccessGuard> CompanyGuard { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IOperatorCompanyAccessPolicy> OperatorAccessPolicy { get; } = new();

        public Fixture()
        {
            // Por defecto ningún test de esta clase es un admin global operando — evita que un
            // Mock sin Setup devuelva Task<bool> nulo (NullReferenceException al await).
            OperatorAccessPolicy
                .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        public BranchAccessGuard BuildGuard() =>
            new(
                CompanyGuard.Object,
                BranchRepo.Object,
                CompanyUserBranchRepo.Object,
                AccessRepo.Object,
                OperatorAccessPolicy.Object
            );
    }

    private static Branch NewBranch(Guid tenantId, Guid companyId, string name, bool isMainBranch) =>
        Branch.Create(
            tenantId,
            name,
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch,
            CreatedBy,
            companyId: companyId
        );

    private static CompanyAccessContext ActiveCompanyA(Guid userId, Guid tenantId, Guid companyAId) =>
        new(userId, tenantId, companyAId, "Admin", true, true);

    [Fact]
    public async Task Sucursal_que_pertenece_a_otra_empresa_nunca_se_acepta_aunque_el_tenant_sea_el_mismo()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        var branchDeEmpresaB = NewBranch(tenantId, companyBId, "Sucursal B", isMainBranch: true);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branchDeEmpresaB.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branchDeEmpresaB.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Sucursal no encontrada.");
        f.CompanyUserBranchRepo.Verify(
            r =>
                r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no debe siquiera evaluar autorización de sucursal para una empresa distinta a la activa"
        );
        f.AccessRepo.Verify(
            a =>
                a.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Sucursal_inexistente_en_el_tenant_no_permite_acceso()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branchId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Sucursal no encontrada.");
    }

    [Fact]
    public async Task Sin_empresa_operativa_valida_nunca_llega_a_evaluar_la_sucursal()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Failure("No tiene acceso a esta empresa."));

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
        f.BranchRepo.Verify(
            r =>
                r.GetByIdForCompanyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Sucursal_de_la_empresa_activa_pero_no_autorizada_para_el_usuario_no_permite_acceso()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Sucursal A", isMainBranch: false);
        var membership = CompanyUserMembership.Create(companyAId, userId, "Admin", null, CreatedBy);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyAId, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene autorización para operar en esta sucursal.");
    }

    [Fact]
    public async Task Sucursal_valida_de_la_empresa_activa_y_autorizada_para_el_usuario_permite_el_acceso()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Matriz", isMainBranch: true);
        var membership = CompanyUserMembership.Create(companyAId, userId, "Admin", null, CreatedBy);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyAId, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BranchId.Should().Be(branch.Id);
        result.Value.CompanyId.Should().Be(companyAId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.IsMainBranch.Should().BeTrue();
    }

    /// <summary>
    /// Regresión del bug real encontrado en revisión manual: el mismo usuario tenía una
    /// CompanyUserMembership propia y activa en la empresa (es Admin de esa empresa) pero su
    /// única fila CompanyUserBranch estaba revocada (is_active=false) — el código anterior
    /// devolvía "No tiene autorización para operar en esta sucursal." sin siquiera consultar la
    /// política de operador, porque cortocircuitaba en cuanto encontraba una membership activa,
    /// sin importar si esa membership autorizaba la sucursal. Ahora debe caer a la política de
    /// operador en vez de fallar, exactamente igual que si no hubiera membership.
    /// </summary>
    [Fact]
    public async Task Admin_global_con_membership_propia_pero_CompanyUserBranch_revocada_opera_igual_por_la_politica_de_operador()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Sucursal Principal", isMainBranch: true);
        var membership = CompanyUserMembership.Create(companyAId, userId, "Admin", null, CreatedBy);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyAId, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.CompanyUserBranchRepo.Setup(r =>
                r.ExistsAsync(membership.Id, branch.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BranchId.Should().Be(branch.Id);
    }

    /// <summary>
    /// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01: un admin global operando la empresa (autorizado
    /// por IOperatorCompanyAccessPolicy, la misma política que ya dejó pasar
    /// ICompanyAccessGuard.RequireCurrentCompanyAsync sin CompanyUserMembership) puede operar
    /// cualquier sucursal activa de esa empresa sin fila CompanyUserBranch.
    /// </summary>
    [Fact]
    public async Task Admin_global_autorizado_por_la_politica_de_operador_opera_sucursal_activa_sin_CompanyUserBranch()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Sucursal operada por admin global", isMainBranch: false);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyAId, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BranchId.Should().Be(branch.Id);
        result.Value.CompanyId.Should().Be(companyAId);
        f.CompanyUserBranchRepo.Verify(
            r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "el admin global no tiene membership de la cual derivar CompanyUserBranch"
        );
    }

    /// <summary>
    /// Sin membership y sin autorización de la política de operador (usuario normal, o un
    /// admin global sin operator_mode/GlobalUserRole vigente), el rechazo es el mismo de siempre.
    /// </summary>
    [Fact]
    public async Task Sin_membership_y_sin_autorizacion_de_operador_no_permite_acceso()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Sucursal A", isMainBranch: false);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.AccessRepo.Setup(a =>
                a.GetCompanyUserMembershipAsync(companyAId, userId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta empresa.");
    }

    /// <summary>
    /// El bypass de operador nunca se salta el chequeo de sucursal activa — sigue rechazando
    /// una sucursal deshabilitada aunque el admin global esté autorizado a operar la empresa.
    /// </summary>
    [Fact]
    public async Task Admin_global_autorizado_no_puede_operar_sucursal_inactiva()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var branch = NewBranch(tenantId, companyAId, "Sucursal deshabilitada", isMainBranch: false);
        branch.Disable(CreatedBy);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(branch);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branch.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La sucursal está deshabilitada.");
        f.OperatorAccessPolicy.Verify(
            o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "no debe siquiera evaluar la política de operador para una sucursal inactiva"
        );
    }

    /// <summary>
    /// El bypass de operador nunca se salta el aislamiento de empresa — GetByIdForCompanyAsync ya
    /// acota por companyId/tenantId, así que una sucursal de otra empresa se descarta igual que
    /// para un usuario normal, sin importar que el admin global esté autorizado a operar.
    /// </summary>
    [Fact]
    public async Task Admin_global_autorizado_no_puede_operar_sucursal_de_otra_empresa()
    {
        var f = new Fixture();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        var branchDeEmpresaB = NewBranch(tenantId, companyBId, "Sucursal B", isMainBranch: true);

        f.CompanyGuard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(ActiveCompanyA(userId, tenantId, companyAId)));
        f.BranchRepo.Setup(r =>
                r.GetByIdForCompanyAsync(
                    tenantId,
                    companyAId,
                    branchDeEmpresaB.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Branch?)null);
        f.OperatorAccessPolicy
            .Setup(o => o.IsAuthorizedOperatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guard = f.BuildGuard();
        var result = await guard.RequireBranchAsync(branchDeEmpresaB.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Sucursal no encontrada.");
    }
}
