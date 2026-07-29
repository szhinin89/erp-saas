using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.DecimalConfig;

public sealed record DecimalConfigDto(
    int SalesUnitPrice,
    int PurchaseUnitPrice,
    int Quantity,
    int Percentage,
    int TotalAmount
);

public sealed record GetDecimalConfigQuery
    : IRequest<Result<DecimalConfigDto>>,
        ICompanyScopedRequest;

public sealed record UpdateDecimalConfigCommand(
    int SalesUnitPrice,
    int PurchaseUnitPrice,
    int Quantity,
    int Percentage,
    int TotalAmount
) : IRequest<Result<DecimalConfigDto>>, ICompanyScopedRequest;

public sealed class GetDecimalConfigHandler
    : IRequestHandler<GetDecimalConfigQuery, Result<DecimalConfigDto>>
{
    private readonly IDecimalConfigRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetDecimalConfigHandler(
        IDecimalConfigRepository repo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<DecimalConfigDto>> Handle(
        GetDecimalConfigQuery q,
        CancellationToken ct
    )
    {
        var cfg = await _repo.GetAsync(_t.TenantId, _c.CompanyId, ct);
        return Result<DecimalConfigDto>.Success(cfg);
    }
}

public sealed class UpdateDecimalConfigHandler
    : IRequestHandler<UpdateDecimalConfigCommand, Result<DecimalConfigDto>>
{
    private readonly IDecimalConfigRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public UpdateDecimalConfigHandler(
        IDecimalConfigRepository repo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<DecimalConfigDto>> Handle(
        UpdateDecimalConfigCommand cmd,
        CancellationToken ct
    )
    {
        await _repo.SaveAsync(
            _t.TenantId,
            _c.CompanyId,
            cmd.SalesUnitPrice,
            cmd.PurchaseUnitPrice,
            cmd.Quantity,
            cmd.Percentage,
            cmd.TotalAmount,
            ct
        );
        return Result<DecimalConfigDto>.Success(
            new DecimalConfigDto(
                cmd.SalesUnitPrice,
                cmd.PurchaseUnitPrice,
                cmd.Quantity,
                cmd.Percentage,
                cmd.TotalAmount
            )
        );
    }
}

public interface IDecimalConfigRepository
{
    Task<DecimalConfigDto> GetAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
    Task SaveAsync(
        Guid tenantId,
        Guid companyId,
        int salesUnitPrice,
        int purchaseUnitPrice,
        int quantity,
        int percentage,
        int totalAmount,
        CancellationToken ct = default
    );
}
