using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.Retenciones;

public sealed class EnviarCompraRetencionEmitidaCommandHandler
    : IRequestHandler<EnviarCompraRetencionEmitidaCommand, Result<Guid>>
{
    private readonly ICompraRepository                    _compraRepository;
    private readonly IConfiguracionSRIRepository          _configSriRepository;
    private readonly ISriComprobanteRetencionService      _sri;
    private readonly IFileStorage                         _fileStorage;
    private readonly IAccountingService                   _accounting;
    private readonly IUserActivityRepository              _activity;
    private readonly IUnitOfWork                          _unitOfWork;
    private readonly ICurrentTenant                       _currentTenant;
    private readonly ICurrentUser                         _currentUser;
    private readonly ILogger<EnviarCompraRetencionEmitidaCommandHandler> _logger;

    public EnviarCompraRetencionEmitidaCommandHandler(
        ICompraRepository compraRepository,
        IConfiguracionSRIRepository configSriRepository,
        ISriComprobanteRetencionService sri,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<EnviarCompraRetencionEmitidaCommandHandler> logger)
    {
        _compraRepository      = compraRepository;
        _configSriRepository   = configSriRepository;
        _sri                   = sri;
        _fileStorage           = fileStorage;
        _accounting            = accounting;
        _activity              = activity;
        _unitOfWork            = unitOfWork;
        _currentTenant         = currentTenant;
        _currentUser           = currentUser;
        _logger                = logger;
    }

    public async Task<Result<Guid>> Handle(EnviarCompraRetencionEmitidaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var ret = await _compraRepository.GetRetencionEmitidaByIdWithDetailsAsync(tenantId, command.RetencionId, ct);
        if (ret is null)
            return Result<Guid>.Failure("Retención no encontrada.");

        if (ret.Estado == "Borrador")
            ret.Validar(userId);

        if (ret.Estado != "Validado")
            return Result<Guid>.Failure($"Estado inválido para enviar: {ret.Estado}");

        var configSri = await _configSriRepository.GetByTenantIdAsync(tenantId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("Configuración SRI ausente.");

        var detalles = ret.Detalles.ToList();
        string xml;
        byte[] firmado;
        try
        {
            xml     = await _sri.GenerarXmlRetencionAsync(ret, detalles, configSri);
            firmado = await _sri.FirmarXmlAsync(xml, configSri.CertificadoP12Path, configSri.CertificadoPassword);
        }
        catch (SriCommunicationException ex)
        {
            ret.MarcarErrorEnvio(userId, ex.Message);
            await _compraRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            ret.MarcarErrorEnvio(userId, ex.Message);
            await _compraRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }

        SriAutorizacionResponse resp;
        try
        {
            resp = await _sri.EnviarAsync(firmado, configSri.UrlSriAutorizacion);
        }
        catch (Exception ex)
        {
            ret.MarcarErrorEnvio(userId, ex.Message);
            await _compraRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }

        if (!resp.Autorizada)
        {
            ret.Rechazar(userId, resp.MensajeError ?? "Rechazada");
            await _compraRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(resp.MensajeError ?? "Rechazada");
        }

        var xmlGen = $"compras/retenciones/{tenantId}/{ret.Id}/generado.xml";
        var xmlAut = $"compras/retenciones/{tenantId}/{ret.Id}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGen, new MemoryStream(firmado), ct);
            await _fileStorage.SaveAsync(xmlAut, new MemoryStream(Encoding.UTF8.GetBytes(resp.XmlAutorizado)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XML retención no guardado");
            xmlGen = null!;
            xmlAut = null!;
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var refNum = $"{ret.Establecimiento}-{ret.PuntoEmision}-{ret.Secuencial}";
            var asiento = await _accounting.CrearAsientoRetencionEmitidaAsync(
                ret.Id, refNum, ret.FechaEmision, ret.TotalRetenido,
                $"Retención en la fuente {refNum}", ct);
            if (!asiento.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                ret.MarcarErrorEnvio(userId, asiento.Error ?? "Asiento");
                await _compraRepository.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asiento.Error ?? "Asiento");
            }

            ret.Autorizar(userId, resp.NumeroAutorizacion, resp.FechaAutorizacion, xmlGen, xmlAut, asiento.Value);
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "compras.retencion.enviar",
                entityType: "CompraRetencionEmitida", entityId: ret.Id,
                description: refNum), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(ret.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            ret.MarcarErrorEnvio(userId, ex.Message);
            await _compraRepository.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
