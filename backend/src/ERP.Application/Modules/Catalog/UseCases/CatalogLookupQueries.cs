using ERP.Application.Common;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Catalog.UseCases;

// ══════════════════════════════════════════════════════════════════════════
// DTOs — mismo shape que los objetos anónimos que antes devolvía CatalogController
// ══════════════════════════════════════════════════════════════════════════

public sealed record SriUomDto(string Code, string Name, string? Abbrev);

public sealed record SriVatRateDto(string Code, string Name, decimal Percentage);

public sealed record SriIceRateDto(string Code, string Name, decimal? Percentage);

public sealed record SriRetentionCodeDto(Guid Id, string TaxType, string Code, string Name, decimal Percentage, string AppliesTo);

public sealed record SriTaxSupportDto(string Code, string Name);

public sealed record SriDocTypeDto(string Code, string Name, string ShortName, bool IsElectronic);

public sealed record SriPaymentMethodDto(string Code, string Name);

public sealed record SriSupplierTypeDto(string Code, string Name);

public sealed record SriTaxRegimeDto(string Code, string Name, string? Abbrev);

public sealed record CatalogPersonTypeDto(short Code, string Name);

public sealed record CatalogBarcodeTypeDto(string Code, string Name);

public sealed record CatalogItemMarginStatusDto(string Code, string Label, string ColorToken);

public sealed record SriIdTypeDto(string Code, string Name, short? Digits);

// ══════════════════════════════════════════════════════════════════════════
// Queries — catálogos globales de solo lectura, sin scope de tenant/empresa
// ══════════════════════════════════════════════════════════════════════════

public sealed record GetSriUomsQuery : IRequest<Result<IReadOnlyList<SriUomDto>>>, IPlatformScopedRequest;

public sealed record GetSriVatRatesQuery : IRequest<Result<IReadOnlyList<SriVatRateDto>>>, IPlatformScopedRequest;

public sealed record GetSriIceRatesQuery : IRequest<Result<IReadOnlyList<SriIceRateDto>>>, IPlatformScopedRequest;

public sealed record GetSriRetentionCodesQuery(string? TaxType = null)
    : IRequest<Result<IReadOnlyList<SriRetentionCodeDto>>>, IPlatformScopedRequest;

public sealed record GetSriTaxSupportCodesQuery : IRequest<Result<IReadOnlyList<SriTaxSupportDto>>>, IPlatformScopedRequest;

public sealed record GetSriDocTypesQuery : IRequest<Result<IReadOnlyList<SriDocTypeDto>>>, IPlatformScopedRequest;

public sealed record GetSriPaymentMethodsQuery : IRequest<Result<IReadOnlyList<SriPaymentMethodDto>>>, IPlatformScopedRequest;

public sealed record GetSriSupplierTypesQuery : IRequest<Result<IReadOnlyList<SriSupplierTypeDto>>>, IPlatformScopedRequest;

public sealed record GetSriTaxRegimesQuery : IRequest<Result<IReadOnlyList<SriTaxRegimeDto>>>, IPlatformScopedRequest;

public sealed record GetCatalogPersonTypesQuery : IRequest<Result<IReadOnlyList<CatalogPersonTypeDto>>>, IPlatformScopedRequest;

public sealed record GetCatalogBarcodeTypesQuery : IRequest<Result<IReadOnlyList<CatalogBarcodeTypeDto>>>, IPlatformScopedRequest;

public sealed record GetCatalogItemMarginStatusesQuery : IRequest<Result<IReadOnlyList<CatalogItemMarginStatusDto>>>, IPlatformScopedRequest;

public sealed record GetSriIdTypesQuery : IRequest<Result<IReadOnlyList<SriIdTypeDto>>>, IPlatformScopedRequest;

public sealed record GetSriIdTypesByUsageQuery(IdentificationUsageType Usage)
    : IRequest<Result<IReadOnlyList<SriIdTypeDto>>>, IPlatformScopedRequest;

// ══════════════════════════════════════════════════════════════════════════
// Handlers
// ══════════════════════════════════════════════════════════════════════════

public sealed class GetSriUomsQueryHandler : IRequestHandler<GetSriUomsQuery, Result<IReadOnlyList<SriUomDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriUomsQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriUomDto>>> Handle(GetSriUomsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveUomsAsync(cancellationToken);
        return Result<IReadOnlyList<SriUomDto>>.Success(
            items.Select(u => new SriUomDto(u.Code, u.Name, u.Abbrev)).ToList());
    }
}

public sealed class GetSriVatRatesQueryHandler : IRequestHandler<GetSriVatRatesQuery, Result<IReadOnlyList<SriVatRateDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriVatRatesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriVatRateDto>>> Handle(GetSriVatRatesQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await _repo.GetActiveVatRatesAsync(today, cancellationToken);
        return Result<IReadOnlyList<SriVatRateDto>>.Success(
            items.Select(r => new SriVatRateDto(r.Code, r.Name, r.Percentage)).ToList());
    }
}

public sealed class GetSriIceRatesQueryHandler : IRequestHandler<GetSriIceRatesQuery, Result<IReadOnlyList<SriIceRateDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriIceRatesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriIceRateDto>>> Handle(GetSriIceRatesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveIceRatesAsync(cancellationToken);
        return Result<IReadOnlyList<SriIceRateDto>>.Success(
            items.Select(r => new SriIceRateDto(r.Code, r.Name, r.Percentage)).ToList());
    }
}

public sealed class GetSriRetentionCodesQueryHandler : IRequestHandler<GetSriRetentionCodesQuery, Result<IReadOnlyList<SriRetentionCodeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriRetentionCodesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriRetentionCodeDto>>> Handle(GetSriRetentionCodesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveRetentionCodesAsync(request.TaxType, cancellationToken);
        return Result<IReadOnlyList<SriRetentionCodeDto>>.Success(
            items.Select(r => new SriRetentionCodeDto(r.Id, r.TaxType, r.Code, r.Name, r.Percentage, r.AppliesTo)).ToList());
    }
}

public sealed class GetSriTaxSupportCodesQueryHandler : IRequestHandler<GetSriTaxSupportCodesQuery, Result<IReadOnlyList<SriTaxSupportDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriTaxSupportCodesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriTaxSupportDto>>> Handle(GetSriTaxSupportCodesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveTaxSupportCodesAsync(cancellationToken);
        return Result<IReadOnlyList<SriTaxSupportDto>>.Success(
            items.Select(t => new SriTaxSupportDto(t.Code, t.Name)).ToList());
    }
}

public sealed class GetSriDocTypesQueryHandler : IRequestHandler<GetSriDocTypesQuery, Result<IReadOnlyList<SriDocTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriDocTypesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriDocTypeDto>>> Handle(GetSriDocTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveDocTypesAsync(cancellationToken);
        return Result<IReadOnlyList<SriDocTypeDto>>.Success(
            items.Select(d => new SriDocTypeDto(d.Code, d.Name, d.ShortName, d.IsElectronic)).ToList());
    }
}

public sealed class GetSriPaymentMethodsQueryHandler : IRequestHandler<GetSriPaymentMethodsQuery, Result<IReadOnlyList<SriPaymentMethodDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriPaymentMethodsQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriPaymentMethodDto>>> Handle(GetSriPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActivePaymentMethodsAsync(cancellationToken);
        return Result<IReadOnlyList<SriPaymentMethodDto>>.Success(
            items.Select(p => new SriPaymentMethodDto(p.Code, p.Name)).ToList());
    }
}

public sealed class GetSriSupplierTypesQueryHandler : IRequestHandler<GetSriSupplierTypesQuery, Result<IReadOnlyList<SriSupplierTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriSupplierTypesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriSupplierTypeDto>>> Handle(GetSriSupplierTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveSupplierTypesAsync(cancellationToken);
        return Result<IReadOnlyList<SriSupplierTypeDto>>.Success(
            items.Select(r => new SriSupplierTypeDto(r.Code, r.Name)).ToList());
    }
}

public sealed class GetSriTaxRegimesQueryHandler : IRequestHandler<GetSriTaxRegimesQuery, Result<IReadOnlyList<SriTaxRegimeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriTaxRegimesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriTaxRegimeDto>>> Handle(GetSriTaxRegimesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveTaxRegimesAsync(cancellationToken);
        return Result<IReadOnlyList<SriTaxRegimeDto>>.Success(
            items.Select(r => new SriTaxRegimeDto(r.Code, r.Name, r.Abbrev)).ToList());
    }
}

public sealed class GetCatalogPersonTypesQueryHandler : IRequestHandler<GetCatalogPersonTypesQuery, Result<IReadOnlyList<CatalogPersonTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetCatalogPersonTypesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<CatalogPersonTypeDto>>> Handle(GetCatalogPersonTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetPersonTypesAsync(cancellationToken);
        return Result<IReadOnlyList<CatalogPersonTypeDto>>.Success(
            items.Select(p => new CatalogPersonTypeDto(p.Code, p.Name)).ToList());
    }
}

public sealed class GetCatalogBarcodeTypesQueryHandler : IRequestHandler<GetCatalogBarcodeTypesQuery, Result<IReadOnlyList<CatalogBarcodeTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetCatalogBarcodeTypesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<CatalogBarcodeTypeDto>>> Handle(GetCatalogBarcodeTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveBarcodeTypesAsync(cancellationToken);
        return Result<IReadOnlyList<CatalogBarcodeTypeDto>>.Success(
            items.Select(b => new CatalogBarcodeTypeDto(b.Code, b.Name)).ToList());
    }
}

public sealed class GetCatalogItemMarginStatusesQueryHandler : IRequestHandler<GetCatalogItemMarginStatusesQuery, Result<IReadOnlyList<CatalogItemMarginStatusDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetCatalogItemMarginStatusesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<CatalogItemMarginStatusDto>>> Handle(GetCatalogItemMarginStatusesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetItemMarginStatusesAsync(cancellationToken);
        return Result<IReadOnlyList<CatalogItemMarginStatusDto>>.Success(
            items.Select(m => new CatalogItemMarginStatusDto(m.Code, m.Label, m.ColorToken)).ToList());
    }
}

public sealed class GetSriIdTypesQueryHandler : IRequestHandler<GetSriIdTypesQuery, Result<IReadOnlyList<SriIdTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriIdTypesQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriIdTypeDto>>> Handle(GetSriIdTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetSriIdTypesAsync(cancellationToken);
        return Result<IReadOnlyList<SriIdTypeDto>>.Success(
            items.Select(t => new SriIdTypeDto(t.Code, t.Name, t.Digits)).ToList());
    }
}

public sealed class GetSriIdTypesByUsageQueryHandler : IRequestHandler<GetSriIdTypesByUsageQuery, Result<IReadOnlyList<SriIdTypeDto>>>
{
    private readonly ISriCatalogLookupRepository _repo;
    public GetSriIdTypesByUsageQueryHandler(ISriCatalogLookupRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<SriIdTypeDto>>> Handle(GetSriIdTypesByUsageQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetSriIdTypesByUsageAsync(request.Usage, cancellationToken);
        return Result<IReadOnlyList<SriIdTypeDto>>.Success(
            items.Select(t => new SriIdTypeDto(t.Code, t.Name, t.Digits)).ToList());
    }
}
