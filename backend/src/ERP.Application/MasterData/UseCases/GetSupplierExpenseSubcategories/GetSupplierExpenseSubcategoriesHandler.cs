using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;

public sealed class GetSupplierExpenseSubcategoriesHandler
    : IRequestHandler<GetSupplierExpenseSubcategoriesQuery, Result<SupplierExpenseSubcategoriesDto>>
{
    private readonly ISupplierExpenseSubcategoryRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext _ctx;

    public GetSupplierExpenseSubcategoriesHandler(
        ISupplierExpenseSubcategoryRepository repo,
        IBusinessPartnerRepository bpRepo,
        IOperationalContext ctx
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _ctx = ctx;
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
        return Result<SupplierExpenseSubcategoriesDto>.Success(
            SupplierExpenseSubcategoriesDto.Build(assignments, infos)
        );
    }
}
