using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.GetConfiguracionSRI;

public sealed record GetConfiguracionSRIQuery : IRequest<Result<ConfiguracionSRIDto?>>;
