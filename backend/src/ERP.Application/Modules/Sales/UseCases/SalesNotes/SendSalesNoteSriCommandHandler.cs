using ERP.Domain.Common;
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
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed class SendSalesNoteSriCommandHandler : IRequestHandler<SendSalesNoteSriCommand, Result<Guid>>
{
    private readonly ISalesRepository             _ventasRepository;
    private readonly ISalesOriginalBillResolver   _originalBillResolver;
    private readonly ISriSettingsRepository       _configSriRepository;
    private readonly ICompanyRepository           _companyRepository;
    private readonly ISriElectronicInvoiceService _sriService;
    private readonly IFileStorage                 _fileStorage;
    private readonly IAccountingService           _accounting;
    private readonly IProductRepository           _productRepository;
    private readonly IUserActivityRepository      _activity;
    private readonly IUnitOfWork                  _unitOfWork;
    private readonly ISecretProtector             _secretProtector;
    private readonly ICurrentSubscriber           _currentSubscriber;
    private readonly ICurrentCompany              _currentCompany;
    private readonly ICurrentUser                 _currentUser;
    private readonly ILogger<SendSalesNoteSriCommandHandler> _logger;

    public SendSalesNoteSriCommandHandler(
        ISalesRepository ventasRepository,
        ISalesOriginalBillResolver originalBillResolver,
        ISriSettingsRepository configSriRepository,
        ICompanyRepository companyRepository,
        ISriElectronicInvoiceService sriService,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IProductRepository productRepository,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ILogger<SendSalesNoteSriCommandHandler> logger)
    {
        _ventasRepository     = ventasRepository;
        _companyRepository    = companyRepository;
        _originalBillResolver = originalBillResolver;
        _configSriRepository  = configSriRepository;
        _sriService           = sriService;
        _fileStorage          = fileStorage;
        _accounting           = accounting;
        _productRepository    = productRepository;
        _activity             = activity;
        _unitOfWork           = unitOfWork;
        _secretProtector      = secretProtector;
        _currentSubscriber    = currentSubscriber;
        _currentCompany       = currentCompany;
        _currentUser          = currentUser;
        _logger               = logger;
    }

    public async Task<Result<Guid>> Handle(SendSalesNoteSriCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var loadResult = await LoadNoteForIssueAsync(subscriberId, command.NoteId, userId, ct);
        if (!loadResult.IsSuccess)
            return Result<Guid>.Failure(loadResult.Error!);

        var (note, originalBill, configSri, lines) = loadResult.Value;

        var company = await _companyRepository.GetByIdAsync(_currentCompany.CompanyId, ct)
            ?? throw new InvalidOperationException("Empresa no encontrada.");

        var xmlResult = await GenerateAndSignNoteXmlAsync(
            originalBill, note, lines, configSri, company, userId, ct);
        if (!xmlResult.IsSuccess)
            return Result<Guid>.Failure(xmlResult.Error!);

        var sendResult = await SendNoteToSriAsync(note, xmlResult.Value, configSri, userId, ct);
        if (!sendResult.IsSuccess)
            return Result<Guid>.Failure(sendResult.Error!);

        var response = sendResult.Value;
        if (!response.IsAuthorized)
        {
            note.Reject(userId, response.ErrorMessage ?? "Rechazada por el SRI.");
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(response.ErrorMessage ?? "El SRI rechazÃ³ la note.");
        }

        var (generatedXmlPath, authorizationXmlPath) = await SaveNoteXmlFilesAsync(
            subscriberId, note.Id, xmlResult.Value, response.AuthorizedXml, ct);

        return await AuthorizeNoteInTransactionAsync(
            note, originalBill, lines, subscriberId, userId,
            response, generatedXmlPath, authorizationXmlPath, ct);
    }

    private async Task<Result<(ERP.Domain.Modules.Sales.Entities.SalesNote note, ERP.Domain.Modules.Sales.Entities.SalesBill originalBill, ERP.Domain.Configuration.Entities.SriSettings Config, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> lines)>>
        LoadNoteForIssueAsync(Guid subscriberId, Guid NoteId, Guid userId, CancellationToken ct)
    {
        var note = await _ventasRepository.GetNoteByIdWithLinesAsync(subscriberId, NoteId, ct);
        if (note is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "note no encontrada.");

        if (note.Status == "Draft")
            note.Validate(userId);

        if (note.Status != "Validated")
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                $"La note debe estar Validated para enviar (estado: {note.Status}).");

        var originalBill = await _originalBillResolver.ResolveAsync(subscriberId, note.OriginalBillId, ct);
        if (originalBill is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "salesBill original no encontrada.");

        if (originalBill.Status != "Autorizado")
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "La salesBill original debe permanecer autorizada.");

        var configSri = await _configSriRepository.GetByCompanyIdAsync(_currentCompany.CompanyId, ct);
        if (configSri is null)
            return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Failure(
                "La configuraciÃ³n SRI no estÃ¡ configurada para esta empresa.");

        return Result<(ERP.Domain.Modules.Sales.Entities.SalesNote, ERP.Domain.Modules.Sales.Entities.SalesBill, ERP.Domain.Configuration.Entities.SriSettings, List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine>)>.Success(
            (note, originalBill, configSri, note.Lines.ToList()));
    }

    private async Task<Result<byte[]>> GenerateAndSignNoteXmlAsync(
        ERP.Domain.Modules.Sales.Entities.SalesBill originalBill,
        ERP.Domain.Modules.Sales.Entities.SalesNote note,
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> lines,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        ERP.Domain.Modules.Company.Entities.Company company,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var xmlContent = await _sriService.GenerateCreditDebitNoteXmlAsync(originalBill, note, lines, configSri, company);
            var signedXml = await _sriService.SignXmlAsync(
                xmlContent, configSri.CertP12Path, _secretProtector.UnprotectOrPlaintext(configSri.CertPassword));
            return Result<byte[]>.Success(signedXml);
        }
        catch (SriCommunicationException ex)
        {
            note.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<byte[]>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            note.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<byte[]>.Failure($"Error al generar XML: {ex.Message}");
        }
    }

    private async Task<Result<SriAuthorizationResponse>> SendNoteToSriAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote note,
        byte[] signedXml,
        ERP.Domain.Configuration.Entities.SriSettings configSri,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var response = await _sriService.SendToSriAsync(signedXml, configSri.WsdlUrl);
            return Result<SriAuthorizationResponse>.Success(response);
        }
        catch (SriCommunicationException ex)
        {
            note.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<SriAuthorizationResponse>.Failure($"Error SRI: {ex.Message}");
        }
        catch (Exception ex)
        {
            note.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<SriAuthorizationResponse>.Failure($"Error de comunicaciÃ³n con SRI: {ex.Message}");
        }
    }

    private async Task<(string? generatedXmlPath, string? authorizationXmlPath)> SaveNoteXmlFilesAsync(
        Guid subscriberId,
        Guid NoteId,
        byte[] signedXml,
        string authorizedXml,
        CancellationToken ct)
    {
        var generatedXmlPath     = $"ventas/notas/{subscriberId}/{NoteId}/generado.xml";
        var authorizationXmlPath = $"ventas/notas/{subscriberId}/{NoteId}/autorizado.xml";
        try
        {
            await _fileStorage.SaveAsync(generatedXmlPath, new MemoryStream(signedXml), ct);
            await _fileStorage.SaveAsync(authorizationXmlPath,
                new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar XML de note {NoteId}", NoteId);
            generatedXmlPath     = null;
            authorizationXmlPath = null;
        }
        return (generatedXmlPath, authorizationXmlPath);
    }

    private async Task<Result<Guid>> AuthorizeNoteInTransactionAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote note,
        ERP.Domain.Modules.Sales.Entities.SalesBill originalBill,
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> lines,
        Guid subscriberId,
        Guid userId,
        SriAuthorizationResponse response,
        string? generatedXmlPath,
        string? authorizationXmlPath,
        CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var numero = $"{note.EstabCode}-{note.EmPointCode}-{note.Sequential}";
            var isCredit = note.NoteType == NoteType.Credit;
            var journalEntryResult = await CreateNoteAccountingEntryAsync(note, numero, isCredit, ct);

            if (!journalEntryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                note.Reject(userId, $"Autorizado por SRI pero fallÃ³ el asiento: {journalEntryResult.Error}");
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<Guid>.Failure(journalEntryResult.Error ?? "Error contable.");
            }

            if (isCredit)
            {
                var stockLines = await BuildCreditNoteStockLinesAsync(lines, subscriberId, ct);
                note.AuthorizeCreditNote(
                    userId,
                    originalBill.WarehouseId,
                    response.AuthNumber,
                    response.AuthDate,
                    generatedXmlPath,
                    authorizationXmlPath,
                    journalEntryResult.Value,
                    stockLines,
                    originalBill.CompanyId);
            }
            else
            {
                note.AuthorizeDebitNote(
                    userId,
                    response.AuthNumber,
                    response.AuthDate,
                    generatedXmlPath,
                    authorizationXmlPath,
                    journalEntryResult.Value);
            }

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.note.enviar",
                entityType: "SalesNote", entityId: note.Id,
                description: $"{numero} â€” auth {response.AuthNumber}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(note.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            note.Reject(userId, ex.Message);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }


    private async Task<Result<Guid>> CreateNoteAccountingEntryAsync(
        ERP.Domain.Modules.Sales.Entities.SalesNote note,
        string numero,
        bool isCredit,
        CancellationToken ct)
    {
        if (isCredit)
            return await _accounting.CreateSalesCreditNoteJournalEntryAsync(
                note.Id, numero, note.IssueDate, note.Subtotal, note.VatTotal, note.Total,
                $"note de crÃ©dito {numero}", ct);

        return await _accounting.CreateSalesDebitNoteJournalEntryAsync(
            note.Id, numero, note.IssueDate, note.Subtotal, note.VatTotal, note.Total,
            $"note de dÃ©bito {numero}", ct);
    }

    private async Task<List<SalesNoteStockLine>> BuildCreditNoteStockLinesAsync(
        List<ERP.Domain.Modules.Sales.Entities.SalesNoteLine> lines,
        Guid subscriberId,
        CancellationToken ct)
    {
        var stockLines = new List<SalesNoteStockLine>();
        foreach (var line in lines)
        {
            var producto = await _productRepository.GetByIdAsync(line.ProductId, subscriberId, ct);
            if (producto is null || producto.IsService || !producto.TracksStock)
                continue;
            stockLines.Add(new SalesNoteStockLine(line.ProductId, line.Quantity));
        }
        return stockLines;
    }
}

