using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductReport;

public class GetProductReportHandler : IRequestHandler<GetProductReportQuery, Result<PagedResult<ProductReportItemDto>>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductReportHandler(IProductRepository repository, ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<PagedResult<ProductReportItemDto>>> HandleAsync(
        ProductReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default) =>
        Handle(new GetProductReportQuery(filter, pageNumber, pageSize), ct);

    public async Task<Result<PagedResult<ProductReportItemDto>>> Handle(
        GetProductReportQuery request,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var (products, totalCount) = await _repository.GetReportPageAsync(
            subscriberId,
            request.Filter,
            request.PageNumber,
            request.PageSize,
            ct);

        var dtos = products.Select(p => new ProductReportItemDto(
            p.Id,
            p.SaleCode,
            p.PurchaseCode,
            p.ShortName,
            p.Description,
            p.IsFavorite,
            p.IsForSale,
            p.IsActive,
            p.IsEcommerceActive,
            p.IsService,
            p.LineId,
            p.CategoryId,
            p.SubcategoryId,
            p.BrandId,
            p.ProductTypeId,
            p.CreatedAt)).ToList();

        return Result<PagedResult<ProductReportItemDto>>.Success(new PagedResult<ProductReportItemDto>(
            Items: dtos,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            TotalCount: totalCount));
    }
}

