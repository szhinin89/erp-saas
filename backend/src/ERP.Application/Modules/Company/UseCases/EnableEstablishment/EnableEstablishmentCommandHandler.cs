using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.EnableEstablishment;

public sealed class EnableEstablishmentCommandHandler
    : IRequestHandler<EnableEstablishmentCommand, Result<bool>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _user;

    public EnableEstablishmentCommandHandler(
        IEstablishmentRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _currentCompany = company;
        _user = user;
    }

    public async Task<Result<bool>> Handle(
        EnableEstablishmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var entity = await _repo.GetByIdForCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            command.Id,
            cancellationToken
        );
        if (entity is null)
            return Result<bool>.Failure("Establecimiento no encontrado.");
        entity.Enable(_user.UserId);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
