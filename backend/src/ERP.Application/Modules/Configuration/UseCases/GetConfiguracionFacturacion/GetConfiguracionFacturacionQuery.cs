using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using MediatR;

namespace ERP.Application.Configuration.UseCases.GetConfiguracionFacturacion;

public sealed record GetConfiguracionFacturacionQuery : IRequest<Result<ConfiguracionFacturacionDto?>>;
