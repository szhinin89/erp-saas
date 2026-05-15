using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetKardex;

public sealed class GetKardexQueryHandler
    : IRequestHandler<GetKardexQuery, Result<KardexResponse>>
{
    private readonly IKardexService _kardex;

    public GetKardexQueryHandler(IKardexService kardex)
    {
        _kardex = kardex;
    }

    public Task<Result<KardexResponse>> Handle(GetKardexQuery query, CancellationToken ct)
        => _kardex.GenerarKardexEscalableAsync(query, ct);
}
