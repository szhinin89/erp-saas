using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;

public sealed class DisableEmissionPointCommandHandler
    : IRequestHandler<DisableEmissionPointCommand, Result<bool>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _user;

    public DisableEmissionPointCommandHandler(
        IEmissionPointRepository repo,
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
        DisableEmissionPointCommand command,
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
            return Result<bool>.Failure("Punto de emisión no encontrado.");
        try
        {
            entity.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
