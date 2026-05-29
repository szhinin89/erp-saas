using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed class SendIssuedRetentionCommandHandler
    : IRequestHandler<SendIssuedRetentionCommand, Result<Guid>>
{
    private readonly IPurchBillRepository                    _compraRepository;
    private readonly ISriSettingsRepository                  _configSriRepository;
    private readonly ICompanyRepository                      _companyRepository;
    private readonly ISriComprobanteRetentionService         _sri;
    private readonly IFileStorage                            _fileStorage;
    private readonly IAccountingService                      _accounting;
    private readonly IUserActivityRepository                 _activity;
    private readonly IUnitOfWork                             _unitOfWork;
    private readonly ISecretProtector                        _secretProtector;
    private readonly ICurrentSubscriber                      _currentSubscriber;
    private readonly ICurrentCompany                         _currentCompany;
    private readonly ICurrentUser                            _currentUser;
    private readonly ILogger<SendIssuedRetentionCommandHandler> _logger;

    public SendIssuedRetentionCommandHandler(
        IPurchBillRepository compraRepository,
        ISriSettingsRepository configSriRepository,
        ICompanyRepository companyRepository,
        ISriComprobanteRetentionService sri,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ILogger<SendIssuedRetentionCommandHandler> logger)
    {
        _compraRepository    = compraRepository;
        _configSriRepository = configSriRepository;
        _companyRepository   = companyRepository;
        _sri                 = sri;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _secretProtector     = secretProtector;
        _currentSubscriber   = currentSubscriber;
        _currentCompany      = currentCompany;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(SendIssuedRetentionCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var ret = await _compraRepository.GetIssuedRetentionByIdWithLinesAsync(subscriberId, command.RetencionId, ct);
        if (ret is null)
            return Result<Guid>.Failure("Retención no encontrada.");

        if (ret.Status == "Draft")
            ret.Validate(userId);

        if (ret.Status != "Validated")
            return Result<Guid>.Failure($"Estado inválido para enviar: {ret.Status}");

        var companyId = _currentCompany.CompanyId;

        var configSri = await _configSriRepository.GetByCompanyIdAsync(companyId, ct);
        if (configSri is null)
            return Result<Guid>.Failure("La configuración SRI no está configurada para esta empresa.");

        var company = await _companyRepository.GetByIdAsync(companyId, ct);
        if (company is null)
            return Result<Guid>.Failure("Empresa no encontrada.");

        var detalles = ret.Lines.ToList();
        string xml;
        byte[] firmado;
        try
        {
            xml     = await _sri.GenerarXmlRetencionAsync(ret, detalles, configSri, company);
            firmado = await _sri.FirmarXmlAsync(xml, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
        }
        catch (SriCommunicationException ex)
        {
            ret.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            ret.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }

        SriAutorizacionResponse resp;
        try
        {
            resp = await _sri.EnviarAsync(firmado, configSri.WsdlUrl);
        }
        catch (Exception ex)
        {
            ret.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }

        if (!resp.IsAuthorized)
        {
            ret.Reject(userId, resp.ErrorMessage ?? "Rechazada");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(resp.ErrorMessage ?? "Rechazada");
        }

        var xmlGen = $"compras/retenciones/{subscriberId}/{ret.Id}/generado.xml";
        var xmlAut = $"compras/retenciones/{subscriberId}/{ret.Id}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGen, new MemoryStream(firmado), ct);
            await _fileStorage.SaveAsync(xmlAut, new MemoryStream(Encoding.UTF8.GetBytes(resp.AuthorizedXml)), ct);
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
            var refNum = $"{ret.EstablishmentCode}-{ret.EmissionPointCode}-{ret.Sequential}";
            var asiento = await _accounting.CrearAsientoRetencionEmitidaAsync(
                ret.Id, refNum, ret.IssueDate, ret.TotalRetained,
                $"Retención en la fuente {refNum}", ct);
            if (!asiento.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                ret.Reject(userId, asiento.Error ?? "Asiento");
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asiento.Error ?? "Asiento");
            }

            ret.Authorize(userId, resp.AuthNumber, resp.AuthDate, xmlGen, xmlAut, asiento.Value);
            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "compras", action: "compras.retencion.enviar",
                entityType: "IssuedRetention", entityId: ret.Id,
                description: refNum), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(ret.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            ret.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
