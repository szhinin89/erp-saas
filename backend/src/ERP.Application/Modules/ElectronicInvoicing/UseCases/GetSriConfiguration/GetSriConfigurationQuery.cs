using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriConfiguration;

public record GetSriConfigurationQuery : IRequest<Result<SriConfigurationDto?>>;
