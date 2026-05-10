using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionSRI;

public sealed class UpsertConfiguracionSRICommandHandler
    : IRequestHandler<UpsertConfiguracionSRICommand, Result<ConfiguracionSRIDto>>
{
    private readonly IConfiguracionSRIRepository _repo;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;

    public UpsertConfiguracionSRICommandHandler(
        IConfiguracionSRIRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<ConfiguracionSRIDto>> Handle(
        UpsertConfiguracionSRICommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var existing = await _repo.GetByTenantIdAsync(tenantId, ct);

        if (existing is null)
        {
            var config = ConfiguracionSRI.Create(
                tenantId:             tenantId,
                rucEmpresa:           command.RucEmpresa,
                razonSocial:          command.RazonSocial,
                nombreComercial:      command.NombreComercial,
                direccionMatriz:      command.DireccionMatriz,
                obligadoContabilidad: command.ObligadoContabilidad,
                contribuyenteEspecial: command.ContribuyenteEspecial,
                establecimiento:      command.Establecimiento,
                puntoEmision:         command.PuntoEmision,
                secuencialActual:     1,
                certificadoP12Path:   command.CertificadoP12Path,
                certificadoPassword:  command.CertificadoPassword,
                ambiente:             command.Ambiente,
                tipoEmision:          command.TipoEmision,
                urlSriAutorizacion:   command.UrlSriAutorizacion,
                createdBy:            userId);

            await _repo.AddAsync(config, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<ConfiguracionSRIDto>.Success(ToDto(config));
        }

        // Update: preservar SecuencialActual para no romper la numeración en curso
        existing.Update(
            rucEmpresa:           command.RucEmpresa,
            razonSocial:          command.RazonSocial,
            nombreComercial:      command.NombreComercial,
            direccionMatriz:      command.DireccionMatriz,
            obligadoContabilidad: command.ObligadoContabilidad,
            contribuyenteEspecial: command.ContribuyenteEspecial,
            establecimiento:      command.Establecimiento,
            puntoEmision:         command.PuntoEmision,
            certificadoP12Path:   command.CertificadoP12Path,
            certificadoPassword:  command.CertificadoPassword,
            ambiente:             command.Ambiente,
            tipoEmision:          command.TipoEmision,
            urlSriAutorizacion:   command.UrlSriAutorizacion,
            updatedBy:            userId);

        await _repo.UpdateAsync(existing, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ConfiguracionSRIDto>.Success(ToDto(existing));
    }

    private static ConfiguracionSRIDto ToDto(ConfiguracionSRI c) => new(
        c.TenantId, c.RucEmpresa, c.RazonSocial, c.NombreComercial,
        c.DireccionMatriz, c.ObligadoContabilidad, c.ContribuyenteEspecial,
        c.Establecimiento, c.PuntoEmision, c.SecuencialActual,
        c.CertificadoP12Path, c.Ambiente, c.TipoEmision, c.UrlSriAutorizacion);
}
