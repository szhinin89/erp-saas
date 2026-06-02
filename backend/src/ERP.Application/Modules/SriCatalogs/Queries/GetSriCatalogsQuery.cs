using ERP.Application.Modules.SriCatalogs.DTOs;
using MediatR;

namespace ERP.Application.Modules.SriCatalogs.Queries;

// One query per catalog — each returns IReadOnlyList for caching simplicity.
// All queries are anonymous (no tenant scope — global regulatory data).

public sealed record GetSriPaymentMethodsQuery  : IRequest<IReadOnlyList<SriPaymentMethodDto>>;
public sealed record GetSriDocTypesQuery        : IRequest<IReadOnlyList<SriDocTypeDto>>;
public sealed record GetSriIdTypesQuery         : IRequest<IReadOnlyList<SriIdTypeDto>>;
public sealed record GetSriVatRatesQuery        : IRequest<IReadOnlyList<SriVatRateDto>>;
public sealed record GetSriIceRatesQuery        : IRequest<IReadOnlyList<SriIceRateDto>>;
public sealed record GetSriRetentionCodesQuery(string? TaxType = null) : IRequest<IReadOnlyList<SriRetentionCodeDto>>;
public sealed record GetSriCountriesQuery       : IRequest<IReadOnlyList<SriCountryDto>>;
public sealed record GetSriTaxRegimesQuery      : IRequest<IReadOnlyList<SriTaxRegimeDto>>;
public sealed record GetSriTaxSupportsQuery     : IRequest<IReadOnlyList<SriTaxSupportDto>>;
public sealed record GetSriUomQuery             : IRequest<IReadOnlyList<SriUomDto>>;
public sealed record GetSriEnvironmentsQuery    : IRequest<IReadOnlyList<SriEnvironmentDto>>;
public sealed record GetSriEmissionTypesQuery   : IRequest<IReadOnlyList<SriEmissionTypeDto>>;
public sealed record GetSriErrorCodesQuery(string? ErrorType = null) : IRequest<IReadOnlyList<SriErrorCodeDto>>;
