using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateSystemUser;

public sealed class CreateSystemUserHandler
    : IRequestHandler<CreateSystemUserCommand, Result<CreateSystemUserResultDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUser _currentUser;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSystemUserHandler(
        IAccessRepository accessRepository,
        IPasswordHasher hasher,
        ICurrentUser currentUser,
        IMediator mediator,
        IUnitOfWork unitOfWork
    )
    {
        _accessRepository = accessRepository;
        _hasher = hasher;
        _currentUser = currentUser;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateSystemUserResultDto>> Handle(
        CreateSystemUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var username = command.Username.Trim().ToLowerInvariant();
        if (await _accessRepository.AnyUserWithUsernameAsync(username, cancellationToken))
            return Result<CreateSystemUserResultDto>.Conflict(
                "Ya existe un usuario con ese username. Asigne la membership sobre el usuario existente."
            );

        var email = string.IsNullOrWhiteSpace(command.Email)
            ? null
            : command.Email.Trim().ToLowerInvariant();
        if (
            email is not null
            && await _accessRepository.AnyUserWithEmailAsync(email, cancellationToken)
        )
            return Result<CreateSystemUserResultDto>.Conflict(
                "Ya existe un usuario con ese email."
            );

        var passwordHash = _hasher.HashPassword(command.Password);
        var user = IdentityUser.Create(
            username,
            command.FirstName,
            command.LastName,
            email,
            passwordHash,
            _currentUser.UserId
        );
        // Estado inicial coherente: el admin define la contraseña, pero el usuario nuevo debe
        // cambiarla en su primer login — no hay infraestructura de invitación por email.
        user.MarkRequirePasswordReset(_currentUser.UserId);

        // Atomicidad: IdentityUser y CompanyUserMembership deben quedar persistidos como una única
        // operación lógica. ErpDbContext.SaveChangesAsync abre/commitea su propia transacción por
        // llamada salvo que detecte una transacción ambiente (Database.CurrentTransaction) — este
        // BeginTransactionAsync la establece antes del primer SaveChanges, así que tanto el alta del
        // usuario como el UseCase de membership delegado (que comparte el mismo ErpDbContext scoped)
        // quedan bajo el mismo commit/rollback, sin reimplementar su lógica.
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accessRepository.AddUserAsync(user, cancellationToken);
            await _accessRepository.SaveChangesAsync(cancellationToken);

            // Nunca se reimplementa la creación de CompanyUserMembership: se reutiliza el UseCase ya
            // probado, igual que UpsertCompanyUserMembershipAdminHandler.
            var membershipResult = await _mediator.Send(
                new UpsertCompanyUserMembershipCommand(
                    command.TenantId,
                    command.CompanyId,
                    username,
                    command.Role,
                    command.ProfileId
                ),
                cancellationToken
            );

            if (!membershipResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return Result<CreateSystemUserResultDto>.Failure(
                    membershipResult.Error!,
                    membershipResult.Code
                );
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            return Result<CreateSystemUserResultDto>.Success(
                new CreateSystemUserResultDto(user.Id, username)
            );
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
