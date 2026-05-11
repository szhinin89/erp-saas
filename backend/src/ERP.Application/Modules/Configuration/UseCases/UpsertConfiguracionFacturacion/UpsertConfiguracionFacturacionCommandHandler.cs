using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Common;

namespace ERP.Application.Configuration.UseCases.UpsertConfiguracionFacturacion;

public sealed class UpsertConfiguracionFacturacionCommandHandler
    : IRequestHandler<UpsertConfiguracionFacturacionCommand, Result<ConfiguracionFacturacionDto>>
{
    private readonly IConfiguracionFacturacionRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpsertConfiguracionFacturacionCommandHandler(
        IConfiguracionFacturacionRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ConfiguracionFacturacionDto>> Handle(
        UpsertConfiguracionFacturacionCommand command,
        CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
        {
            config = ConfiguracionFacturacion.Create(
                _currentTenant.TenantId,
                command.RazonSocial,
                command.NombreComercial,
                command.Ruc,
                command.DireccionMatriz,
                command.Telefono,
                command.Correo,
                command.ObligadoContabilidad,
                command.ContribuyenteEspecial,
                command.LogoBase64,
                command.LeyendaAdicional,
                command.AnchoTirilla,
                _currentUser.UserId);

            await _repo.AddAsync(config, ct);
        }
        else
        {
            config.Update(
                command.RazonSocial,
                command.NombreComercial,
                command.Ruc,
                command.DireccionMatriz,
                command.Telefono,
                command.Correo,
                command.ObligadoContabilidad,
                command.ContribuyenteEspecial,
                command.LogoBase64,
                command.LeyendaAdicional,
                command.AnchoTirilla,
                _currentUser.UserId);

            await _repo.UpdateAsync(config, ct);
        }

        await _repo.SaveChangesAsync(ct);

        return Result<ConfiguracionFacturacionDto>.Success(new ConfiguracionFacturacionDto(
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
