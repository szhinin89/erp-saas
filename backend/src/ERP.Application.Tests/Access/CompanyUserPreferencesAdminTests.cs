using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase F: administración explícita de CompanyUserPreferences. Ambos handlers delegan
/// íntegramente en los UseCases de Fase C vía MediatR — estos tests verifican, sobre todo, que
/// el único agregado propio (aislamiento por empresa operativa actual) funciona y que ninguna
/// validación de Fase C (LoginMode/DefaultBranchId/autorización de sucursal) se reimplementa.
/// </summary>
public sealed class CompanyUserPreferencesAdminTests
{
    private static readonly Guid CurrentCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();

    private static CompanyUserMembership Membership(Guid companyId) =>
        CompanyUserMembership.Create(companyId, Guid.NewGuid(), "User", null, Guid.NewGuid());

    private static CompanyUserPreferencesDto Preferences(
        Guid membershipId,
        CompanyUserLoginMode loginMode,
        Guid? defaultBranchId
    ) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CurrentCompanyId,
            membershipId,
            defaultBranchId,
            loginMode.ToString()
        );

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public Guid CompanyId => CurrentCompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    // ── GetCompanyUserPreferencesAdminQuery ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_devuelve_las_preferencias_existentes_de_una_membresia_de_la_empresa_actual()
    {
        var membership = Membership(CurrentCompanyId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.DirectToDefault, Guid.NewGuid())
                )
            );

        var handler = new GetCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );
        var result = await handler.Handle(
            new GetCompanyUserPreferencesAdminQuery(membership.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CompanyUserId.Should().Be(membership.Id);
        result.Value.LoginMode.Should().Be(nameof(CompanyUserLoginMode.DirectToDefault));
    }

    [Fact]
    public async Task Get_de_membresia_de_otro_tenant_o_empresa_devuelve_NotFound()
    {
        var membership = Membership(OtherCompanyId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        var handler = new GetCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );

        var result = await handler.Handle(
            new GetCompanyUserPreferencesAdminQuery(membership.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        mediator.Verify(
            m => m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Get_de_usuario_inexistente_devuelve_NotFound()
    {
        var missingId = Guid.NewGuid();
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = new GetCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            new Mock<IMediator>().Object
        );
        var result = await handler.Handle(
            new GetCompanyUserPreferencesAdminQuery(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── UpdateCompanyUserPreferencesAdminCommand ────────────────────────────────────────────

    [Fact]
    public async Task Update_LoginMode_delega_en_el_UseCase_de_Fase_C_y_devuelve_el_resultado()
    {
        var membership = Membership(CurrentCompanyId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.AskBranch, null)
                )
            );
        UpdateCompanyUserPreferencesCommand? sentCommand = null;
        mediator
            .Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<CompanyUserPreferencesDto>>, CancellationToken>(
                (cmd, _) => sentCommand = (UpdateCompanyUserPreferencesCommand)cmd
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.AskBranch, null)
                )
            );

        var handler = new UpdateCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );
        var command = new UpdateCompanyUserPreferencesAdminCommand(
            membership.Id,
            nameof(CompanyUserLoginMode.AskBranch),
            null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LoginMode.Should().Be(nameof(CompanyUserLoginMode.AskBranch));
        sentCommand.Should().NotBeNull();
        sentCommand!.CompanyUserMembershipId.Should().Be(membership.Id);
    }

    [Fact]
    public async Task Update_DefaultBranch_valido_funciona()
    {
        var membership = Membership(CurrentCompanyId);
        var branchId = Guid.NewGuid();
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.AskBranch, null)
                )
            );
        mediator
            .Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.DirectToDefault, branchId)
                )
            );

        var handler = new UpdateCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );
        var command = new UpdateCompanyUserPreferencesAdminCommand(
            membership.Id,
            nameof(CompanyUserLoginMode.DirectToDefault),
            branchId
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultBranchId.Should().Be(branchId);
    }

    /// <summary>
    /// La autorización de sucursal (CompanyUserBranch) no se reimplementa aquí — este test
    /// prueba que el fallo generado por el UseCase de Fase C se propaga sin alterar su código.
    /// </summary>
    [Fact]
    public async Task Update_DefaultBranch_no_autorizado_falla_propagando_el_ValidationFailure_de_Fase_C()
    {
        var membership = Membership(CurrentCompanyId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    Preferences(membership.Id, CompanyUserLoginMode.AskBranch, null)
                )
            );
        mediator
            .Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.ValidationFailure(
                    "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."
                )
            );

        var handler = new UpdateCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );
        var command = new UpdateCompanyUserPreferencesAdminCommand(
            membership.Id,
            nameof(CompanyUserLoginMode.DirectToDefault),
            Guid.NewGuid()
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Update_de_membresia_de_otro_tenant_o_empresa_falla_y_no_llega_a_invocar_Fase_C()
    {
        var membership = Membership(OtherCompanyId);
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var mediator = new Mock<IMediator>();
        var handler = new UpdateCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            mediator.Object
        );
        var command = new UpdateCompanyUserPreferencesAdminCommand(
            membership.Id,
            nameof(CompanyUserLoginMode.AskBranch),
            null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Update_de_usuario_inexistente_falla()
    {
        var missingId = Guid.NewGuid();
        var accessRepo = new Mock<IAccessRepository>();
        accessRepo
            .Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(missingId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = new UpdateCompanyUserPreferencesAdminHandler(
            accessRepo.Object,
            new CurrentCompanyStub(),
            new Mock<IMediator>().Object
        );
        var command = new UpdateCompanyUserPreferencesAdminCommand(
            missingId,
            nameof(CompanyUserLoginMode.AskBranch),
            null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
