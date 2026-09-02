using ERP.Application.Access.UseCases.CreateSystemUser;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Único caso de uso que crea un IdentityUser nuevo fuera de First Run. Cubre: unicidad de
/// username (Conflict), hashing vía IPasswordHasher, RequirePasswordReset forzado (estado inicial
/// coherente sin infraestructura de invitación por email), que la membership se delega
/// íntegramente en UpsertCompanyUserMembershipCommand (ya probado, nunca reimplementada aquí), y
/// atomicidad (Fase E): todo el flujo queda envuelto en IUnitOfWork — Commit solo si la membership
/// también tuvo éxito, Rollback si falla, para que un IdentityUser nunca quede huérfano.
/// </summary>
public sealed class CreateSystemUserHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string Username = "nuevo.usuario";
    private const string Email = "nuevo@test.com";

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CreatedBy);
            Hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed");
            AccessRepo
                .Setup(r =>
                    r.AnyUserWithEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(false);
        }

        public CreateSystemUserHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                Hasher.Object,
                CurrentUser.Object,
                Mediator.Object,
                UnitOfWork.Object
            );
    }

    private static CreateSystemUserCommand ValidCommand(Guid tenantId) =>
        new(tenantId, CompanyId, Username, "Ana", "Perez", Email, "S3curePass!", "User");

    [Fact]
    public async Task Username_ya_existente_devuelve_Conflict_y_no_crea_nada()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.AnyUserWithUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        f.AccessRepo.Verify(
            r => r.AddUserAsync(It.IsAny<IdentityUser>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpsertCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Username_nuevo_crea_el_IdentityUser_con_password_hasheado_y_RequirePasswordReset_activo()
    {
        var tenantId = Guid.NewGuid();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.AnyUserWithUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpsertCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<object>.Success(new { }));

        IdentityUser? addedUser = null;
        f.AccessRepo.Setup(r =>
                r.AddUserAsync(It.IsAny<IdentityUser>(), It.IsAny<CancellationToken>())
            )
            .Callback<IdentityUser, CancellationToken>((u, _) => addedUser = u)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(tenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Username.Should().Be(Username);
        addedUser.Should().NotBeNull();
        addedUser!.PasswordHash.Should().Be("hashed");
        addedUser
            .RequirePasswordReset.Should()
            .BeTrue("el usuario nuevo debe cambiar su contraseña en el primer login");
        f.Hasher.Verify(h => h.HashPassword("S3curePass!"), Times.Once);
        f.UnitOfWork.Verify(
            u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Username_nuevo_delega_la_membership_en_UpsertCompanyUserMembershipCommand_sin_reimplementarla()
    {
        var tenantId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.AnyUserWithUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        UpsertCompanyUserMembershipCommand? sentCommand = null;
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpsertCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<object>>, CancellationToken>(
                (cmd, _) => sentCommand = (UpsertCompanyUserMembershipCommand)cmd
            )
            .ReturnsAsync(Result<object>.Success(new { }));

        var handler = f.BuildHandler();
        var command = new CreateSystemUserCommand(
            tenantId,
            CompanyId,
            Username,
            "Ana",
            "Perez",
            Email,
            "S3curePass!",
            "Admin",
            profileId
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.TenantId.Should().Be(tenantId);
        sentCommand.CompanyId.Should().Be(CompanyId);
        sentCommand.Username.Should().Be(Username);
        sentCommand.Role.Should().Be("Admin");
        sentCommand.ProfileId.Should().Be(profileId);
    }

    [Fact]
    public async Task Falla_de_membership_hace_Rollback_de_la_transaccion_y_nunca_Commit()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.AnyUserWithUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpsertCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<object>.Failure(
                    "El perfil no existe.",
                    ApiResponseCodes.Common.ValidationError
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El perfil no existe.");
        f.AccessRepo.Verify(
            r => r.AddUserAsync(It.IsAny<IdentityUser>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.UnitOfWork.Verify(
            u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.UnitOfWork.Verify(
            u => u.RollbackAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "el IdentityUser ya se guardó en memoria pero la transacción externa nunca debe comitearse si falla la membership"
        );
        f.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
