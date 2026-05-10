using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetConfiguracionSRI;

public sealed class GetConfiguracionSRIQueryHandler
    : IRequestHandler<GetConfiguracionSRIQuery, Result<ConfiguracionSRIDto?>>
{
    private readonly IConfiguracionSRIRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetConfiguracionSRIQueryHandler(
        IConfiguracionSRIRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ConfiguracionSRIDto?>> Handle(
        GetConfiguracionSRIQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
            return Result<ConfiguracionSRIDto?>.Success(null);

        return Result<ConfiguracionSRIDto?>.Success(new ConfiguracionSRIDto(
            config.TenantId,
            config.RucEmpresa,
            config.RazonSocial,
            config.NombreComercial,
            config.DireccionMatriz,
            config.ObligadoContabilidad,
            config.ContribuyenteEspecial,
            config.Establecimiento,
            config.PuntoEmision,
            config.SecuencialActual,
            config.CertificadoP12Path,
            config.Ambiente,
            config.TipoEmision,
            config.UrlSriAutorizacion));
    }
}
