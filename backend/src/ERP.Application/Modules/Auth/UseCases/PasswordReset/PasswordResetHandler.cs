using ERP.Application.Common;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public class PasswordResetHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;

    public PasswordResetHandler(ITenantRepository tenantRepository, IUserRepository userRepository)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<bool>> HandleAsync(PasswordResetCommand command, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, ct);

        if (tenant is null || !tenant.IsActive)
            return Result<bool>.Failure("Empresa no encontrada o inactiva.");

        if (tenant.PasswordResetMode != PasswordResetMode.Direct)
            return Result<bool>.Failure("La recuperación directa de contraseña no está habilitada para esta empresa.");

        var normalizedEmail = command.Email.Trim();

        var user = await _userRepository.GetByEmailSystemAsync(normalizedEmail, command.TenantId, ct);

        if (user is null)
            return Result<bool>.Failure("Usuario no encontrado.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
        user.SetPasswordHash(newHash, updatedBy: user.Id);

        await _userRepository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

