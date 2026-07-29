using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase F/S1: envoltorio administrativo que delega en UpdateCompanyUserPreferencesCommand (Fase C,
/// probado aparte) — estos tests cubren solo el agregado propio: pertenencia a la empresa activa y,
/// desde Fase E, el guard de membership revocada (mismo criterio que
/// UpdateCompanyUserBranchesAdminHandlerTests).
/// </summary>
public sealed class UpdateCompanyUserPreferencesAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid CurrentCompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();

    private static CompanyUserMembership Membership(Guid companyId) =>
        CompanyUserMembership.Create(companyId, Guid.NewGuid(), "User", null, CreatedBy);

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public Guid CompanyId => CurrentCompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public UpdateCompanyUserPreferencesAdminHandler BuildHandler() =>
            new(AccessRepo.Object, new CurrentCompanyStub(), Mediator.Object);
    }

    [Fact]
    public async Task Delega_en_el_UseCase_de_Fase_C_y_devuelve_su_resultado()
    {
        var membership = Membership(CurrentCompanyId);
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto?>.Success(
                    new CompanyUserPreferencesDto(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        CurrentCompanyId,
                        membership.Id,
                        null,
                        "AskBranch"
                    )
                )
            );
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    new CompanyUserPreferencesDto(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        CurrentCompanyId,
                        membership.Id,
                        null,
                        "AskBranch"
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserPreferencesAdminCommand(membership.Id, "AskBranch", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_sin_fila_de_preferencias_las_crea_en_vez_de_fallar()
    {
        var membership = Membership(CurrentCompanyId);
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);
        f.Mediator.Setup(m =>
                m.Send(It.IsAny<GetCompanyUserPreferencesQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<CompanyUserPreferencesDto?>.Success(null));
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyUserPreferencesDto>.Success(
                    new CompanyUserPreferencesDto(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        CurrentCompanyId,
                        membership.Id,
                        null,
                        "AskBranch"
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserPreferencesAdminCommand(membership.Id, "AskBranch", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<CreateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_de_otra_empresa_devuelve_NotFound_y_no_delega()
    {
        var membership = Membership(OtherCompanyId);
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserPreferencesAdminCommand(membership.Id, "AskBranch", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Membresia_revocada_devuelve_Forbidden_y_no_delega()
    {
        var membership = Membership(CurrentCompanyId);
        membership.Deactivate(CreatedBy);
        var f = new Fixture();
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipByIdAsync(membership.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new UpdateCompanyUserPreferencesAdminCommand(membership.Id, "AskBranch", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<UpdateCompanyUserPreferencesCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
