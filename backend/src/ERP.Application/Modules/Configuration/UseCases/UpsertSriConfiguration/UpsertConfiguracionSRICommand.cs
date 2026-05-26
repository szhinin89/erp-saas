using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public record UpsertSriConfigurationCommand(
    string  Ruc,
    string  LegalName,
    string? TradeName,
    string  MainAddress,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string  CertP12Path,
    string  CertPassword,
    int     Environment,
    int     EmissionType,
    string  WsdlUrl
) : IRequest<Result<SriConfigurationDto>>;
