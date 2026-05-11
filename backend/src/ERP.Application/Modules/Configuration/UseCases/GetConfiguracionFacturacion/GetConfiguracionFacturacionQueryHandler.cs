using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetConfiguracionFacturacion;

public sealed class GetConfiguracionFacturacionQueryHandler
    : IRequestHandler<GetConfiguracionFacturacionQuery, Result<ConfiguracionFacturacionDto?>>
{
    private readonly IConfiguracionFacturacionRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetConfiguracionFacturacionQueryHandler(
        IConfiguracionFacturacionRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ConfiguracionFacturacionDto?>> Handle(
        GetConfiguracionFacturacionQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
            return Result<ConfiguracionFacturacionDto?>.Success(null);

        return Result<ConfiguracionFacturacionDto?>.Success(new ConfiguracionFacturacionDto(
            config.Id,
            config.TenantId,
            config.RazonSocial,
            config.NombreComercial,
            config.Ruc,
            config.DireccionMatriz,
            config.Telefono,
            config.Correo,
            config.ObligadoContabilidad,
            config.ContribuyenteEspecial,
            config.LogoBase64,
            config.LeyendaAdicional,
            config.AnchoTirilla));
    }
}
