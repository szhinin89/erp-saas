using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using MediatR;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetAccountById;

public class GetAccountByIdHandler : IRequestHandler<GetAccountByIdQuery, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetAccountByIdHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public Task<Result<AccountDto>> HandleAsync(Guid id, CancellationToken ct = default)
        => Handle(new GetAccountByIdQuery(id), ct);

    public async Task<Result<AccountDto>> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var account  = await _repository.GetByIdAsync(request.Id, tenantId, ct);

        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        return Result<AccountDto>.Success(new AccountDto(
            account.Id, account.Code.Value, account.Name,
            account.Type.ToString(), account.Nature.ToString(),
            account.IsActive, account.ParentId, account.CreatedAt));
    }
}
