using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.CreateExpense;

public sealed class CreateExpenseCommandHandler
    : IRequestHandler<CreateExpenseCommand, Result<ExpenseInvoiceDto>>
{
    private readonly IExpenseInvoiceRepository   _gastos;
    private readonly IBusinessPartnerRepository  _bpRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage            _storage;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CreateExpenseCommandHandler> _logger;

    public CreateExpenseCommandHandler(
        IExpenseInvoiceRepository gastos,
        IBusinessPartnerRepository bpRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<CreateExpenseCommandHandler> logger)
    {
        _gastos = gastos;
        _bpRepo = bpRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _subscriber = subscriber;
        _user          = user;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public Task<Result<ExpenseInvoiceDto>> Handle(CreateExpenseCommand command, CancellationToken ct)
        => command.Modo == ExpenseCreationMode.Xml
            ? HandleXml(command, ct)
            : HandleManual(command, ct);

    private async Task<Result<ExpenseInvoiceDto>> HandleXml(CreateExpenseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        FacturaParseResult parsed;
        try
        {
            using var stream = new MemoryStream(command.XmlContent!);
            parsed = await _parser.ParseAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fallo al parsear XML de gasto (tenant {SubscriberId}, usuario {UserId}).",
                subscriberId, userId);
            return Result<ExpenseInvoiceDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        if (await _gastos.ExistsAccessKeyAsync(subscriberId, parsed.AccessKey, ct))
            return Result<ExpenseInvoiceDto>.Failure(
                $"Ya existe un gasto registrado con la clave de acceso '{parsed.AccessKey}'.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var bp = await ObtenerOCrearBusinessPartner(
                subscriberId, parsed.SupplierRuc, parsed.SupplierLegalName, userId, ct);

            var xmlPath = $"facturas/gastos/{parsed.AccessKey}.xml";
            using (var xmlStream = new MemoryStream(command.XmlContent!))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            var concepto = parsed.Items.Count > 0
                ? parsed.Items[0].Description.Trim()
                : $"Gasto {parsed.InvoiceNumber}";

            ExpenseInvoice gasto;
            try
            {
                gasto = ExpenseInvoice.CreateFromXml(
                    subscriberId,
                    bp.Id,
                    parsed.AccessKey,
                    parsed.InvoiceNumber,
                    parsed.IssueDate,
                    concepto,
                    command.Category!.Trim(),
                    parsed.Subtotal,
                    parsed.VatTotal,
                    parsed.Total,
                    xmlPath,
                    command.Notes,
                    userId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Fallo al construir gasto desde XML parseado (tenant {SubscriberId}, clave {Clave}).",
                    subscriberId, parsed.AccessKey);
                return Result<ExpenseInvoiceDto>.Failure(ex.Message);
            }

            await _gastos.AddAsync(gasto, ct);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.crear.xml",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: $"{parsed.InvoiceNumber} — {bp.Name.LegalName}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Gasto creado desde XML: id {GastoId}, tenant {SubscriberId}, clave {AccessKey}.",
                gasto.Id, subscriberId, parsed.AccessKey);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear gasto desde XML (tenant {SubscriberId})", subscriberId);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo registrar el gasto: {ex.Message}");
        }
    }

    private async Task<Result<ExpenseInvoiceDto>> HandleManual(CreateExpenseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        if (command.Total!.Value > ExpenseInvoice.RequiresXmlThreshold)
            return Result<ExpenseInvoiceDto>.Failure(
                $"Los gastos con total estrictamente mayor a {ExpenseInvoice.RequiresXmlThreshold} deben registrarse con comprobante XML.");

        Guid? bpId = command.BusinessPartnerId;
        if (bpId.HasValue)
        {
            var p = await _bpRepo.GetByIdAsync(bpId.Value, ct);
            if (p is null)
                return Result<ExpenseInvoiceDto>.Failure("Proveedor no encontrado.");
            if (!p.IsActive)
                return Result<ExpenseInvoiceDto>.Failure("El proveedor está deshabilitado.");
        }

        ExpenseInvoice gasto;
        try
        {
            gasto = ExpenseInvoice.CreateManual(
                subscriberId,
                bpId,
                command.IssueDate!.Value,
                command.Concept!,
                command.Category!,
                command.Subtotal!.Value,
                command.VatTotal!.Value,
                command.Total!.Value,
                command.Notes,
                userId);
        }
        catch (Exception ex)
        {
            return Result<ExpenseInvoiceDto>.Failure(ex.Message);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _gastos.AddAsync(gasto, ct);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.crear.manual",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: gasto.Concept), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Gasto creado manual: id {GastoId}, tenant {SubscriberId}, concepto {Description}.",
                gasto.Id, subscriberId, gasto.Concept);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear gasto manual (tenant {SubscriberId})", subscriberId);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo registrar el gasto: {ex.Message}");
        }
    }

    private async Task<BusinessPartner> ObtenerOCrearBusinessPartner(
        Guid subscriberId, string ruc, string razonSocial, Guid userId, CancellationToken ct)
    {
        var existente = await _bpRepo.GetByIdentificationAsync("04", ruc, ct);
        if (existente is not null) return existente;

        var nuevo = BusinessPartner.Create(subscriberId, "04", ruc, ERP.Domain.MasterData.Enums.PersonType.Legal, razonSocial, userId);
        await _bpRepo.AddAsync(nuevo, ct);
        return nuevo;
    }

    private static ExpenseInvoiceDto ToDto(ExpenseInvoice g) => new(
        g.Id,
        g.AccessKey,
        g.IssueDate,
        g.BusinessPartnerId,
        g.InvoiceNumber,
        g.Concept,
        g.Category,
        g.Subtotal,
        g.TaxTotal,
        g.Total,
        g.Status,
        g.XmlPath,
        g.Notes,
        g.JournalEntryId,
        g.CreatedAt);
}

