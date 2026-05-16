using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class GetExpenseCategorysQueryHandler
    : IRequestHandler<GetExpenseCategorysQuery, Result<IReadOnlyList<ExpenseCategoryDto>>>
{
    private readonly IAccountingSetupRepository _repo;

    public GetExpenseCategorysQueryHandler(IAccountingSetupRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<IReadOnlyList<ExpenseCategoryDto>>> Handle(
        GetExpenseCategorysQuery request,
        CancellationToken ct)
    {
        var items = await _repo.GetExpenseCategoriesAsync(ct);
        var dtos = items.Select(g => new ExpenseCategoryDto(g.Id, g.Category, g.ExpenseAccountId)).ToList();
        return Result<IReadOnlyList<ExpenseCategoryDto>>.Success(dtos);
    }
}
