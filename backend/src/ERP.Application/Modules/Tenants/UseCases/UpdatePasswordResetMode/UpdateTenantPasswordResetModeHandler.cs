using ERP.Application.Common;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;

public class UpdateTenantPasswordResetModeHandler
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateTenantPasswordResetModeHandler(ITenantRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> HandleAsync(UpdateTenantPasswordResetModeCommand command, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<bool>.Failure("No autenticado.");

        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<bool>.Failure("Empresa no encontrada.");

        tenant.SetPasswordResetMode(command.PasswordResetMode, updatedBy: _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

