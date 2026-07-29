using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;

public record UpsertSriConfigurationCommand(
    string? CertPassword,
    int Environment,
    int EmissionType,
    string WsdlUrl
) : IRequest<Result<SriConfigurationDto>>;
