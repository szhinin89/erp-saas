using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionFacturacion;

public sealed record UpsertConfiguracionFacturacionCommand(
    string RazonSocial,
    string NombreComercial,
    string Ruc,
    string DireccionMatriz,
    string Telefono,
    string? Correo,
    bool ObligadoContabilidad,
    string? ContribuyenteEspecial,
    string? LogoBase64,
    string? LeyendaAdicional,
    int AnchoTirilla)
    : IRequest<Result<ERP.Application.Configuration.DTOs.ConfiguracionFacturacionDto>>;
