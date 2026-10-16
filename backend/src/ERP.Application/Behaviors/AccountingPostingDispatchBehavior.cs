using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>Despacha únicamente obligaciones nuevas tras finalizar la transacción del request.</summary>
public sealed class AccountingPostingDispatchBehavior<TRequest, TResponse>(
    IAccountingPostingDispatcher dispatcher, IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next(ct);
        // Los comandos internos de una anulaci?n no pueden consumir la cola antes del commit.
        if (!unitOfWork.HasActiveTransaction) await dispatcher.DispatchPendingAsync(ct);
        return response;
    }
}
