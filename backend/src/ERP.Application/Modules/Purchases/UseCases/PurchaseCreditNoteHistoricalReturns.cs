using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

public sealed record GetCreditNoteHistoricalReturnsQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<IReadOnlyList<PurchaseReturnDto>>>, IBranchScopedRequest;

public sealed class GetCreditNoteHistoricalReturnsValidator : AbstractValidator<GetCreditNoteHistoricalReturnsQuery>
{
    public GetCreditNoteHistoricalReturnsValidator() => RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
}

public sealed class GetCreditNoteHistoricalReturnsHandler(
    IPurchaseReturnRepository returns, ICurrentTenant tenant, ICurrentBranch branch)
    : IRequestHandler<GetCreditNoteHistoricalReturnsQuery, Result<IReadOnlyList<PurchaseReturnDto>>>
{
    public async Task<Result<IReadOnlyList<PurchaseReturnDto>>> Handle(
        GetCreditNoteHistoricalReturnsQuery query, CancellationToken ct)
    {
        var candidates = await returns.GetPendingCreditNoteByInvoiceAsync(
            tenant.TenantId, branch.BranchId, query.PurchaseInvoiceId, ct);
        return Result<IReadOnlyList<PurchaseReturnDto>>.Success(candidates.Select(Map.ToDto).ToList());
    }
}
