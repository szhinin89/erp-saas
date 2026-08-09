using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.ClassificationCatalogs;

// ══════════════════════════════════════════════════════════════════════════
// QUERIES — 12 catálogos de clasificación de BusinessPartner (CLASS-BP-CATALOGS-01).
// Solo lectura (GET) — CRUD administrativo queda fuera de alcance de este bloque.
// ══════════════════════════════════════════════════════════════════════════

public sealed record GetActiveCustomerCategoriesQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveCustomerSegmentsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveCustomerCreditRatingsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveLoyaltyTiersQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveCustomerInvoiceFormatsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveCustomerClassificationsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveSupplierCategoriesQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveSupplierTypesQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveSupplierRisksQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveSupplierRatingsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActivePrimaryGoodTypesQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

public sealed record GetActiveSupplierSegmentsQuery
    : IRequest<Result<IReadOnlyList<ClassificationCatalogItemDto>>>,
        ICompanyScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// HANDLER BASE — genérico solo por dentro; MediatR necesita tipos concretos registrables,
// así que cada catálogo sigue teniendo su propio Query + Handler con nombre propio.
// ══════════════════════════════════════════════════════════════════════════

public abstract class GetActiveClassificationCatalogQueryHandlerBase<TEntity>
    where TEntity : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity, IClassificationCatalogEntity
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    protected GetActiveClassificationCatalogQueryHandlerBase(
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _tenant = tenant;
        _company = company;
    }

    protected async Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> HandleAsync(
        Func<Guid, Guid, CancellationToken, Task<IReadOnlyList<TEntity>>> getActive,
        CancellationToken ct
    )
    {
        var items = await getActive(_tenant.TenantId, _company.CompanyId, ct);
        IReadOnlyList<ClassificationCatalogItemDto> dtos = items
            .Select(e => new ClassificationCatalogItemDto(e.Id, e.Code, e.Name, e.SortOrder))
            .ToList();
        return Result<IReadOnlyList<ClassificationCatalogItemDto>>.Success(dtos);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// HANDLERS
// ══════════════════════════════════════════════════════════════════════════

public sealed class GetActiveCustomerCategoriesQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<CustomerCategory>,
        IRequestHandler<GetActiveCustomerCategoriesQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ICustomerCategoryRepository _repo;

    public GetActiveCustomerCategoriesQueryHandler(
        ICustomerCategoryRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveCustomerCategoriesQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveCustomerSegmentsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<CustomerSegment>,
        IRequestHandler<GetActiveCustomerSegmentsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ICustomerSegmentRepository _repo;

    public GetActiveCustomerSegmentsQueryHandler(
        ICustomerSegmentRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveCustomerSegmentsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveCustomerCreditRatingsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<CustomerCreditRating>,
        IRequestHandler<GetActiveCustomerCreditRatingsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ICustomerCreditRatingRepository _repo;

    public GetActiveCustomerCreditRatingsQueryHandler(
        ICustomerCreditRatingRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveCustomerCreditRatingsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveLoyaltyTiersQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<LoyaltyTier>,
        IRequestHandler<GetActiveLoyaltyTiersQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ILoyaltyTierRepository _repo;

    public GetActiveLoyaltyTiersQueryHandler(
        ILoyaltyTierRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveLoyaltyTiersQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveCustomerInvoiceFormatsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<CustomerInvoiceFormat>,
        IRequestHandler<GetActiveCustomerInvoiceFormatsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ICustomerInvoiceFormatRepository _repo;

    public GetActiveCustomerInvoiceFormatsQueryHandler(
        ICustomerInvoiceFormatRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveCustomerInvoiceFormatsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveCustomerClassificationsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<CustomerClassification>,
        IRequestHandler<GetActiveCustomerClassificationsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ICustomerClassificationRepository _repo;

    public GetActiveCustomerClassificationsQueryHandler(
        ICustomerClassificationRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveCustomerClassificationsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveSupplierCategoriesQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<SupplierCategory>,
        IRequestHandler<GetActiveSupplierCategoriesQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ISupplierCategoryRepository _repo;

    public GetActiveSupplierCategoriesQueryHandler(
        ISupplierCategoryRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveSupplierCategoriesQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveSupplierTypesQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<SupplierType>,
        IRequestHandler<GetActiveSupplierTypesQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ISupplierTypeRepository _repo;

    public GetActiveSupplierTypesQueryHandler(
        ISupplierTypeRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveSupplierTypesQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveSupplierRisksQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<SupplierRisk>,
        IRequestHandler<GetActiveSupplierRisksQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ISupplierRiskRepository _repo;

    public GetActiveSupplierRisksQueryHandler(
        ISupplierRiskRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveSupplierRisksQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveSupplierRatingsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<SupplierRating>,
        IRequestHandler<GetActiveSupplierRatingsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ISupplierRatingRepository _repo;

    public GetActiveSupplierRatingsQueryHandler(
        ISupplierRatingRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveSupplierRatingsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActivePrimaryGoodTypesQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<PrimaryGoodType>,
        IRequestHandler<GetActivePrimaryGoodTypesQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly IPrimaryGoodTypeRepository _repo;

    public GetActivePrimaryGoodTypesQueryHandler(
        IPrimaryGoodTypeRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActivePrimaryGoodTypesQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}

public sealed class GetActiveSupplierSegmentsQueryHandler
    : GetActiveClassificationCatalogQueryHandlerBase<SupplierSegment>,
        IRequestHandler<GetActiveSupplierSegmentsQuery, Result<IReadOnlyList<ClassificationCatalogItemDto>>>
{
    private readonly ISupplierSegmentRepository _repo;

    public GetActiveSupplierSegmentsQueryHandler(
        ISupplierSegmentRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
        : base(tenant, company) => _repo = repo;

    public Task<Result<IReadOnlyList<ClassificationCatalogItemDto>>> Handle(
        GetActiveSupplierSegmentsQuery request,
        CancellationToken ct
    ) => HandleAsync(_repo.GetActiveAsync, ct);
}
