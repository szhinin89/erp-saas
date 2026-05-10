using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionSRI;

public sealed record UpsertConfiguracionSRICommand(
    string  RucEmpresa,
    string  RazonSocial,
    string? NombreComercial,
    string  DireccionMatriz,
    bool    ObligadoContabilidad,
    string? ContribuyenteEspecial,
    string  Establecimiento,
    string  PuntoEmision,
    string  CertificadoP12Path,
    string  CertificadoPassword,
    int     Ambiente,
    int     TipoEmision,
    string  UrlSriAutorizacion
) : IRequest<Result<ConfiguracionSRIDto>>;
