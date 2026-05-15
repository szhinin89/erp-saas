using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public sealed record UpsertConfiguracionSRICommand(
    string  Ruc,
    string  LegalName,
    string? TradeName,
    string  MainAddress,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string  EstabCode,
    string  EmPointCode,
    string  CertP12Path,
    string  CertPassword,
    int     Environment,
    int     EmissionType,
    string  WsdlUrl
) : IRequest<Result<ConfiguracionSRIDto>>;
