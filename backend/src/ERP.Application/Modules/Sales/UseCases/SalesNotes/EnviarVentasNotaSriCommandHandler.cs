using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Modules.Fiscal.Integration;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed class EnviarSalesNotesriCommandHandler : IRequestHandler<SendSalesNoteSriCommand, Result<Guid>>
{
    private readonly ISalesRepository             _ventasRepository;
    private readonly ISalesOriginalBillResolver   _originalBillResolver;
    private readonly ISriSettingsRepository _configSriRepository;
    private readonly ISriFacturaElectronicaService _sriService;
    private readonly IFileStorage                _fileStorage;
    private readonly IAccountingService          _accounting;
    private readonly IProductRepository          _productRepository;
    private readonly IUserActivityRepository     _activity;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ISecretProtector            _secretProtector;
    private readonly ICurrentSubscriber              _currentSubscriber;
    private readonly ICurrentUser                _currentUser;
    private readonly ILogger<EnviarSalesNotesriCommandHandler> _logger;

    public EnviarSalesNotesriCommandHandler(
        ISalesRepository ventasRepository,
        ISalesOriginalBillResolver originalBillResolver,
        ISriSettingsRepository configSriRepository,
        ISriFacturaElectronicaService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<EnviarSalesNotesriCommandHandler> logger)
    {
        _ventasRepository    = ventasRepository;
        _originalBillResolver  = originalBillResolver;
        _configSriRepository = configSriRepository;
        _sriService          = sriService;
        _fileStorage         = fileStorage;
        _accounting          = accounting;
        _productRepository   = productRepository;
        _activity            = activity;
        _unitOfWork          = unitOfWork;
        _secretProtector     = secretProtector;
        _currentSubscriber       = currentSubscriber;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    public async Task<Result<Guid>> Handle(SendSalesNoteSriCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var loadResult = await LoadNoteForIssueAsync(subscriberId, command.NotaId, userId, ct);
        if (!loadResult.IsSuccess)
            return Result<Guid>.Failure(loadResult.Error!);

        var (nota, facturaOriginal, configSri, detalles) = loadResult.Value;

        var xmlResult = await GenerateAndSignNoteXmlAsync(
            facturaOriginal, nota, detalles, configSri, userId, ct);
        if (!xmlResult.IsSuccess)
            return Result<Guid>.Failure(xmlResult.Error!);

        var sendResult = await SendNoteToSriAsync(nota, xmlResult.Value, configSri, userId, ct);
        if (!sendResult.IsSuccess)
            return Result<Guid>.Failure(sendResult.Error!);

        var response = sendResult.Value;
        if (!response.IsAuthorized)
        {
            nota.Reject(userId, response.ErrorMessage ?? "Rechazada por el SRI.");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(response.ErrorMessage ?? "El SRI rechazó la nota.");
        }

        var (xmlGeneradoPath, xmlAutorizacionPath) = await SaveNoteXmlFilesAsync(
            subscriberId, nota.Id, xmlResult.Value, response.AuthorizedXml, ct);

        return await AuthorizeNoteInTransactionAsync(
            nota, facturaOriginal, detalles, subscriberId, userId,
            response, xmlGeneradoPath, xmlAutorizacionPath, ct);
    }

    private async Task<Result<(ERP.Domain.Modules.Sales.Entities.SalesNote Nota, ERP.Domain.Modules.Sales.Entities.SalesBill FacturaOriginal, ERP.Domain.Configuration.Entities.SriSettings Config, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> Detalles)>>
        LoadNoteForIssueAsync(Guid subscriberId, Guid notaId, Guid userId, CancellationToken ct)
    {
        var nota = await _ventasRepository.GetNoteByIdWithLinesAsync(subscriberId, notaId, ct);
        if (nota is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "Nota no encontrada.");

        if (nota.Status == "Draft")
            nota.Validate(userId);

        if (nota.Status != "Validated")
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                $"La nota debe estar Validated para enviar (estado: {nota.Status}).");

        var facturaOriginal = await _originalBillResolver.ResolveAsync(subscriberId, nota.OriginalBillId, ct);
        if (facturaOriginal is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "Factura original no encontrada.");

        if (facturaOriginal.Status != "Autorizado")
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "La factura original debe permanecer autorizada.");

        var configSri = await _configSriRepository.GetBySubscriberIdAsync(subscriberId, ct);
        if (configSri is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "La configuración SRI no está configurada para este tenant.");

        return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Success(
            (nota, facturaOriginal, configSri, nota.Lines.ToList()));
    }

    private async Task<Result<byte[]>> GenerateAndSignNoteXmlAsync(
        ERP.Domain.Modules.Sales.Entities.SalesBill facturaOriginal,
        ERP.Domain.Modules.Sales.Entities.SalesNote nota,
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> detalles,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var xmlContent = await _sriService.GenerarXmlNotaCreditoDebitoAsync(facturaOriginal, nota, detalles, configSri);
            var xmlFirmado = await _sriService.FirmarXmlAsync(
                xmlContent, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
            return Result<byte[]>.Success(xmlFirmado);
        }
        catch (SriCommunicationException ex)
        {
            nota.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<byte[]>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            nota.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<byte[]>.Failure($"Error al generar XML: {ex.Message}");
        }
    }

    private async Task<Result<SriAutorizacionResponse>> SendNoteToSriAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote nota,
        byte[] xmlFirmado,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var response = await _sriService.EnviarAlSriAsync(xmlFirmado, configSri.WsdlUrl);
            return Result<SriAutorizacionResponse>.Success(response);
        }
        catch (SriCommunicationException ex)
        {
            nota.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<SriAutorizacionResponse>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            nota.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<SriAutorizacionResponse>.Failure($"Error de comunicación con SRI: {ex.Message}");
        }
    }

    private async Task<(string? XmlGeneradoPath, string? XmlAutorizacionPath)> SaveNoteXmlFilesAsync(
        Guid subscriberId,
        Guid notaId,
        byte[] xmlFirmado,
        string authorizedXml,
        CancellationToken ct)
    {
        var xmlGeneradoPath     = $"ventas/notas/{subscriberId}/{notaId}/generado.xml";
        var xmlAutorizacionPath = $"ventas/notas/{subscriberId}/{notaId}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(xmlGeneradoPath, new MemoryStream(xmlFirmado), ct);
            await _fileStorage.SaveAsync(xmlAutorizacionPath,
                new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar XML de nota {NotaId}", notaId);
            xmlGeneradoPath     = null;
            xmlAutorizacionPath = null;
        }
        return (xmlGeneradoPath, xmlAutorizacionPath);
    }

    private async Task<Result<Guid>> AuthorizeNoteInTransactionAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote nota,
        ERP.Domain.Modules.Sales.Entities.SalesBill facturaOriginal,
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> detalles,
        Guid subscriberId,
        Guid userId,
        SriAutorizacionResponse response,
        string? xmlGeneradoPath,
        string? xmlAutorizacionPath,
        CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var numero = $"{nota.EstabCode}-{nota.EmPointCode}-{nota.Sequential}";
            var isCredit = IsCreditNote(nota.NoteType);
            var asientoResult = await CreateNoteAccountingEntryAsync(nota, numero, isCredit, ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                nota.Reject(userId, $"Autorizado por SRI pero falló el asiento: {asientoResult.Error}");
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(asientoResult.Error ?? "Error contable.");
            }

            if (isCredit)
            {
                var stockLines = await BuildCreditNoteStockLinesAsync(detalles, subscriberId, ct);
                nota.AuthorizeCreditNote(
                    userId,
                    facturaOriginal.WarehouseId,
                    response.AuthNumber,
                    response.AuthDate,
                    xmlGeneradoPath,
                    xmlAutorizacionPath,
                    asientoResult.Value,
                    stockLines,
                    facturaOriginal.CompanyId);
            }
            else
            {
                nota.AuthorizeDebitNote(
                    userId,
                    response.AuthNumber,
                    response.AuthDate,
                    xmlGeneradoPath,
                    xmlAutorizacionPath,
                    asientoResult.Value);
            }

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.nota.enviar",
                entityType: "SalesNote", entityId: nota.Id,
                description: $"{numero} — auth {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(nota.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            nota.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }

    private static bool IsCreditNote(string noteType) =>
        string.Equals(noteType, "CREDIT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(noteType, "CREDITO", StringComparison.OrdinalIgnoreCase);

    private async Task<Result<Guid>> CreateNoteAccountingEntryAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote nota,
        string numero,
        bool isCredit,
        CancellationToken ct)
    {
        if (isCredit)
            return await _accounting.CrearAsientoNotaCreditoVentaAsync(
                nota.Id, numero, nota.IssueDate, nota.Subtotal, nota.VatTotal, nota.Total,
                $"Nota de crédito {numero}", ct);

        return await _accounting.CrearAsientoNotaDebitoVentaAsync(
            nota.Id, numero, nota.IssueDate, nota.Subtotal, nota.VatTotal, nota.Total,
            $"Nota de débito {numero}", ct);
    }

    private async Task<List<SalesNoteStockLine>> BuildCreditNoteStockLinesAsync(
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> detalles,
        Guid subscriberId,
        CancellationToken ct)
    {
        var stockLines = new List<SalesNoteStockLine>();
        foreach (var detalle in detalles)
        {
            var producto = await _productRepository.GetByIdAsync(detalle.ProductId, subscriberId, ct);
            if (producto is null || producto.IsService || !producto.TracksStock)
                continue;
            stockLines.Add(new SalesNoteStockLine(detalle.ProductId, detalle.Quantity));
        }
        return stockLines;
    }
}
