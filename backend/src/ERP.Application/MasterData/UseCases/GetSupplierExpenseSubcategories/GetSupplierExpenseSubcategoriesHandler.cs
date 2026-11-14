using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;

public sealed class GetSupplierExpenseSubcategoriesHandler
    : IRequestHandler<GetSupplierExpenseSubcategoriesQuery, Result<SupplierExpenseSubcategoriesDto>>
{
    private readonly ISupplierExpenseSubcategoryRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext _ctx;
    private readonly IRuntimePermissionAuthorizer _authorizer;

    public GetSupplierExpenseSubcategoriesHandler(
        ISupplierExpenseSubcategoryRepository repo,
        IBusinessPartnerRepository bpRepo,
        IOperationalContext ctx,
        IRuntimePermissionAuthorizer authorizer
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _ctx = ctx;
        _authorizer = authorizer;
    }

    public async Task<Result<SupplierExpenseSubcategoriesDto>> Handle(
        GetSupplierExpenseSubcategoriesQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasCompany)
            return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                "Contexto de empresa no establecido."
            );

        var bp = await _bpRepo.GetByIdAsync(query.BusinessPartnerId, cancellationToken);
        if (bp is null)
            return Result<SupplierExpenseSubcategoriesDto>.NotFound("Proveedor no encontrado.");

        var assignments = await _repo.GetByBusinessPartnerAsync(
            query.BusinessPartnerId,
            cancellationToken
        );
        var infos = await _repo.GetSubcategoryInfosAsync(
            assignments.Select(x => x.ExpenseSubcategoryId).ToList(),
            cancellationToken
        );
        var dto = SupplierExpenseSubcategoriesDto.Build(assignments, infos);

        // El catálogo candidato solo se entrega a quien puede configurar (permiso oficial existente);
        // quien solo consulta no lo recibe, así que la sección no depende de expenses.catalog.view.
        var canConfigure = await _authorizer.IsAuthorizedAsync(
            MasterDataPermissions.BusinessPartnersConfigureCompany,
            _ctx.UserId,
            _ctx.Role,
            cancellationToken
        );
        if (canConfigure)
            dto = dto.WithCatalog(await _repo.GetActiveSubcategoryCatalogAsync(cancellationToken));

        return Result<SupplierExpenseSubcategoriesDto>.Success(dto);
    }
}
