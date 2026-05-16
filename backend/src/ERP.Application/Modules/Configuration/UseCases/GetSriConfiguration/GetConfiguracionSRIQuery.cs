using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.GetSriSettings;

public record GetSriConfigurationQuery : IRequest<Result<SriConfigurationDto?>>;
