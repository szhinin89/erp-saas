using ERP.Application.Modules.SriCatalogs.DTOs;
using ERP.Application.Modules.SriCatalogs.Queries;
using ERP.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Modules.SriCatalogs;

// ── All SRI catalog handlers live here ───────────────────────────────────────
// These are read-only queries against global (non-tenant) lookup tables.
// AsNoTracking + direct DbContext projection — no repository needed for catalogs.

public sealed class GetSriPaymentMethodsHandler
    : IRequestHandler<GetSriPaymentMethodsQuery, IReadOnlyList<SriPaymentMethodDto>>
{
    private readonly ErpDbContext _db;
    public GetSriPaymentMethodsHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriPaymentMethodDto>> Handle(
        GetSriPaymentMethodsQuery _, CancellationToken ct)
        => await _db.SriPaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SriPaymentMethodDto(x.Code, x.Name, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriDocTypesHandler
    : IRequestHandler<GetSriDocTypesQuery, IReadOnlyList<SriDocTypeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriDocTypesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriDocTypeDto>> Handle(
        GetSriDocTypesQuery _, CancellationToken ct)
        => await _db.SriDocTypes.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new SriDocTypeDto(x.Code, x.Name, x.ShortName, x.IsElectronic, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriIdTypesHandler
    : IRequestHandler<GetSriIdTypesQuery, IReadOnlyList<SriIdTypeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriIdTypesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriIdTypeDto>> Handle(
        GetSriIdTypesQuery _, CancellationToken ct)
        => await _db.SriIdTypes.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new SriIdTypeDto(x.Code, x.Name, x.Digits))
            .ToListAsync(ct);
}

public sealed class GetSriVatRatesHandler
    : IRequestHandler<GetSriVatRatesQuery, IReadOnlyList<SriVatRateDto>>
{
    private readonly ErpDbContext _db;
    public GetSriVatRatesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriVatRateDto>> Handle(
        GetSriVatRatesQuery _, CancellationToken ct)
        => await _db.SriVatRates.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new SriVatRateDto(x.Code, x.Name, x.Percentage, x.IsActive, x.ValidFrom, x.ValidUntil))
            .ToListAsync(ct);
}

public sealed class GetSriIceRatesHandler
    : IRequestHandler<GetSriIceRatesQuery, IReadOnlyList<SriIceRateDto>>
{
    private readonly ErpDbContext _db;
    public GetSriIceRatesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriIceRateDto>> Handle(
        GetSriIceRatesQuery _, CancellationToken ct)
        => await _db.SriIceRates.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SriIceRateDto(x.Code, x.Name, x.Percentage, x.UnitValue, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriRetentionCodesHandler
    : IRequestHandler<GetSriRetentionCodesQuery, IReadOnlyList<SriRetentionCodeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriRetentionCodesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriRetentionCodeDto>> Handle(
        GetSriRetentionCodesQuery request, CancellationToken ct)
    {
        var q = _db.SriRetentionCodes.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.TaxType))
            q = q.Where(x => x.TaxType == request.TaxType.ToUpperInvariant());
        return await q.OrderBy(x => x.TaxType).ThenBy(x => x.Code)
            .Select(x => new SriRetentionCodeDto(x.TaxType, x.Code, x.Name, x.Percentage, x.AppliesTo, x.IsActive))
            .ToListAsync(ct);
    }
}

public sealed class GetSriCountriesHandler
    : IRequestHandler<GetSriCountriesQuery, IReadOnlyList<SriCountryDto>>
{
    private readonly ErpDbContext _db;
    public GetSriCountriesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriCountryDto>> Handle(
        GetSriCountriesQuery _, CancellationToken ct)
        => await _db.SriCountries.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SriCountryDto(x.Code, x.Iso2, x.Name, x.PhoneCode, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriTaxRegimesHandler
    : IRequestHandler<GetSriTaxRegimesQuery, IReadOnlyList<SriTaxRegimeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriTaxRegimesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriTaxRegimeDto>> Handle(
        GetSriTaxRegimesQuery _, CancellationToken ct)
        => await _db.SriTaxRegimes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SriTaxRegimeDto(x.Code, x.Name, x.Abbrev, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriTaxSupportsHandler
    : IRequestHandler<GetSriTaxSupportsQuery, IReadOnlyList<SriTaxSupportDto>>
{
    private readonly ErpDbContext _db;
    public GetSriTaxSupportsHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriTaxSupportDto>> Handle(
        GetSriTaxSupportsQuery _, CancellationToken ct)
        => await _db.SriTaxSupports.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SriTaxSupportDto(x.Code, x.Name, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriUomHandler
    : IRequestHandler<GetSriUomQuery, IReadOnlyList<SriUomDto>>
{
    private readonly ErpDbContext _db;
    public GetSriUomHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriUomDto>> Handle(
        GetSriUomQuery _, CancellationToken ct)
        => await _db.SriUoms.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new SriUomDto(x.Code, x.Name, x.Abbrev, x.IsActive))
            .ToListAsync(ct);
}

public sealed class GetSriEnvironmentsHandler
    : IRequestHandler<GetSriEnvironmentsQuery, IReadOnlyList<SriEnvironmentDto>>
{
    private readonly ErpDbContext _db;
    public GetSriEnvironmentsHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriEnvironmentDto>> Handle(
        GetSriEnvironmentsQuery _, CancellationToken ct)
        => await _db.SriEnvironments.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new SriEnvironmentDto(x.Code, x.Name, x.Abbrev))
            .ToListAsync(ct);
}

public sealed class GetSriEmissionTypesHandler
    : IRequestHandler<GetSriEmissionTypesQuery, IReadOnlyList<SriEmissionTypeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriEmissionTypesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriEmissionTypeDto>> Handle(
        GetSriEmissionTypesQuery _, CancellationToken ct)
        => await _db.SriEmissionTypes.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new SriEmissionTypeDto(x.Code, x.Name))
            .ToListAsync(ct);
}

public sealed class GetSriErrorCodesHandler
    : IRequestHandler<GetSriErrorCodesQuery, IReadOnlyList<SriErrorCodeDto>>
{
    private readonly ErpDbContext _db;
    public GetSriErrorCodesHandler(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriErrorCodeDto>> Handle(
        GetSriErrorCodesQuery request, CancellationToken ct)
    {
        var q = _db.SriErrorCodes.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.ErrorType))
            q = q.Where(x => x.ErrorType == request.ErrorType.ToUpperInvariant());
        return await q.OrderBy(x => x.Code)
            .Select(x => new SriErrorCodeDto(x.Code, x.ErrorType, x.Name, x.Description, x.IsActive))
            .ToListAsync(ct);
    }
}
