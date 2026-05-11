using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.DisableAccount;

public class DisableAccountHandler
{
    private readonly IAccountingRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableAccountHandler(
        IAccountingRepository repository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var account = await _repository.GetByIdAsync(id, tenantId, ct);
        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        if (!account.IsActive)
            return Result<AccountDto>.Failure("La cuenta ya está deshabilitada.");

        account.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "accounting",
            action: "account.disable",
            entityType: "Account",
            entityId: account.Id,
            description: $"{account.Code.Value} — {account.Name}"), ct);

        await _repository.SaveChangesAsync(ct);

        return Result<AccountDto>.Success(new AccountDto(
            account.Id,
            account.Code.Value,
            account.Name,
            account.Type.ToString(),
            account.Nature.ToString(),
            account.IsActive,
            account.ParentId,
            account.CreatedAt));
    }
}
