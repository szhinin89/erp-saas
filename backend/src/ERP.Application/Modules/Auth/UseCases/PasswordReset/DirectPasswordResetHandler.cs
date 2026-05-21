using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class DirectPasswordResetHandler : IRequestHandler<DirectPasswordResetCommand, Result<bool>>
{
    private readonly ISubscriberRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public DirectPasswordResetHandler(
        ISubscriberRepository tenantRepository,
        IAccessRepository accessRepository,
        ICompanyProvisioningService companyProvisioning,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _companyProvisioning = companyProvisioning;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<bool>> Handle(DirectPasswordResetCommand command, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.SubscriberId, ct);

        if (tenant is null || !tenant.IsActive)
            return Result<bool>.Failure("Empresa no encontrada o inactiva.");

        if (tenant.PasswordResetMode != PasswordResetMode.Direct)
            return Result<bool>.Failure("La recuperación directa de contraseña no está habilitada para esta empresa.");

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(normalizedEmail, ct);
        if (user is null || user.IsPlatformSuperAdmin)
            return Result<bool>.Failure("Usuario no encontrado.");

        var defaultCompany = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        var membership = await _accessRepository.GetCompanyUserMembershipAsync(defaultCompany.Id, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<bool>.Failure("Usuario no encontrado.");

        var newHash = _passwordHasher.HashPassword(command.NewPassword);
        user.SetPasswordHash(newHash, updatedBy: user.Id);
        await _accessRepository.SaveChangesAsync(ct);

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, command.SubscriberId, "Cambio de contraseña", ct);

        return Result<bool>.Success(true);
    }
}
