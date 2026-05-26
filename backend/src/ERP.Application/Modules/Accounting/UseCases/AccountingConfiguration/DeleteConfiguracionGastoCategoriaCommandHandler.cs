using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

public sealed class DeleteExpenseCategoryCommandHandler
    : IRequestHandler<DeleteExpenseCategoryCommand, Result<Unit>>
{
    private readonly IAccountingSetupRepository _repo;

    public DeleteExpenseCategoryCommandHandler(IAccountingSetupRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<Unit>> Handle(DeleteExpenseCategoryCommand request, CancellationToken ct)
    {
        var row = await _repo.GetExpenseCategoryByIdAsync(request.Id, ct);
        if (row is null)
            return Result<Unit>.Failure("El mapeo no existe.");

        _repo.RemoveExpenseCategory(row);
        await _repo.SaveChangesAsync(ct);
        return Result<Unit>.Success(default);
    }
}
