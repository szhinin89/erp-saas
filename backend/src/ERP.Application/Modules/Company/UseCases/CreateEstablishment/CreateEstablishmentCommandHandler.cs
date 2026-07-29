using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.CreateEstablishment;

public sealed class CreateEstablishmentCommandHandler
    : IRequestHandler<CreateEstablishmentCommand, Result<EstablishmentDto>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly IBranchRepository _branches;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateEstablishmentCommandHandler(
        IEstablishmentRepository repo,
        IBranchRepository branches,
        ICurrentTenant currentTenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _branches = branches;
        _currentTenant = currentTenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<EstablishmentDto>> Handle(
        CreateEstablishmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _company.CompanyId;

        if (command.BranchId.HasValue)
        {
            var branch = await _branches.GetByIdAsync(
                tenantId,
                command.BranchId.Value,
                cancellationToken
            );
            if (branch is null || !branch.IsActive)
                return Result<EstablishmentDto>.Failure("La sucursal no existe o no está activa.");
        }

        var code = command.Code.Trim().PadLeft(3, '0');
        var exists = await _repo.ExistsAsync(tenantId, companyId, code, cancellationToken);
        if (exists)
            return Result<EstablishmentDto>.Failure(
                $"Ya existe un establecimiento con código {code} en esta empresa."
            );

        var entity = Establishment.Create(
            tenantId: tenantId,
            branchId: command.BranchId,
            companyId: companyId,
            code: command.Code,
            name: command.Name,
            address: command.Address,
            phone: command.Phone,
            isMain: command.IsMain,
            createdBy: _user.UserId
        );

        await _repo.AddAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EstablishmentDto>.Success(
            GetEstablishmentsByBranchQueryHandler.ToDto(entity)
        );
    }
}
