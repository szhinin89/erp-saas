using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public record UpsertSriConfigurationCommand(
    string CertP12Path,
    string CertPassword,
    int    Environment,
    int    EmissionType,
    string WsdlUrl
) : IRequest<Result<SriConfigurationDto>>;
