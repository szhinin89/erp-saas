using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Setup;
using ERP.Application.Setup.CreateInitialAdmin;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Setup;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Setup;

/// <summary>
/// Fase S1 (hardening 5A). No modifica CreateInitialAdminHandler — cubre que el flujo alternativo
/// seguro sigue funcionando después de eliminar POST /auth/register (anónimo, TenantId/Role
/// arbitrarios del cliente). A diferencia del endpoint eliminado, este flujo: nunca acepta
/// TenantId ni Role del cliente (siempre crea un tenant nuevo con rol Admin fijo), exige un token
/// de instalación de un solo uso generado y mostrado solo en la consola del servidor al arrancar,
/// y queda permanentemente inhabilitado tras el primer uso (SystemSetupState.IsInitialized).
/// </summary>
public sealed class CreateInitialAdminHandlerTests
{
    private const string RawToken = "abc123token";

    private static SystemSetupState NewActiveState()
    {
        var state = SystemSetupState.CreateNew();
        state.IssueSetupToken(SetupTokenCrypto.Hash(RawToken), DateTime.UtcNow.AddMinutes(30));
        return state;
    }

    private sealed class Fixture
    {
        public Mock<ISystemSetupRepository> SetupRepo { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ERP.Domain.Access.Interfaces.IAccessRepository> AccessRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<ERP.Domain.Access.Interfaces.ICompanyUserBranchRepository> CompanyUserBranchRepo { get; } =
            new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<ICompanyProvisioningService> CompanyProvisioning { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            Hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed");

            // Por defecto (sin bootstrap de sucursal en el test) no hay sucursal principal —
            // el handler debe seguir teniendo éxito sin crear CompanyUserBranch en ese caso.
            BranchRepo
                .Setup(r =>
                    r.GetAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<bool?>(),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<Branch>());

            // El handler delega toda la escritura a ExecuteInTransactionAsync — el fake solo corre
            // el delegate inline (sin transacción real), para que los asserts sobre los demás mocks
            // (TenantRepo/AccessRepo/CompanyProvisioning) sigan reflejando lo que el handler hizo.
            UnitOfWork
                .Setup(u =>
                    u.ExecuteInTransactionAsync(
                        It.IsAny<Func<CancellationToken, Task>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(
                    (Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct)
                );
        }

        public CreateInitialAdminHandler BuildHandler() =>
            new(
                SetupRepo.Object,
                TenantRepo.Object,
                AccessRepo.Object,
                BranchRepo.Object,
                CompanyUserBranchRepo.Object,
                Hasher.Object,
                CompanyProvisioning.Object,
                UnitOfWork.Object
            );
    }

    private static CreateInitialAdminCommand ValidCommand(string token = RawToken) =>
        new("ana.perez", "Ana", "Perez", "ana@test.com", "S3curePass!", token);

    [Fact]
    public async Task Token_de_instalacion_valido_y_sistema_no_inicializado_crea_tenant_y_admin_sin_TenantId_ni_Role_del_cliente()
    {
        var state = NewActiveState();
        var f = new Fixture();
        f.SetupRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        f.AccessRepo.Setup(r =>
                r.AnyUserWithUsernameAsync("ana.perez", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.AccessRepo.Setup(r =>
                r.AnyUserWithEmailAsync("ana@test.com", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.CompanyProvisioning.Setup(s =>
                s.EnsureDefaultCompanyAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (Tenant t, CancellationToken _) =>
                    Company.CreateManaged(
                        t.Id,
                        "1790012345001",
                        "Principal S.A.",
                        createdBy: Guid.NewGuid()
                    )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        state
            .IsInitialized.Should()
            .BeTrue("el token debe invalidarse permanentemente tras el primer uso");
        f.TenantRepo.Verify(
            r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.AccessRepo.Verify(
            r =>
                r.AddCompanyUserMembershipAsync(
                    It.Is<ERP.Domain.Access.Entities.CompanyUserMembership>(m => m.Role == "Admin"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.CompanyProvisioning.Verify(
            p => p.EnsureDefaultCompanyAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "el bootstrap de la empresa (sucursal, bodega, perfiles, etc.) ahora es responsabilidad "
                + "interna de CompanyProvisioningService — el handler solo debe garantizar que se invoque"
        );
    }

    /// <summary>
    /// ERP-CORE-CLOSEOUT-06 — sin esto, el admin inicial queda con CompanyUserMembership pero
    /// ninguna CompanyUserBranch: BranchAccessGuard lo bloquea en toda operación branch-scoped y
    /// el selector de sucursal del frontend (no-dismissible) lo deja sin forma de recuperarse
    /// desde la propia app. Verifica que, cuando el bootstrap ya dejó una sucursal principal
    /// activa para la empresa, el handler la autoriza automáticamente en la misma transacción.
    /// </summary>
    [Fact]
    public async Task Autoriza_automaticamente_al_admin_en_la_sucursal_principal_del_bootstrap()
    {
        var state = NewActiveState();
        var f = new Fixture();
        f.SetupRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        f.AccessRepo.Setup(r =>
                r.AnyUserWithUsernameAsync("ana.perez", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        f.AccessRepo.Setup(r =>
                r.AnyUserWithEmailAsync("ana@test.com", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        Company? createdCompany = null;
        f.CompanyProvisioning
            .Setup(s =>
                s.EnsureDefaultCompanyAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (Tenant t, CancellationToken _) =>
                {
                    createdCompany = Company.CreateManaged(
                        t.Id,
                        "1790012345001",
                        "Principal S.A.",
                        createdBy: Guid.NewGuid()
                    );
                    return createdCompany;
                }
            );

        Branch? mainBranch = null;
        f.BranchRepo
            .Setup(r =>
                r.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (Guid tenantId, bool? _, string? _, CancellationToken _) =>
                {
                    mainBranch ??= Branch.Create(
                        tenantId: tenantId,
                        name: "Sucursal Principal",
                        address: "—",
                        code: "001",
                        description: null,
                        reference: null,
                        postalCode: null,
                        phone: null,
                        secondaryPhone: null,
                        email: null,
                        website: null,
                        managerName: null,
                        managerPosition: null,
                        managerEmail: null,
                        managerPhone: null,
                        countryId: null,
                        provinceId: null,
                        cantonId: null,
                        parishId: null,
                        latitude: null,
                        longitude: null,
                        openingDate: null,
                        internalNotes: null,
                        isMainBranch: true,
                        createdBy: Guid.NewGuid(),
                        companyId: createdCompany!.Id
                    );
                    return new List<Branch> { mainBranch };
                }
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.CompanyUserBranchRepo.Verify(
            r =>
                r.AddAsync(
                    It.Is<ERP.Domain.Access.Entities.CompanyUserBranch>(cub =>
                        cub.BranchId == mainBranch!.Id && cub.CompanyId == createdCompany!.Id
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Sistema_ya_inicializado_rechaza_un_segundo_uso_del_flujo()
    {
        var state = NewActiveState();
        state.MarkInitialized("otro@test.com");
        var f = new Fixture();
        f.SetupRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El sistema ya ha sido inicializado.");
        f.TenantRepo.Verify(
            r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Token_incorrecto_no_crea_nada()
    {
        var state = NewActiveState();
        var f = new Fixture();
        f.SetupRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            ValidCommand(token: "token-incorrecto"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Token de inicialización inválido.");
        state.IsInitialized.Should().BeFalse();
        f.TenantRepo.Verify(
            r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_token_de_instalacion_activo_en_el_servidor_rechaza_la_solicitud()
    {
        var f = new Fixture();
        f.SetupRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemSetupState?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.TenantRepo.Verify(
            r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
